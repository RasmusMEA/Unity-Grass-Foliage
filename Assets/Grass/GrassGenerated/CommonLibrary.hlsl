// Make sure file is not included twice
#ifndef COMMONLIBRARY_HLSL
#define COMMONLIBRARY_HLSL

// Returns a pseudorandom number. By Ronja Böhringer
float rand(float4 value) {
    float4 smallValue = sin(value);
    float random = dot(smallValue, float4(12.9898, 78.233, 37.719, 9.151));
    random = frac(sin(random) * 143758.5453);
    return random;
}

float rand(uint seed) {
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
    return ((float)seed) / float(0xffffffff);
}

// Xorshift algorithm from George Marsaglia's paper
float rand_xorshift(uint seed) {
    seed ^= (seed << 13);
    seed ^= (seed >> 17);
    seed ^= (seed << 5);
    return ((float)seed) / float(0xffffffff);
}

// A function to compute an rotation matrix which rotates a point
// by angle radians around the given axis
// By Keijiro Takahashi
float3x3 AngleAxis3x3(float angle, float3 axis) {
    float c, s;
    sincos(angle, s, c);

    float t = 1 - c;
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;

    return float3x3(
        t * x * x + c, t * x * y - s * z, t * x * z + s * y,
        t * x * y + s * z, t * y * y + c, t * y * z - s * x,
        t * x * z - s * y, t * y * z + s * x, t * z * z + c
    );
}

// https://math.stackexchange.com/questions/18686/uniform-random-point-in-triangle-in-3d
float3 UniformRandomBarycentricCoefficients(float seedA, float seedB) {
    float r1 = rand_xorshift(seedA), r2 = rand_xorshift(seedB);
    if (r1 + r2 > 1) {
        r1 = 1 - r1;
        r2 = 1 - r2;
    }
    return float3(1 - r1 - r2, r1, r2);
}

// https://math.stackexchange.com/questions/18686/uniform-random-point-in-triangle-in-3d
float3 UniformRandomBarycentricPosition(float3 a, float3 b, float3 c, float seedA, float seedB) {
    float3 coefficients = UniformRandomBarycentricCoefficients(seedA, seedB);
    return a * coefficients.x + b * coefficients.y + c * coefficients.z;
}

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

#endif // COMMONLIBRARY_HLSL