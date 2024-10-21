Shader "Custom/Combination"
{
    Properties
    {
        [Header(Tessellation)]
        [Space]
        _TessellationUniform ("Tessellation Uniform", Range(1, 64)) = 1
        _TessellationEdgeLength ("Tessellation Edge Length", Range(5, 100)) = 50

        [Header(Grass Color)]
        [Space]
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _BottomColor ("Bottom Color", Color) = (1,1,1,1)

        [Header(Grass Dimensions)]
        [Space]
        _BladeWidth("Blade Width", Float) = 0.05
        _BladeWidthRandom("Blade Width Random", Float) = 0.02
        _BladeHeight("Blade Height", Float) = 0.5
        _BladeHeightRandom("Blade Height Random", Float) = 0.3
        _BendRotationRandom("Bend Rotation Random", Range(0, 1)) = 0.2
    }

    CGINCLUDE
    // Simple noise function, sourced from http://answers.unity.com/answers/624136/view.html
	// Extended discussion on this function can be found at the following link:
	// https://forum.unity.com/threads/am-i-over-complicating-this-random-function.454887/#post-2949326
	// Returns a number in the 0...1 range.
	float rand(float3 co)
	{
		return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
	}

	// Construct a rotation matrix that rotates around the provided axis, sourced from:
	// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
	float3x3 AngleAxis3x3(float angle, float3 axis)
	{
		float c, s;
		sincos(angle, s, c);

		float t = 1 - c;
		float x = axis.x;
		float y = axis.y;
		float z = axis.z;

		return float3x3(
			t * x * x + c, t * x * y - s * z, t * x * z + s * y,
			t * x * y + s * z, t * y * y + c, t * y * z - s * x,
			t * x * z - s * y, t * y * z + s * x, t * z * z + c
        );
	}
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        // Avoid culling back faces (TEMP)
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.6
            
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma geometry geom
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            // Grass color
            float4 _TopColor;
            float4 _BottomColor;

            // Grass dimensions
            float _BladeWidth;
            float _BladeWidthRandom;
            float _BladeHeight;
            float _BladeHeightRandom;
            float _BendRotationRandom;

            // Tessellation
            float _TessellationUniform;
            float _TessellationEdgeLength;

            struct TessellationFactors
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct TessellationControlPoint
            {
                UNITY_FOG_COORDS(1)
                float4 vertex : INTERNALTESSPOS;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct vertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct vertexOutput
            {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            struct geometryOutput
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            geometryOutput VertexOutput(float3 pos, float2 uv)
            {
                geometryOutput o;
                o.pos = UnityObjectToClipPos(pos);
                o.uv = uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float TessellationEdgeFactor(float3 p0, float3 p1)
            {
                return 5;
                float edgeLength = distance(p0, p1);

                float3 edgeCenter = (p0 + p1) * 0.5;
                float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);

                return edgeLength * _ScreenParams.y / (_TessellationEdgeLength * viewDistance);
            }

            TessellationFactors MyPathConstantFunction(InputPatch<vertexInput, 3> patch)
            {
                // Transformed to world space here for shader to optimize TessellationEdgeFactor calls.
                float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex).xyz;
                float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex).xyz;
                float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex).xyz;

                TessellationFactors f;
                f.edge[0] = TessellationEdgeFactor(p1, p2);
                f.edge[1] = TessellationEdgeFactor(p2, p0);
                f.edge[2] = TessellationEdgeFactor(p0, p1);
	
                // Explicitly called due to alleged OpenGL Core bug.
                f.inside =
                    (TessellationEdgeFactor(p1, p2) +
                    TessellationEdgeFactor(p2, p0) +
                    TessellationEdgeFactor(p0, p1)) * (1 / 3.0);
                return f;
            }

            vertexOutput tessVert(vertexInput v)
            {
                vertexOutput o;
                o.vertex = v.vertex;
                UNITY_TRANSFER_FOG(o, o.vertex);
                o.normal = v.normal;
                o.tangent = v.tangent;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //p.uv1 = v.uv1;
                //p.uv2 = v.uv2;
                return o;
            }

            vertexInput vert (vertexInput v)
            {
                return v;
            }

            [UNITY_domain("tri")]
            [UNITY_outputcontrolpoints(3)]
            [UNITY_outputtopology("triangle_cw")]
            [UNITY_partitioning("integer")]
            [UNITY_patchconstantfunc("MyPathConstantFunction")]
            vertexInput hull(InputPatch<vertexInput, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            [UNITY_domain("tri")]
            vertexOutput domain(TessellationFactors factors, OutputPatch<vertexInput, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                vertexOutput o;
                
                #define MY_DOMAIN_PROGRAM_INTERPOLATE(fieldName) o.fieldName = \
                    patch[0].fieldName * barycentricCoordinates.x + \
                    patch[1].fieldName * barycentricCoordinates.y + \
                    patch[2].fieldName * barycentricCoordinates.z;

                MY_DOMAIN_PROGRAM_INTERPOLATE(vertex)
                MY_DOMAIN_PROGRAM_INTERPOLATE(normal)
                MY_DOMAIN_PROGRAM_INTERPOLATE(tangent)
                MY_DOMAIN_PROGRAM_INTERPOLATE(uv)
                // MY_DOMAIN_PROGRAM_INTERPOLATE(uv1)
                // MY_DOMAIN_PROGRAM_INTERPOLATE(uv2)

                return tessVert(o);
            }

            [maxvertexcount(8)]
            void geom(triangle vertexOutput IN[3], inout TriangleStream<geometryOutput> triStream)
            {
                geometryOutput o;

                // Calculate average position, normal, tangent and binormal
                float4 pos = (IN[0].vertex + IN[1].vertex + IN[2].vertex) / 3;
                float3 normal = (IN[0].normal + IN[1].normal + IN[2].normal) / 3;
                float4 tangent = (IN[0].tangent + IN[1].tangent + IN[2].tangent) / 3;
                float3 binormal = cross(normal, tangent.xyz) * tangent.w;

                // Create tangent to local space matrix
                float3x3 tangentToLocal = float3x3(
                    tangent.x, binormal.x, normal.x,
                    tangent.y, binormal.y, normal.y,
                    tangent.z, binormal.z, normal.z
                );

                // Rotate randomly around the normal
                float3x3 facingRotationMatrix = AngleAxis3x3(rand(pos) * UNITY_TWO_PI, float3(0, 0, 1));
                float3x3 bendRotationMatrix = AngleAxis3x3(rand(pos.zzx) * _BendRotationRandom * UNITY_PI * 0.5, float3(-1, 0, 0));

                // Final transformation matrix
                float3x3 transformationMatrix = mul(mul(tangentToLocal, facingRotationMatrix), bendRotationMatrix);

                // Calculate blade dimensions
                float height = _BladeHeight + (2 * rand(pos) - 1) * _BladeHeightRandom;
                float width = _BladeWidth + (2 *rand(pos) - 1) * _BladeWidthRandom;
                
                // Create original triangle
                triStream.Append(VertexOutput(IN[0].vertex, float2(0, 0)));
                triStream.Append(VertexOutput(IN[1].vertex, float2(1, 0)));
                triStream.Append(VertexOutput(IN[2].vertex, float2(0.5, 0)));

                // Reset triangle strip
                triStream.RestartStrip();

                // Create grass blade
                triStream.Append(VertexOutput(pos + mul(transformationMatrix, float3(-width, 0, 0)), float2(0, 0)));
                triStream.Append(VertexOutput(pos + mul(transformationMatrix, float3(width, 0, 0)), float2(1, 0)));
                triStream.Append(VertexOutput(pos + mul(transformationMatrix, float3(-width, 0, height*0.7)), float2(0, 0.7)));
                triStream.Append(VertexOutput(pos + mul(transformationMatrix, float3(width, 0, height*0.7)), float2(1, 0.7)));
                triStream.Append(VertexOutput(pos + mul(transformationMatrix, float3(0, 0, height)), float2(0.5, 1)));
            }

            fixed4 frag (geometryOutput i) : SV_Target
            {
                fixed4 col = lerp(_BottomColor, _TopColor, i.uv.y);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
