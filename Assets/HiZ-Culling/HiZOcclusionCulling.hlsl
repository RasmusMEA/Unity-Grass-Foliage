// Make sure file is not included twice
#ifndef HIZOCCLUSIONCULLING_HLSL
#define HIZOCCLUSIONCULLING_HLSL

// Built-in macro includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Custom Include
#include "Assets/ShaderLibrary/Common/Rendering.hlsl"

// Camera
float4x4 _viewMatrix;
float4x4 _projectionMatrix;
float4 _ZBufferParams_Custom;

// Depth texture
int2 _CameraDimensions;
TEXTURE2D_X_FLOAT(_CameraDepthTexture);

// Hi-Z buffer
RWTexture2D<float> _HiZBuffer;

// Source: https://discussions.unity.com/t/manually-calculating-linear-depth-in-fragment-function/735637/4
float LinearToDepth(float linearDepth) {
    if (linearDepth * _ZBufferParams_Custom.z < 0.00001) return 0.0;
    return (1.0 - _ZBufferParams_Custom.w * linearDepth) / (linearDepth * _ZBufferParams_Custom.z);
}

// Get the mip level of the Hi-Z buffer
int GetMipLevel(float size) {
    return (int)ceil(log2(max(size, 1)));
}

// Get the mip offset of the Hi-Z buffer
int2 GetMipOffset(int mipLevel) {
    int2 mipOffset = int2(0, 0);
    for (int i = 0; i < mipLevel; i++) {
        mipOffset += (_CameraDimensions >> i) * int2(i & 1, !(i & 1));
    }
    return mipOffset;
}

// Read the 4 texel values nearest to the given UV coordinates at a specific mip level
float4 GetTexels(float2 uv, int mipLevel) {
    int2 mipOffset = GetMipOffset(mipLevel);
    int2 mipMaxBounds = (_CameraDimensions >> mipLevel) - 1;
    
    // Get the 4 texel coordinates and clamp them to the mip level bounds
    float2 center = uv * mipMaxBounds;
    int2 texelCoords[4] = {
        clamp(int2(center + int2(0, 0)), 0, mipMaxBounds),
        clamp(int2(center + int2(1, 0)), 0, mipMaxBounds),
        clamp(int2(center + int2(0, 1)), 0, mipMaxBounds),
        clamp(int2(center + int2(1, 1)), 0, mipMaxBounds)
    };

    // Read the 4 texels from the Hi-Z buffer
    return float4(
        _HiZBuffer[mipOffset + texelCoords[0]],
        _HiZBuffer[mipOffset + texelCoords[1]],
        _HiZBuffer[mipOffset + texelCoords[2]],
        _HiZBuffer[mipOffset + texelCoords[3]]
    );
}

// Checks if a sphere is occluded by the Hi-Z buffer
bool OcclusionCull(float3 positionWS, float radius) {

    // Calculate the bounding sphere center in screen space, remap to [0, 1]
    // float4 positionCS = mul(_projectionMatrix, mul(_viewMatrix, float4(positionWS, 1.0)));

    // Move position towards the camera by the radius to get the depth of the sphere
    float3 Cv = mul(_viewMatrix, float4(positionWS, 1.0)).xyz;
    float3 Pv = Cv - normalize(Cv) * radius;
    float4 positionCS = mul(_projectionMatrix, float4(Pv, 1));
    if (positionCS.w <= 0) return false;
    positionCS.xyz /= positionCS.w;
    positionCS.xy = positionCS.xy * 0.5 + 0.5;

    // Read the depth values from the Hi-Z buffer
    int mipLevel = GetMipLevel(screenSpaceHeight(positionWS, radius) * _CameraDimensions.y);
    float4 texels = GetTexels(positionCS.xy, mipLevel);
    float HiZDepth = min(min(texels.x, texels.y), min(texels.z, texels.w));

    // Check if depth to the sphere is less than the Hi-Z depth
    return (1 - positionCS.z) < HiZDepth;
}

// Checks if a sphere is of less pixel size than target size
bool ScreenSizeCull(float3 positionWS, float radius, float pixelSize) {
    float radiusCS = radius / mul(_viewMatrix, float4(positionWS, 1.0)).w;
    return radiusCS * 2 * _CameraDimensions.y < pixelSize;
}

#endif // HIZOCCLUSIONCULLING_HLSL