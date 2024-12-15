// Make sure file is not included twice
#ifndef GRASS_SHADING_HLSL
#define GRASS_SHADING_HLSL

// Includes
#include "Grass.hlsl"
#include "CommonLibrary.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

// Includes to enable indirect draw
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"

// Grass instance data
struct GrassInstance {
    float3 positionWS : POSITION;
    float3 normalWS : NORMAL;
    float2 facing;

    // Grass type properties
    int type;
    
    // Grass blade properties
    float height;
    float width;
    float bend;

    // Wind properties, encodes both direction and strength
    float3 windDirection;

    float hash;
};

// Mesh vertex input data
struct InstancedVertexInput {
    float3 position : POSITION;
    float3 normal : NORMAL;
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

// Input from compute shader
StructuredBuffer<GrassInstance> _GrassInstances;

// Shader properties
half4 _TopColor;
half4 _BottomColor;

// Vertex shader
VertexOutput vert(InstancedVertexInput input, uint svInstanceID : SV_InstanceID) {
    VertexOutput output = (VertexOutput)0;

    // Get instance data
    InitIndirectDrawArgs(0);
    uint cmdID = GetCommandID(0);
    uint instanceID = GetIndirectInstanceID(svInstanceID);
    GrassInstance instance = _GrassInstances[instanceID];

    // Calculate tangent plane from the normal
    float3 tangent = normalize(cross(instance.normalWS, float3(1, 1, 1)));
    float3 bitangent = cross(instance.normalWS, tangent);
    float3x3 TBN = transpose(float3x3(tangent, bitangent, instance.normalWS));

    // Calculate wind direction in world space and bend matrix in tangent space
    float3x3 bendMatrix = GetBladeBendMatrix(input.uv.y, 1, instance.bend);
    float3x3 windMatrix = AngleAxis3x3(1 * instance.windDirection.z, instance.windDirection);
    float3x3 directionMatrix = AngleAxis3x3(instance.hash * 2 * PI, float3(0, 0, 1));
    
    // Apply local transformations
    float3 vertex = input.position.xyz * float3(instance.width, instance.width, instance.height);
    vertex = mul(windMatrix, mul(TBN, mul(directionMatrix, mul(bendMatrix, vertex))));

    // Output combined data
    output.positionWS = instance.positionWS + vertex;
    output.normalWS = instance.normalWS;
    output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
    output.positionCS = TransformWorldToHClip(output.positionWS);

    return output;
}

// Fragment shader
half4 frag(VertexOutput input, uint svInstanceID : SV_InstanceID) : SV_Target {
    
    // Sample the texture
    half3 color = lerp(_BottomColor.xyz, _TopColor.xyz, input.uv.y);
    // half4 texColor = _GrassTexture.Sample(sampler_GrassTexture, input.uv);
    // clip(texColor.a - 0.5);

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

#endif // GRASS_SHADING_HLSL