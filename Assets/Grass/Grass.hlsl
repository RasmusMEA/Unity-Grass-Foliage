// Make sure file is not included twice
#ifndef GRASS_HLSL
#define GRASS_HLSL

// Built-in macro includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Custom includes
#include "Rendering.hlsl"
#include "CommonLibrary.hlsl"

// Define the maximum number of blade segments that can be generated
#define MAX_BLADE_SEGMENTS 5

// Define the maximum number of blade points based on maximum blade segments.
#define MAX_BLADE_POINTS (2 * MAX_BLADE_SEGMENTS + 1)

// Grass settings
uint _MaxBladeSegments;
uint _GrassBladesPerTriangle;

float _GrassHeight;
float _GrassWidth;
float _GrassBend;
float _GrassSlant;
float _GrassRigidity;

float _GrassHeightVariation;
float _GrassWidthVariation;
float _GrassBendVariation;
float _GrassSlantVariation;
float _GrassRigidityVariation;

// Wind settings
Texture2D _WindTexture;
SamplerState sampler_WindTexture;
float _WindStrength;
float _WindTimeMultiplier;
float _WindTextureScale;
float _WindPositionScale;

// Calculates the LOD based on the camera position
float GetBladeSegments(float3 positionWS, float height) {
    
    // Calculate the LOD based on the distance and apply curve factor
    float d = distance(positionWS, _CameraPositionWS);
    d = 1 - smoothstep(_LODSettings.x, _LODSettings.y, d);
    d = pow(abs(d), _LODSettings.z);

    // Clamp the blade segments between 1 and the maximum
    return max(1, min(MAX_BLADE_SEGMENTS, ceil(d * _MaxBladeSegments)));
}

// Returns the wind direction and strength at a given position
float3 GetWindDirection(float3 positionWS, float3 normalWS) {
    
    // Get the wind UV coordinate and sample the wind noise texture red and green channels where 128 is neutral
    float2 windUV = (positionWS.xz * _WindPositionScale + _Time.y * _WindTimeMultiplier) * _WindTextureScale;
    float2 windNoise = _WindTexture.SampleLevel(sampler_WindTexture, windUV, 0).rg * 2 - 1;
    //float2 windNoise = SAMPLE_TEXTURE2D_LOD(_WindTexture, sampler_WindTexture, windUV, 0).rg * 2 - 1;

    // Get the wind direction and strength perpendicularly to the blade
    return cross(normalWS, float3(windNoise.x, 0, windNoise.y));
}

// Returns a matrix that bends the blade given a UV coordinate and bend factor
float3x3 GetBladeBendMatrix(float2 uv, float rigidity, float maxBend) {
    return AngleAxis3x3(pow(uv.y, rigidity) * maxBend, float3(1, 0, 0));
}

// Checks whether to cull the grass blade based on the frustum, occlusion and size-distance LOD
bool CullGrassBlade(float3 position, float radius, float height, float3 normal) {
    bool cull = false;

    // Frustum cull and occlusion cull
    cull = cull | FrustumCull(position, radius);

    // Parameter culling
    //cull = cull | position.y < 0 || dot(normal, float3(0, 1, 0)) < 0.87;

    // Size-distance LOD culling
    cull = cull | (height < 2 * distance(position, _CameraPositionWS) * tan(0.5 * _CameraFOV * PI / 180) * 0.001);

    return cull;
}

#endif // GRASS_HLSL