#ifndef FOLIAGE_HLSL
#define FOLIAGE_HLSL

// Includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

// Custom include to setup and support indirect instancing
#include "IndirectInstancing.hlsl"

// appdata_tan
struct VertexInput {
    float4 vertex : POSITION;
    float4 tangent : TANGENT;
    float3 normal : NORMAL;
    float4 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Fragment shader input data
struct VertexOutput {
    float3 vertex : TEXCOORD1;
    float4 tangent : TEXCOORD3;
    float3 normal : TEXCOORD2;
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID 
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float _Billboarding;
    float _Inflation;
    float4 _Color;

    float _FresnelScale;
    float _FresnelPower;
    float4 _FresnelColor;
CBUFFER_END

// Vertex shader
VertexOutput vert(VertexInput input, uint svInstanceID : SV_InstanceID) {
    VertexOutput output;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    // UV transformation using TBN matrix
    float3x3 TBN = float3x3(input.tangent.xyz, cross(input.tangent.xyz, input.normal), input.normal);
    float3 uv = float3(input.texcoord.xy * 2.0 - 1.0, 0);
    float3 uvOS = mul(uv, TBN);
    float3 uvVS = mul(float4(uv, 0), UNITY_MATRIX_MV).xyz;

    output.vertex = input.vertex.xyz + lerp(uvOS, uvVS, _Billboarding) * _Inflation;
    output.normal = input.normal;
    output.tangent = input.tangent;
    output.positionCS = mul(UNITY_MATRIX_MVP, float4(output.vertex, 1.0));
    output.uv = input.texcoord.xy;
    return output;
}

// Fragment shader
half4 frag(VertexOutput input, uint svInstanceID : SV_InstanceID) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(input);

    half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

    // Discard transparent pixels
    clip(texColor.a - 0.5);

    // Transform from object space to world space
    float3 positionWS = TransformObjectToWorld(input.vertex);
    float3 normalWS = TransformObjectToWorldNormal(input.normal);
    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);

    // Freshnel for subsurface scattering if not in shadow
    half3 fresnel = 1 - saturate(dot(normalWS, viewDirWS));
    fresnel = pow(fresnel, _FresnelPower) * _FresnelScale;

    // Gather data for lighting
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.shadowCoord = shadowCoord;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    // Gather data for surface
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = (texColor * _Color).xyz;
    surfaceData.specular = half3(1, 1, 1);
    surfaceData.metallic = 0;
    surfaceData.smoothness = 0.1;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.emission = fresnel * (texColor).xyz * (_FresnelColor).xyz * _FresnelColor.a;
    surfaceData.occlusion = 0.5;
    surfaceData.alpha = texColor.a;

    half4 lighting = UniversalFragmentPBR(inputData, surfaceData);
    half4 ambient = _GlossyEnvironmentColor * _Color;
    return lighting + ambient;
}

#endif // FOLIAGE_HLSL