// Make sure file is not included twice
#ifndef GRASS_SHADING_HLSL
#define GRASS_SHADING_HLSL

// Includes
#include "CommonLibrary.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

// Fragment shader input data
struct VertexOutput {
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 positionCS : SV_POSITION;
};

// Input from compute shader
#if GPU_INSTANCING

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
    };

    // Mesh vertex input data
    struct InstancedVertexInput {
        float3 position : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    // Input from compute shader
    StructuredBuffer<GrassInstance> _GrassInstances;

    // Vertex shader
    VertexOutput vert(InstancedVertexInput input, uint svInstanceID : SV_InstanceID) {
        VertexOutput output = (VertexOutput)0;

        // Get instance data
        InitIndirectDrawArgs(0);
        uint cmdID = GetCommandID(0);
        uint instanceID = GetIndirectInstanceID(svInstanceID);
        GrassInstance instance = _GrassInstances[instanceID];

        // Calculate random floats [0, 1] from the instance position
        float r1 = rand(abs(instance.positionWS.x) * 37 + abs(instance.positionWS.y) * 53 + abs(instance.positionWS.z) * 59);
        float r2 = rand(abs(instance.positionWS.x) * 47 + abs(instance.positionWS.y) * 29 + abs(instance.positionWS.z) * 23);

        // Calculate tangent plane from the normal
        float3 tangent = normalize(cross(instance.normalWS, float3(1, 1, 1)));
        float3 bitangent = cross(instance.normalWS, tangent);
        float3x3 TBN = transpose(float3x3(tangent, bitangent, instance.normalWS));

        // Apply local transformations
        float3 vertex = input.position.xyz * float3(instance.width, instance.width, instance.height);
        vertex = mul(AngleAxis3x3(r1 * 2 * PI, float3(0, 0, 1)), vertex);
        vertex = mul(TBN, vertex);

        // Output combined data
        output.positionWS = instance.positionWS + vertex;
        output.normalWS = instance.normalWS;
        output.uv = input.uv;
        output.positionCS = TransformWorldToHClip(output.positionWS);

        return output;
    }

#elif GPU_GENERATION

    // Generated vertex input data
    struct GeneratedVertex {
        float3 positionWS : POSITION;
        float2 uv : TEXCOORD0;
    };

    // Generated triangle data
    struct GeneratedTriangle {
        GeneratedVertex vertices[3];
        float3 normal : NORMAL;
    };

    // Input from compute shader
    StructuredBuffer<GeneratedTriangle> _Triangles;

    // Vertex shader
    VertexOutput vert(uint vertexID : SV_VertexID) {
        VertexOutput output;

        // Get triangle and vertex data
        GeneratedTriangle tri = _Triangles[vertexID / 3];
        GeneratedVertex vertex = tri.vertices[vertexID % 3];

        // Transform the vertex
        output.positionWS = vertex.positionWS.xyz;
        output.normalWS = tri.normal;
        output.uv = vertex.uv;

        #ifdef SHADOW_CASTER_PASS
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(vertex.positionWS, output.normalWS, UNITY_MATRIX_I_V._m02_m12_m22));
            #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
        #else
            output.positionCS = TransformObjectToHClip(vertex.positionWS);
        #endif

        return output;
    }
#endif

// Shader properties
half4 _TopColor;
half4 _BottomColor;

// Fragment shader
half4 frag(VertexOutput input, uint svInstanceID : SV_InstanceID) : SV_Target {
    #ifdef SHADOW_CASTER_PASS
        return 0;
    #endif

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

#endif // GRASS_SHADING_HLSL