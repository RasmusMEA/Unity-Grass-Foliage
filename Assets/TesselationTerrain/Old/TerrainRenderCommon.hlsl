
/*******************************************************************************
 * Uniform Data -- Global variables for terrain rendering
 *
 */
// PerFrameVariables 
float4 u_ModelMatrix;
float4 u_ModelViewMatrix;
float4 u_ViewMatrix;
float4 u_CameraMatrix;
float4 u_ViewProjectionMatrix;
float4 u_ModelViewProjectionMatrix;
float4 u_FrustumPlanes[6];

struct Triangle {
    float4 vertices[3];
};

float u_TargetEdgeLength;
float u_LodFactor;

/*******************************************************************************
 * DecodeTriangleVertices -- Decodes the triangle vertices in local space
 *
 */
Triangle DecodeTriangleVertices(in const cbt_Node node) {
    float3 xPos = float3(0, 0, 1), yPos = float3(1, 0, 0);
    float2x3 pos = leb_DecodeAttributeArray(node, float2x3(xPos, yPos));

    Triangle tri;
    tri.vertices[0] = float4(pos[0][0], pos[1][0], 0.0, 1.0);
    tri.vertices[1] = float4(pos[0][1], pos[1][1], 0.0, 1.0);
    tri.vertices[2] = float4(pos[0][2], pos[1][2], 0.0, 1.0);

    return tri;
}

/*******************************************************************************
 * TriangleLevelOfDetail -- Computes the LoD assocaited to a triangle
 *
 * This function is used to garantee a user-specific pixel edge length in
 * screen space. The reference edge length is that of the longest edge of the
 * input triangle.In practice, we compute the LoD as:
 *      LoD = 2 * log2(EdgePixelLength / TargetPixelLength)
 * where the factor 2 is because the number of segments doubles every 2
 * subdivision level.
 */
float TriangleLevelOfDetail_Perspective(in const Triangle patchVertices)
{
    float3 v0 = (u_ModelViewMatrix * patchVertices.vertices[0]).xyz;
    float3 v2 = (u_ModelViewMatrix * patchVertices.vertices[2]).xyz;

    #if 0 //  human-readable version
        float3 edgeCenter = (v0 + v2); // division by 2 was moved to u_LodFactor
        float3 edgeVector = (v2 - v0);
        float distanceToEdgeSqr = dot(edgeCenter, edgeCenter);
        float edgeLengthSqr = dot(edgeVector, edgeVector);

        return u_LodFactor + log2(edgeLengthSqr / distanceToEdgeSqr);
    #else // optimized version
        float sqrMagSum = dot(v0, v0) + dot(v2, v2);
        float twoDotAC = 2.0f * dot(v0, v2);
        float distanceToEdgeSqr = sqrMagSum + twoDotAC;
        float edgeLengthSqr     = sqrMagSum - twoDotAC;

        return u_LodFactor + log2(edgeLengthSqr / distanceToEdgeSqr);
    #endif
}

/*
    In Orthographic Mode, we have
        EdgePixelLength = EdgeViewSpaceLength / ImagePlaneViewSize * ImagePlanePixelResolution
    and so using some identities we get:
        LoD = 2 * (log2(EdgeViewSpaceLength)
            + log2(ImagePlanePixelResolution / ImagePlaneViewSize)
            - log2(TargetPixelLength))

            = log2(EdgeViewSpaceLength^2)
            + 2 * log2(ImagePlanePixelResolution / (ImagePlaneViewSize * TargetPixelLength))
    so we precompute:
    u_LodFactor = 2 * log2(ImagePlanePixelResolution / (ImagePlaneViewSize * TargetPixelLength))
*/
float TriangleLevelOfDetail_Orthographic(in const Triangle patchVertices)
{
    float3 v0 = (u_ModelViewMatrix * patchVertices.vertices[0]).xyz;
    float3 v2 = (u_ModelViewMatrix * patchVertices.vertices[2]).xyz;
    float3 edgeVector = (v2 - v0);
    float edgeLengthSqr = dot(edgeVector, edgeVector);

    return u_LodFactor + log2(edgeLengthSqr);
}

float TriangleLevelOfDetail(in const Triangle patchVertices) {
    // float3 v0 = (u_ModelViewMatrix * patchVertices.vertices[0]).xyz;
    // float3 v2 = (u_ModelViewMatrix * patchVertices.vertices[2]).xyz;
    
    #if defined(PROJECTION_RECTILINEAR)
        return TriangleLevelOfDetail_Perspective(patchVertices);
    #elif defined(PROJECTION_ORTHOGRAPHIC)
        return TriangleLevelOfDetail_Orthographic(patchVertices);
    #elif defined(PROJECTION_FISHEYE)
        return TriangleLevelOfDetail_Perspective(patchVertices);
    #else
        return 0.0;
    #endif
}

