Shader "Custom/Grass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

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
	#include "UnityCG.cginc"

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
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

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

            vertexOutput vert (vertexInput v)
            {
                vertexOutput o;
                o.vertex = v.vertex;
                o.normal = v.normal;
                o.tangent = v.tangent;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            // Grass color
            float4 _TopColor;
            float4 _BottomColor;

            // Grass dimensions
            float _BladeWidth;
            float _BladeWidthRandom;
            float _BladeHeight;
            float _BladeHeightRandom;
            float _BendRotationRandom;

            fixed4 frag (geometryOutput i) : SV_Target
            {

                fixed4 col = lerp(_BottomColor, _TopColor, i.uv.y);

                // sample the texture
                //fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }

            [maxvertexcount(6)]
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
                triStream.Append(VertexOutput(pos + mul(transformationMatrix, float3(0, 0, height)), float2(0.5, 1)));
            }
            ENDCG
        }
    }
}
