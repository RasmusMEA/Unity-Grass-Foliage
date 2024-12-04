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

// Get the mip offset of the Hi-Z buffer
int2 GetMipOffset(int mipLevel) {
    int2 mipOffset = int2(0, 0);
    for (int i = 0; i < mipLevel; i++) {
        mipOffset += (_Dimensions >> i) * int2(i & 1, !(i & 1));
    }
    return mipOffset;
}

// Get the texel coordinates of the Hi-Z buffer at a specific mip level
int2 GetTexelCoords(float2 uv, int mipLevel) {
    return int2(uv * _Dimensions.xy) >> mipLevel;
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
    // int2 texelCoords[4] = {
    //     (int2(center) >> mipLevel) + int2(frac(center) + float2(-0.5, -0.5)),
    //     (int2(center) >> mipLevel) + int2(frac(center) + float2(0.5, -0.5)),
    //     (int2(center) >> mipLevel) + int2(frac(center) + float2(-0.5, 0.5)),
    //     (int2(center) >> mipLevel) + int2(frac(center) + float2(0.5, 0.5))
    // };

    // Read the 4 texels from the Hi-Z buffer
    return float4(
        _HiZBuffer[mipOffset + texelCoords[0]],
        _HiZBuffer[mipOffset + texelCoords[1]],
        _HiZBuffer[mipOffset + texelCoords[2]],
        _HiZBuffer[mipOffset + texelCoords[3]]
    );
}

// Get the mip level of the Hi-Z buffer
int GetMipLevel(float size) {
    size = max(size, 1);
    return (int)log2(size);
}

// Checks if a sphere is occluded by the Hi-Z buffer
bool OcclusionCull(float3 positionWS, float radius) {

    // Calculate the bounding sphere and radius in screen space, remap to [0, 1]
    float4 positionCS = mul(_viewMatrix, float4(positionWS, 1.0));
    float radiusCS = radius / positionCS.w;
    positionCS.xyz /= positionCS.w;
    positionCS.xy = clamp(positionCS.xy, -1, 1) * 0.5 + 0.5;

    // Calculate the mip level, texel coordinates and offset of the Hi-Z buffer
    int mipLevel = GetMipLevel(radiusCS * _Dimensions.y);
    int2 texelCoords = GetTexelCoords(positionCS.xy, mipLevel);
    int2 mipOffset = GetMipOffset(mipLevel);

    // Read the depth values from the Hi-Z buffer
    float4 texels = GetTexels(positionCS.xy, mipLevel);
    // texels.x = _HiZBuffer[texelCoords + mipOffset + int2(0, 0)];
    // texels.y = _HiZBuffer[texelCoords + mipOffset + int2(1, 0)];
    // texels.z = _HiZBuffer[texelCoords + mipOffset + int2(0, 1)];
    // texels.w = _HiZBuffer[texelCoords + mipOffset + int2(1, 1)];
    float HiZDepth = min(min(texels.x, texels.y), min(texels.z, texels.w));

    // Check if depth to the sphere is less than the Hi-Z depth
    return (1 - positionCS.z) < HiZDepth;
}

#endif // HIZOCCLUSIONCULLING_HLSL