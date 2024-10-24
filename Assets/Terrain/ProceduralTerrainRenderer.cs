using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralTerrainRenderer : MonoBehaviour {

    // References
    [Tooltip("The compute shader to use to modify the mesh.")]
    [SerializeField] private ComputeShader terrainComputeShader;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // Instantiated compute shader to modify mesh.
    private ComputeShader instantiatedTerrainComputeShader;
    private Mesh modifiedMesh;

    // Buffers tohat store the mesh data.
    GraphicsBuffer indexBuffer;
    GraphicsBuffer vertexBuffer;

    // Local instance compute shader variables.
    private bool _isInitialized = false;
    private int idVertexDisplacementKernel = 0;         // The kernel to use to displace the vertices.
    private int idNormalsKernel = 0;                    // The kernel to use to calculate the normals.
    private int idNormalizeNormalsKernel = 0;           // The kernel to use to normalize the normals.
    private Vector3Int vertexDisplacementDispatchSize = new Vector3Int(64, 1, 1);
    private Vector3Int calculateNormalsDispatchSize = new Vector3Int(64, 1, 1);
    private Vector3Int normalizeNormalsDispatchSize = new Vector3Int(64, 1, 1);

    void OnEnable() {
        Debug.Assert(terrainComputeShader != null, "Compute shader is not set.", this);

        // Get references.
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Deep copy the mesh from the mesh filter, upload mesh data to the GPU, and set the additional vertex streams.
        modifiedMesh = meshFilter.mesh;
        modifiedMesh.UploadMeshData(true);
        meshRenderer.additionalVertexStreams = modifiedMesh;
        
        // If initialized, call OnDisable to clean up.
        if (_isInitialized) { OnDisable(); }

        // Instantiate the compute shader and cache the kernel.
        instantiatedTerrainComputeShader = Instantiate(terrainComputeShader);
        idVertexDisplacementKernel = instantiatedTerrainComputeShader.FindKernel("DisplaceVertices");
        idNormalsKernel = instantiatedTerrainComputeShader.FindKernel("CalculateNormals");
        idNormalizeNormalsKernel = instantiatedTerrainComputeShader.FindKernel("NormalizeNormals");

        // Get the vertex and index buffers from the mesh.
        modifiedMesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        modifiedMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        indexBuffer = modifiedMesh.GetIndexBuffer();
        vertexBuffer = modifiedMesh.GetVertexBuffer(0);
        
        // Set vertex buffer and other data for the compute shader.
        instantiatedTerrainComputeShader.SetBuffer(idVertexDisplacementKernel, "_VertexBuffer", vertexBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idNormalsKernel, "_IndexBuffer", indexBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idNormalsKernel, "_VertexBuffer", vertexBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idNormalizeNormalsKernel, "_VertexBuffer", vertexBuffer);

        instantiatedTerrainComputeShader.SetInt("_IndexCount", (int)modifiedMesh.GetIndexCount(0));
        instantiatedTerrainComputeShader.SetInt("_VertexCount", modifiedMesh.vertexCount);
        instantiatedTerrainComputeShader.SetInt("_IndexStride", modifiedMesh.indexFormat == IndexFormat.UInt32 ? 4 : 2);
        instantiatedTerrainComputeShader.SetInt("_VertexStride", modifiedMesh.GetVertexBufferStride(0));

        instantiatedTerrainComputeShader.SetInt("_PositionOffset", modifiedMesh.GetVertexAttributeOffset(VertexAttribute.Position));
        instantiatedTerrainComputeShader.SetInt("_NormalOffset", modifiedMesh.GetVertexAttributeOffset(VertexAttribute.Normal));

        // Calculate the dispatch size.
        instantiatedTerrainComputeShader.GetKernelThreadGroupSizes(idVertexDisplacementKernel, out uint threadGroupSizeVertexDisplacement, out _, out _);
        instantiatedTerrainComputeShader.GetKernelThreadGroupSizes(idNormalsKernel, out uint threadGroupSizeNormals, out _, out _);
        instantiatedTerrainComputeShader.GetKernelThreadGroupSizes(idNormalizeNormalsKernel, out uint threadGroupSizeNormalizeNormals, out _, out _);
        vertexDisplacementDispatchSize = new Vector3Int(Mathf.CeilToInt((float)modifiedMesh.vertices.Length / threadGroupSizeVertexDisplacement), 1, 1);
        calculateNormalsDispatchSize = new Vector3Int(Mathf.CeilToInt((float)modifiedMesh.GetIndexCount(0) / threadGroupSizeNormals), 1, 1);
        normalizeNormalsDispatchSize = new Vector3Int(Mathf.CeilToInt((float)modifiedMesh.vertices.Length / threadGroupSizeNormalizeNormals), 1, 1);

        // Set the initialized flag.
        _isInitialized = true;
    }

    void OnDisable() {

        // Dispose of the vertex and index buffers.
        indexBuffer?.Release();
        vertexBuffer?.Release();

        // Destroy the instantiated compute shader and material.
        if (Application.isPlaying) {
            Destroy(instantiatedTerrainComputeShader);
        } else {
            DestroyImmediate(instantiatedTerrainComputeShader);
        }

        // Set the initialized flag.
        _isInitialized = false;
    }

    // LateUpdate is called after all Update functions have been called.
    void LateUpdate() {

        // If in editor mode, reinitialize the renderer to make sure changes are applied.
        if (!Application.isPlaying) {
            OnDisable();
            OnEnable();
        }

        // If not initialized, try to initialize the renderer.
        if (!_isInitialized) { OnEnable(); }
        if (!_isInitialized) { return; }

        // Update compute shader with frame specific data.
        instantiatedTerrainComputeShader.SetVector("_Time", new Vector4(0, Time.timeSinceLevelLoad, 0, 0));
        instantiatedTerrainComputeShader.SetVector("_OffsetWS", transform.position);

        // Dispatch the compute shader.
        instantiatedTerrainComputeShader.Dispatch(idVertexDisplacementKernel, vertexDisplacementDispatchSize.x, vertexDisplacementDispatchSize.y, vertexDisplacementDispatchSize.z);
        instantiatedTerrainComputeShader.Dispatch(idNormalsKernel, calculateNormalsDispatchSize.x, calculateNormalsDispatchSize.y, calculateNormalsDispatchSize.z);
        instantiatedTerrainComputeShader.Dispatch(idNormalizeNormalsKernel, normalizeNormalsDispatchSize.x, normalizeNormalsDispatchSize.y, normalizeNormalsDispatchSize.z);
    }
}
