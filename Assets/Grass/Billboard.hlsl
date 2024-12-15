// Make sure file is not included twice
#ifndef BILLBOARD_HLSL
#define BILLBOARD_HLSL

// Includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Variables
half4 _Color;
Texture2D _LeafTexture;
float4 _LeafTexture_ST;
SamplerState sampler_LeafTexture;

float _Scale;
float _Billboarding;

// Mesh vertex input data
struct InstancedVertexInput {
    float3 position : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Fragment shader input data
struct VertexOutput {
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 positionCS : SV_POSITION;
};

// Vertex shader
VertexOutput vert(InstancedVertexInput input, uint svInstanceID : SV_InstanceID) {
    VertexOutput output = (VertexOutput)0;

    // // Get instance data
    // InitIndirectDrawArgs(0);
    // uint cmdID = GetCommandID(0);
    // uint instanceID = GetIndirectInstanceID(svInstanceID);

    // Remap UV to [-1, 1]
    float2 uv = input.uv * 2 - 1;

    // Transform vertex data (Source: https://discussions.unity.com/t/transform-node-differences-between-shadergraph-and-amplify/843779/9)
    float3 bitangent = cross(input.tangent.xyz, input.normal);
    VertexNormalInputs tbn = GetVertexNormalInputs(input.normal, input.tangent);
    float3x3 tangentObject_Transform = float3x3(input.tangent.xyz, bitangent, input.normal);
    float3 transformedOffset = mul(unity_ObjectToWorld, float4(input.position + normalize(mul(float3(uv, 0), tangentObject_Transform)) * _Scale, 1)).xyz;

    // Billboard
    float3 billboardOffset = mul(unity_ObjectToWorld, float4(input.position, 1) + float4(TransformWorldToObject(mul(uv, unity_WorldToCamera)), 1) * _Scale);
    
    // Output combined data
    output.positionWS = lerp(transformedOffset, billboardOffset, _Billboarding);
    output.normalWS = input.normal;
    output.uv = TRANSFORM_TEX(input.uv, _LeafTexture);
    output.positionCS = TransformWorldToHClip(output.positionWS);

    return output;

    // Whole object billboard
    // output.positionWS = mul(unity_WorldToObject, unity_ObjectToWorld._m03_m13_m23 + mul(UNITY_MATRIX_I_V, float4(input.position, 0) * _Scale));
}

// Fragment shader
half4 frag(VertexOutput input, uint svInstanceID : SV_InstanceID) : SV_Target {

    // Sample the texture
    half4 color = _Color.rgba * _LeafTexture.Sample(sampler_LeafTexture, input.uv).a;
    
    // Cutout
    clip(color.a - 0.5);

    // Gather data for lighting
    InputData lightingData = (InputData)0;
    lightingData.positionWS = input.positionWS;
    lightingData.normalWS = input.normalWS;
    lightingData.viewDirectionWS = GetWorldSpaceViewDir(lightingData.positionWS);
    lightingData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

    // Gather data for surface
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = color;
    surfaceData.alpha = 1;

    // Use URP's Blinn-Phong lighting model (Bloom combined with MSAA and HDR causes flickering specular highlights)
    half4 lighting = UniversalFragmentBlinnPhong(lightingData, surfaceData);
    half4 ambient = half4(_GlossyEnvironmentColor.xyz * color, 1);
    return lighting + ambient;
}

#endif // BILLBOARD_HLSL