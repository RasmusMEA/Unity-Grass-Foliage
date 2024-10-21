// Make sure file is not included twice
#ifndef GRASSBLADES_HLSL
#define GRASSBLADES_HLSL

// Include some helper functions
#include "CommonLibrary.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

// Define the maximum number of blade segments that can be generated
#define MAX_BLADE_SEGMENTS 5

struct VertexInput {
    float3 positionWS : POSITION;
    float2 uv : TEXCOORD0;
};

struct DrawTriangle {
    //GrassBlade blade;
    VertexInput vertices[3];
    float3 normal : NORMAL;
};

struct GrassBlade {
    // Can be used to find source triangle uv, normal and position,
    // allowing for color blending against the terrain
    int sourceTriangleIndex;
    half3 barycentricCoefficients;
};

struct VertexOutput {
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    
    float4 positionCS : SV_POSITION;
};

// Color properties
half4 _TopColor;
half4 _BottomColor;

// Input from compute shader
StructuredBuffer<DrawTriangle> _DrawTriangles;

// Vertex shader
VertexOutput vert(uint vertexID : SV_VertexID) {
    VertexOutput output;

    // Get triangle and vertex data
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    VertexInput vertex = tri.vertices[vertexID % 3];

    // Transform the vertex
    output.positionCS = TransformObjectToHClip(vertex.positionWS);
    output.positionWS = vertex.positionWS.xyz;
    output.normalWS = tri.normal;
    output.uv = vertex.uv;

    return output;
}

// Fragment shader
half4 frag(VertexOutput input) : SV_Target {

    // Get the color from the texture
    half3 color = lerp(_BottomColor.xyz, _TopColor.xyz, input.uv.y);

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

// Returns a matrix that bends the blade given a UV coordinate and bend factor
float3x3 GetBladeBendMatrix(float2 uv, float rigidity, float maxBend) {
    return AngleAxis3x3(pow(uv.y, rigidity) * maxBend, float3(1, 0, 0));
}

#endif // GRASSBLADES_HLSL
