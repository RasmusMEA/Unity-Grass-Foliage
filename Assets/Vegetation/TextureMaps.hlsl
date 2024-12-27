// Make sure file is not included twice
#ifndef TEXTUREMAPS_HLSL
#define TEXTUREMAPS_HLSL

// Variables for the texture maps
float _Scale;
float2 _Dimensions;
float3 _PositionWS;

// Texture map for the terrain, where each channel represents a different property of the terrain
// R: Height
// G: Water-height
// B: Relative height (Currently unused)
// B: Moisture
RWTexture2D<float4> _TerrainMap;
Texture2D<float4> _TerrainMapView;
SamplerState sampler_TerrainMapView {
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Normal map for the terrain, contains steepness and normal information
// RGB: Normal (XYZ) and Steepness (1 - Z)
RWTexture2D<float3> _NormalMap;
Texture2D<float4> _NormalMapView;
SamplerState sampler_NormalMapView {
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Coverage map for vegetation and other objects
// R: Large vegetation
// G: Medium vegetation
// B: Small vegetation
// A: Objects
RWTexture2D<float4> _CoverageMap;
Texture2D<float4> _CoverageMapView;
SamplerState sampler_CoverageMapView {
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Alias for blurred texture maps
// Should be assigned to manually before using the blurred texture maps
Texture2D<float4> _BlurredMapView;
SamplerState sampler_BlurredMapView {
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 GetUVFromWorldPosition(float3 positionWS) {
    return (positionWS.xz - _PositionWS.xz) / (_Dimensions * _Scale) * 0.5 + 0.5;
}

float4 SampleTextureWorldPosition(Texture2D<float4> source, SamplerState sampleState, float3 positionWS, out bool isValid) {
    float2 uv = GetUVFromWorldPosition(positionWS);
    if (any(saturate(uv) != uv)) {
        isValid = false;
        return float4(0, 0, 0, 0);
    }
    isValid = true;
    return source.SampleLevel(sampleState, uv, 0);
}

#endif // TEXTUREMAPS_HLSL