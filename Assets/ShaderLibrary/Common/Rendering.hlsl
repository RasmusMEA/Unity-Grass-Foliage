// Make sure file is not included twice
#ifndef RENDERING_HLSL
#define RENDERING_HLSL

// LOD settings and camera position, x = near, y = far, z = curve factor
float3 _LODSettings;
float3 _CameraPositionWS;
float3 _CameraDirectionWS;
float _CameraFOV;

// Camera frustum culling
float4 _CameraFrustumPlanes[6];

// Calculate the distance from a position to a plane
float DistanceToPlane(float4 plane, float3 position) {
    return dot(plane.xyz, position) + plane.w;
}

// Checks if a position is inside the camera frustum
bool FrustumCull(float3 position, float radius) {
    for (uint i = 0; i < 6; i++) {
        if (DistanceToPlane(_CameraFrustumPlanes[i], position) < -radius) {
            return true;
        }
    }
    return false;
}

#endif // RENDERING_HLSL