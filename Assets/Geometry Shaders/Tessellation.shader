Shader "Custom/Tessellation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TessellationUniform ("Tessellation Uniform", Range(1, 64)) = 1
        _TessellationEdgeLength ("Tessellation Edge Length", Range(5, 100)) = 50
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma hull MyHullProgram
            #pragma domain MyDomainProgram
            #pragma vertex MyTessellationVertexProgram
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct VertexData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (VertexData v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }

            float _TessellationUniform;
            float _TessellationEdgeLength;

            struct TessellationFactors {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct TessellationControlPoint {
                float4 vertex : INTERNALTESSPOS;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            float TessellationEdgeFactor(float3 p0, float3 p1) {
                float edgeLength = distance(p0, p1);

                float3 edgeCenter = (p0 + p1) * 0.5;
                float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);

                return edgeLength * _ScreenParams.y / (_TessellationEdgeLength * viewDistance);
            }

            TessellationFactors MyPathConstantFunction(InputPatch<TessellationControlPoint, 3> patch)
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

            [UNITY_domain("tri")]
            [UNITY_outputcontrolpoints(3)]
            [UNITY_outputtopology("triangle_cw")]
            [UNITY_partitioning("fractional_odd")]
            [UNITY_patchconstantfunc("MyPathConstantFunction")]
            TessellationControlPoint MyHullProgram(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            [UNITY_domain("tri")]
            v2f MyDomainProgram(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                VertexData data;
                
                #define MY_DOMAIN_PROGRAM_INTERPOLATE(fieldName) data.fieldName = \
                    patch[0].fieldName * barycentricCoordinates.x + \
                    patch[1].fieldName * barycentricCoordinates.y + \
                    patch[2].fieldName * barycentricCoordinates.z;

                MY_DOMAIN_PROGRAM_INTERPOLATE(vertex)
                // MY_DOMAIN_PROGRAM_INTERPOLATE(normal)
                // MY_DOMAIN_PROGRAM_INTERPOLATE(tangent)
                MY_DOMAIN_PROGRAM_INTERPOLATE(uv)
                // MY_DOMAIN_PROGRAM_INTERPOLATE(uv1)
                // MY_DOMAIN_PROGRAM_INTERPOLATE(uv2)

                return vert(data);
            }

            TessellationControlPoint MyTessellationVertexProgram(VertexData v)
            {
                TessellationControlPoint p;
                p.vertex = v.vertex;
                //p.normal = v.normal;
                //p.tangent = v.tangent;
                p.uv = v.uv;
                //p.uv1 = v.uv1;
                //p.uv2 = v.uv2;
                return p;
            }
            ENDCG
        }
    }
}
