// Make sure file is not included twice
#ifndef COMMONLIBRARY_HLSL
#define COMMONLIBRARY_HLSL

// Outputs a random float between 0 and 1 based on the given seed
float rand(uint seed) {
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
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

// Outputs 3 random barycentric coefficients
float3 UniformRandomBarycentricCoefficients(float seedA, float seedB) {
    float r1 = rand(seedA), r2 = rand(seedB);
    if (r1 + r2 > 1) {
        r1 = 1 - r1;
        r2 = 1 - r2;
    }
    return float3(1 - r1 - r2, r1, r2);
}

#endif // COMMONLIBRARY_HLSL