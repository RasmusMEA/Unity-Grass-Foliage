// Make sure file is not included twice
#ifndef GRASSBLADES_HLSL
#define GRASSBLADES_HLSL

// Include some helper functions
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
    float2 facingWS;

    // Grass type properties
    int type;
    
    // Grass blade properties
    float height;
    float width;
    float bend;

    // Wind properties, encodes both direction and strength
    float3 windDirection;
};

// Mesh vertex input data
struct VertexInput {
    float3 position : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Vertex output data
struct VertexOutput {
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Input from compute shader
StructuredBuffer<GrassInstance> _GrassInstances;

// Vertex shader
VertexOutput vert(VertexInput input, uint svInstanceID : SV_InstanceID) {
    VertexOutput output = (VertexOutput)0;

    // Get instance data
    InitIndirectDrawArgs(0);
    uint cmdID = GetCommandID(0);
    uint instanceID = GetIndirectInstanceID(svInstanceID);
    GrassInstance instance = _GrassInstances[instanceID];

    // Calculate random floats [0, 1] from the instance position
    float r1 = rand(instance.positionWS.x * 12763 + instance.positionWS.y * 9737333 + instance.positionWS.z * 648391);
    float r2 = rand(instance.positionWS.x * 219613 + instance.positionWS.y * 112129 + instance.positionWS.z * 1128889);

    // Calculate tangent plane from the normal
    float3 tangent = normalize(cross(instance.normalWS, float3(1, 1, 1)));
    float3 bitangent = cross(instance.normalWS, tangent);
    float3x3 TBN = transpose(float3x3(tangent, bitangent, instance.normalWS));

    // Apply local transformations
    float3 vertex = input.position.xyz * float3(instance.width, instance.width, instance.height);
    vertex = mul(AngleAxis3x3(r1 * 2 * PI, float3(0, 0, 1)), vertex);
    vertex = mul(TBN, vertex);

    // Output combined data
    output.uv = input.uv;
    output.positionWS = instance.positionWS + vertex;
    output.normalWS = instance.normalWS;
    output.positionCS = TransformWorldToHClip(output.positionWS);

    return output;
}

half4 _TopColor;
half4 _BottomColor;

// Fragment shader
half4 frag(VertexOutput input, uint svInstanceID : SV_InstanceID) : SV_Target {

    // // Get instance data
    // InitIndirectDrawArgs(0);
    // uint cmdID = GetCommandID(0);
    // uint instanceID = GetIndirectInstanceID(svInstanceID);

    // Sample the texture
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

#endif // GRASSBLADES_HLSL
