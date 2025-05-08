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

// Calculate the relative height of an object on the screen, returns a factor between 0 and 1
float screenRelativeHeight(float3 positionWS, float radius) {
    
    // Calculate the distance from the camera to the object
    float3 cameraToObject = positionWS - _CameraPositionWS;
    float distance = length(cameraToObject);

    // Calculate the screen size of the object based on its distance and radius
    float screenSize = (radius * 2) / distance * _CameraFOV * _LODSettings.x;
    return screenSize;
}

#endif // RENDERING_HLSL