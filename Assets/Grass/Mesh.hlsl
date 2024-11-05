// Make sure file is not included twice
#ifndef MESH_HLSL
#define MESH_HLSL

// Mesh data (vertex and index buffers used by MeshRenderer)
ByteAddressBuffer _IndexBuffer;
ByteAddressBuffer _VertexBuffer;

// Buffer sizes
uint _IndexCount;
uint _VertexCount;

// Index buffer layout
uint _IndexStride;

// Vertex buffer layout
uint _VertexStride;
uint _PositionOffset;
uint _NormalOffset;
uint _TangentOffset;

// Get the vertex indices of the triangle, taking into account the index buffer stride.
// If the index buffer is 16-bit, the indices are packed and need to be unpacked.
uint3 GetTriangleIndices(uint3 id) {
    int3 v_id;
    int byteIndex = (id.x * 3) * 2;
    if (_IndexStride == 2) {
        uint2 data = asint(_IndexBuffer.Load2(byteIndex & ~3));
        if (byteIndex % (uint)4) {
            v_id.x = (data.x >> 16) & 0xFFFF;
            v_id.y = data.y         & 0xFFFF;
            v_id.z = (data.y >> 16) & 0xFFFF;
        } else {
            v_id.x = data.x         & 0xFFFF;
            v_id.y = (data.x >> 16) & 0xFFFF;
            v_id.z = data.y         & 0xFFFF;
        }
    } else {
        v_id = asint(_IndexBuffer.Load3(id.x * 3 * 4));
    }
    return v_id;
}

// Get the interpolated vertex data for the given triangle and barycentric coordinates.
// The barycentric coordinates are expected to be in the range [0, 1].


#endif // MESH_HLSL