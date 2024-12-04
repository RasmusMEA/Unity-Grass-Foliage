// Make sure file is not included twice
#ifndef HIZOCCLUSIONCULLING_HLSL
#define HIZOCCLUSIONCULLING_HLSL

// Built-in macro includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Camera
float4x4 _viewMatrix;

// Depth texture
uint2 _Dimensions;
TEXTURE2D_X_FLOAT(_CameraDepthTexture);

// Hi-Z buffer
RWTexture2D<float> _HiZBuffer;

// Get the mip level of the Hi-Z buffer
int GetMipLevel(float size) {
    size = max(size, 1);
    return (int)log2(size);
}

// Get the mip offset of the Hi-Z buffer
int2 GetMipOffset(int mipLevel) {
    int2 mipOffset = int2(0, 0);
    for (int i = 0; i < mipLevel; i++) {
        mipOffset += (_Dimensions >> i) * int2(i & 1, !(i & 1));
    }
    return mipOffset;
}

// Read the 4 texel values nearest to the given UV coordinates at a specific mip level
float4 GetTexels(float2 uv, int mipLevel) {
    int2 mipOffset = GetMipOffset(mipLevel);
    int2 mipMaxBounds = (int2(_Dimensions.xy) >> mipLevel) - 1;
    
    // Get the 4 texel coordinates and clamp them to the mip level bounds
    float2 center = uv * _Dimensions.xy;
    int2 texelCoords[4] = {
        clamp((int2(center) >> mipLevel) + int2(frac(center) + float2(-0.5, -0.5)), 0, mipMaxBounds),
        clamp((int2(center) >> mipLevel) + int2(frac(center) + float2(0.5, -0.5)), 0, mipMaxBounds),
        clamp((int2(center) >> mipLevel) + int2(frac(center) + float2(-0.5, 0.5)), 0, mipMaxBounds),
        clamp((int2(center) >> mipLevel) + int2(frac(center) + float2(0.5, 0.5)), 0, mipMaxBounds)
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

    // Calculate the bounding sphere and radius in screen space, remap to [0, 1]
    float4 positionCS = mul(_viewMatrix, float4(positionWS, 1.0));
    float radiusCS = radius / positionCS.w;
    positionCS.xyz /= positionCS.w;
    positionCS.xy = clamp(positionCS.xy, -1, 1) * 0.5 + 0.5;

    // Read the depth values from the Hi-Z buffer
    float4 texels = GetTexels(positionCS.xy, GetMipLevel(radiusCS * _Dimensions.y));
    float HiZDepth = min(min(texels.x, texels.y), min(texels.z, texels.w));

    // Check if depth to the sphere is less than the Hi-Z depth
    // The 1.1 exponent is used to be more aggressive with culling at distance
    // TODO: Fix culling of similar depth values at distance
    return pow(clamp(1 - positionCS.z, 0, 1), 1.1) < HiZDepth;
}

// Checks if a sphere is of less pixel size than target size
bool ScreenSizeCull(float3 positionWS, float radius, float pixelSize) {
    float radiusCS = radius / mul(_viewMatrix, float4(positionWS, 1.0)).w;
    return radiusCS * 2 * _Dimensions.y < pixelSize;
}

#endif // HIZOCCLUSIONCULLING_HLSL