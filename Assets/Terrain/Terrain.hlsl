// Make sure file is not included twice
#ifndef TERRAIN_HLSL
#define TERRAIN_HLSL

// Include some helper functions
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

struct VertexOutput {
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    
    float4 positionCS : SV_POSITION;
};

// Input from compute shader
StructuredBuffer<int> _Triangles;
StructuredBuffer<float3> _Vertices;
StructuredBuffer<int3> _Normals;
StructuredBuffer<float2> _UVs;

// Vertex shader
VertexOutput vert(uint id : SV_VertexID) {
    VertexOutput output;

    // Get the vertex data
    id = _Triangles[id];

    // Transform the vertex
    output.positionWS = mul(unity_ObjectToWorld, float4(_Vertices[id], 1)).xyz;
    output.positionCS = TransformObjectToHClip(output.positionWS);
    output.normalWS = normalize(float3(_Normals[id]));
    output.uv = _UVs[id];

    return output;
}

// Fragment shader
half4 frag(VertexOutput input) : SV_Target {

    // Get the color
    half3 color = lerp(half3(0.2, 0.2, 0.2), half3(0.2, 0.8, 0.2), saturate(input.positionWS.y / 15));

    // Color slanted surfaces with a different color
    color = lerp(half3(0.4, 0.4, 0.4), color, saturate(dot(input.normalWS, float3(0, 1, 0))));

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

#endif // TERRAIN_HLSL
