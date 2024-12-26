// Make sure file is not included twice
#ifndef TEXTUREMAPS_HLSL
#define TEXTUREMAPS_HLSL

// Global variables for the texture maps
float _Scale;
float2 _Dimensions;
float3 _PositionWS;

// Texture map for the terrain, where each channel represents a different property of the terrain
// R: Height
// G: Water-height
// B: Moisture
// A: Coverage
RWTexture2D<float4> _TerrainMap;

// Normal map for the terrain, contains steepness and normal information
// RGB: Normal (XYZ) and Steepness (1 - Z)
RWTexture2D<float3> _NormalMap;

// Alias for blurred texture maps
// Should be assigned to manually before using the blurred texture maps
RWTexture2D<float4> _BlurredMap;

float GetSteepness(float2 uv) {
    return 1 - _NormalMap[uv * _Dimensions].z;
}

float GetSteepness(int2 texel) {
    return 1 - _NormalMap[texel].z;
}

float2 GetUVFromWorldPosition(float3 positionWS) {
    return (positionWS.xz - _PositionWS.xz) / (_Dimensions * _Scale) * 0.5 + 0.5;
}

float4 SampleTexture(RWTexture2D<float4> source, float2 uv, out bool isValid) {
    isValid = all(saturate(uv) == uv);
    return source[uv * _Dimensions];
}

float4 SampleTextureWorldPosition(RWTexture2D<float4> source, float3 positionWS, out bool isValid) {
    return SampleTexture(source, GetUVFromWorldPosition(positionWS), isValid);
}

#endif // TEXTUREMAPS_HLSL