#if FLAG_DISPLACE
    /*******************************************************************************
    * DisplacementVarianceTest -- Checks if the height variance criteria is met
    *
    * Terrains tend to have locally flat regions, which don't need large amounts
    * of polygons to be represented faithfully. This function checks the
    * local flatness of the terrain.
    *
    */
    bool DisplacementVarianceTest(in const Triangle patchVertices) {
        #define P0 patchVertices.vertices[0].xy
        #define P1 patchVertices.vertices[1].xy
        #define P2 patchVertices.vertices[2].xy
            float2 P = (P0 + P1 + P2) / 3.0;
            float2 dx = (P0 - P1);
            float2 dy = (P2 - P1);
            float2 dmap = textureGrad(u_DmapSampler, P, dx, dy).rg;
            float dmapVariance = clamp(dmap.y - dmap.x * dmap.x, 0.0, 1.0);

            return (dmapVariance >= u_MinLodVariance);
        #undef P0
        #undef P1
        #undef P2
    }
#endif

/**
 * Negative Vertex of an AABB
 *
 * This procedure computes the negative vertex of an AABB
 * given a normal.
 * See the View Frustum Culling tutorial @ LightHouse3D.com
 * http://www.lighthouse3d.com/tutorials/view-frustum-culling/geometric-approach-testing-boxes-ii/
 */
float3 NegativeVertex(float3 bmin, float3 bmax, float3 n) {
    bool3 b = n > float3(0, 0, 0);
    return lerp(bmin, bmax, b);
}

/**
 * Frustum-AABB Culling Test
 *
 * This procedure returns true if the AABB is either inside, or in
 * intersection with the frustum, and false otherwise.
 * The test is based on the View Frustum Culling tutorial @ LightHouse3D.com
 * http://www.lighthouse3d.com/tutorials/view-frustum-culling/geometric-approach-testing-boxes-ii/
 */
bool FrustumCullingTest(in const float4 planes[6], float3 bmin, float3 bmax) {
    float a = 1.0f;
    for (int i = 0; i < 6 && a >= 0.0f; ++i) {
        float3 n = NegativeVertex(bmin, bmax, planes[i].xyz);
        a = dot(float4(n, 1.0f), planes[i]);
    }
    return (a >= 0.0);
}

/*******************************************************************************
 * FrustumCullingTest -- Checks if the triangle lies inside the view frutsum
 *
 * This function depends on FrustumCulling.glsl
 *
 */
bool FrustumCullingTest(in const Triangle patchVertices) {
    float3 bmin = min(min(patchVertices.vertices[0].xyz, patchVertices.vertices[1].xyz), patchVertices.vertices[2].xyz);
    float3 bmax = max(max(patchVertices.vertices[0].xyz, patchVertices.vertices[1].xyz), patchVertices.vertices[2].xyz);

    return FrustumCullingTest(u_FrustumPlanes, bmin, bmax);
}

/*******************************************************************************
 * LevelOfDetail -- Computes the level of detail of associated to a triangle
 *
 * The first component is the actual LoD value. The second value is 0 if the
 * triangle is culled, and one otherwise.
 *
 */
float2 LevelOfDetail(in const Triangle patchVertices) {
    // culling test
    if (!FrustumCullingTest(patchVertices.vertices))
        #if FLAG_CULL
                return float2(0.0f, 0.0f);
        #else
                return float2(0.0f, 1.0f);
        #endif

    #if FLAG_DISPLACE
        // variance test
        if (!DisplacementVarianceTest(patchVertices.vertices))
            return float2(0.0f, 1.0f);
    #endif

    // compute triangle LOD
    return float2(TriangleLevelOfDetail(patchVertices.vertices), 1.0f);
}


/*******************************************************************************
 * BarycentricInterpolation -- Computes a barycentric interpolation
 *
 */
float2 BarycentricInterpolation(in float2 v[3], in float2 u) {
    return v[1] + u.x * (v[2] - v[1]) + u.y * (v[0] - v[1]);
}

float4 BarycentricInterpolation(in float4 v[3], in float2 u) {
    return v[1] + u.x * (v[2] - v[1]) + u.y * (v[0] - v[1]);
}


/*******************************************************************************
 * GenerateVertex -- Computes the final vertex position
 *
 */
struct VertexAttribute {
    float4 position;
    float2 texCoord;
};

VertexAttribute TessellateTriangle(in const float2 texCoords[3], in float2 tessCoord) {
    float2 texCoord = BarycentricInterpolation(texCoords, tessCoord);
    float4 position = float4(texCoord, 0, 1);

    #if FLAG_DISPLACE
        position.z = u_DmapFactor * textureLod(u_DmapSampler, texCoord, 0.0).r;
    #endif

    VertexAttribute vertex;
    vertex.position = position;
    vertex.texCoord = texCoord;

    return vertex;
}
