// Make sure file is not included twice
#ifndef HIZOCCLUSIONCULLING_HLSL
#define HIZOCCLUSIONCULLING_HLSL

// Built-in macro includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Camera
TEXTURE2D_X_FLOAT(_CameraDepthTexture);
float4x4 _viewMatrix;

// Hi-Z buffer
RWTexture2D<float> _HiZBuffer;

// Get the mip offset of the Hi-Z buffer
int2 GetMipOffset(int mipLevel) {
    int2 mipOffset = int2(0, 0);
    for (int i = 0; i < mipLevel; i++) {
        mipOffset += int2(0, (uint)_ScreenParams.y >> i) * !(i & 1);
        mipOffset += int2((uint)_ScreenParams.x >> i, 0) * (i & 1);
    }
    return mipOffset;
}

// Get the texel coordinates of the Hi-Z buffer at a specific mip level
int2 GetTexelCoords(float2 uv, int mipLevel) {
    return int2(uv * _ScreenParams.xy) >> mipLevel;
}

// Get the mip level of the Hi-Z buffer
int GetMipLevel(float size) {
    return (int)log2(size);
}

// Checks if a sphere is occluded by the Hi-Z buffer
bool IsOccluded(float3 positionWS, float radius) {

    // Calculate the bounding sphere in screen space
    // float4 positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
    // float4 positionCS = mul(mul(unity_CameraProjection, unity_WorldToCamera), float4(positionWS, 1.0));
    float4 positionCS = mul(_viewMatrix, float4(positionWS, 1.0));
    positionCS.xyz /= positionCS.w;

    // Remap the screen space position to [0, 1]
    positionCS.xy = clamp(positionCS.xy, -1, 1) * 0.5 + 0.5;

    // Calculate the mip level of the Hi-Z buffer
    int mipLevel = GetMipLevel(1);

    // Calculate the texel coordinates and offset of the Hi-Z buffer
    int2 texelCoords = GetTexelCoords(positionCS.xy, mipLevel);
    int2 mipOffset = GetMipOffset(mipLevel);

    // Read the depth values from the Hi-Z buffer
    float4 texels;
    texels.x = _HiZBuffer[texelCoords + mipOffset + int2(0, 0)];
    texels.y = _HiZBuffer[texelCoords + mipOffset + int2(1, 0)];
    texels.z = _HiZBuffer[texelCoords + mipOffset + int2(0, 1)];
    texels.w = _HiZBuffer[texelCoords + mipOffset + int2(1, 1)];
    float HiZDepth = max(max(texels.x, texels.y), max(texels.z, texels.w));

    // Check if depth to the sphere is less than the Hi-Z depth
    return (1 - positionCS.z) < HiZDepth;
}

#endif // HIZOCCLUSIONCULLING_HLSL