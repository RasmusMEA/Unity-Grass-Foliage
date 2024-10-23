using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralTerrainRenderer : MonoBehaviour {

    // References
    [Tooltip("The mesh to used to displace")]
    [SerializeField] private Mesh sourceMesh;
    [Tooltip("The compute shader to use to modify the mesh.")]
    [SerializeField] private ComputeShader terrainComputeShader;
    [Tooltip("The material to use to render the terrain.")]
    [SerializeField] private Material material;
    private MeshRenderer meshRenderer;

    // Instantiated compute shader and material.
    private ComputeShader instantiatedTerrainComputeShader;     // Compute shader to modify mesh.

    // Compute buffers to store the modified mesh data.
    private bool _isInitialized = false;                // Flag to check if the renderer has been initialized.
    private ComputeBuffer sourceVerticesBuffer;         // Buffer to store the source mesh vertices.
    private ComputeBuffer sourceTrianglesBuffer;        // Buffer to store the source mesh triangles.
    private ComputeBuffer sourceNormalsBuffer;          // Buffer to store the source mesh normals.
    private ComputeBuffer sourceUVsBuffer;              // Buffer to store the source mesh UVs.
    private ComputeBuffer drawArgsBuffer;               // Buffer to store the draw arguments.

    // Local instance compute shader variables.
    private int idVertexDisplacementKernel = 0;         // The kernel to use to displace the vertices.
    private int idNormalsKernel = 0;                    // The kernel to use to calculate the normals.
    private Vector3Int vertexDisplacementDispatchSize = new Vector3Int(64, 1, 1);
    private Vector3Int calculateNormalsDispatchSize = new Vector3Int(64, 1, 1);

    // The size of a singular element in the compute buffers.
    private const int SOURCE_TRIANGLE_STRIDE = sizeof(int);
    private const int SOURCE_VERTEX_STRIDE = sizeof(float) * 3;
    private const int SOURCE_NORMAL_STRIDE = sizeof(float) * 3;
    private const int SOURCE_UV_STRIDE = sizeof(float) * 2;
    private const int DRAW_ARGS_STRIDE = sizeof(int) * 4;

    void OnEnable() {

        // Get mesh data, if no mesh then return.
        sourceMesh = GetComponent<MeshFilter>().sharedMesh;
        meshRenderer = GetComponent<MeshRenderer>();

        // Check if the mesh, compute shader, and material are set.
        if (sourceMesh == null || terrainComputeShader == null || material == null) {
            Debug.Assert(sourceMesh != null, "Mesh is not set.", this);
            Debug.Assert(terrainComputeShader != null, "Compute shader is not set.", this);
            Debug.Assert(material != null, "Material is not set.", this);
            return;
        }

        // Set material on mesh renderer.
        meshRenderer.sharedMaterial = material;

        // If initialized, call OnDisable to clean up.
        if (_isInitialized) { OnDisable(); }

        // Instantiate the compute shader and material.
        instantiatedTerrainComputeShader = Instantiate(terrainComputeShader);

        // Initialize the compute shader buffers.
        sourceTrianglesBuffer = new ComputeBuffer(sourceMesh.triangles.Length, SOURCE_TRIANGLE_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceTrianglesBuffer.SetData(sourceMesh.triangles);
        sourceVerticesBuffer = new ComputeBuffer(sourceMesh.vertices.Length, SOURCE_VERTEX_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceVerticesBuffer.SetData(sourceMesh.vertices);
        sourceNormalsBuffer = new ComputeBuffer(sourceMesh.normals.Length, SOURCE_NORMAL_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceNormalsBuffer.SetData(sourceMesh.normals);
        sourceUVsBuffer = new ComputeBuffer(sourceMesh.uv.Length, SOURCE_UV_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceUVsBuffer.SetData(sourceMesh.uv);
        drawArgsBuffer = new ComputeBuffer(1, DRAW_ARGS_STRIDE, ComputeBufferType.IndirectArguments);
        drawArgsBuffer.SetData(new uint[4] { sourceMesh.GetIndexCount(0), 1, 0, 0 });

        // Cache the compute shader kernel.
        idVertexDisplacementKernel = instantiatedTerrainComputeShader.FindKernel("DisplaceVertices");
        idNormalsKernel = instantiatedTerrainComputeShader.FindKernel("CalculateNormals");

        // Set compute shader buffers.
        instantiatedTerrainComputeShader.SetBuffer(idVertexDisplacementKernel, "_SourceTriangles", sourceTrianglesBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idVertexDisplacementKernel, "_SourceVertices", sourceVerticesBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idVertexDisplacementKernel, "_SourceNormals", sourceNormalsBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idNormalsKernel, "_SourceTriangles", sourceTrianglesBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idNormalsKernel, "_SourceVertices", sourceVerticesBuffer);
        instantiatedTerrainComputeShader.SetBuffer(idNormalsKernel, "_SourceNormals", sourceNormalsBuffer);

        // Set the material properties.
        meshRenderer.sharedMaterial.SetBuffer("_Triangles", sourceTrianglesBuffer);
        meshRenderer.sharedMaterial.SetBuffer("_Vertices", sourceVerticesBuffer);
        meshRenderer.sharedMaterial.SetBuffer("_Normals", sourceNormalsBuffer);
        meshRenderer.sharedMaterial.SetBuffer("_UVs", sourceUVsBuffer);

        // Calculate the dispatch size.
        instantiatedTerrainComputeShader.GetKernelThreadGroupSizes(idVertexDisplacementKernel, out uint threadGroupSizeVertexDisplacement, out _, out _);
        instantiatedTerrainComputeShader.GetKernelThreadGroupSizes(idNormalsKernel, out uint threadGroupSizeNormals, out _, out _);
        vertexDisplacementDispatchSize = new Vector3Int(Mathf.CeilToInt((float)sourceMesh.vertices.Length / threadGroupSizeVertexDisplacement), 1, 1);
        calculateNormalsDispatchSize = new Vector3Int(Mathf.CeilToInt((float)(sourceMesh.triangles.Length / 3) / threadGroupSizeNormals), 1, 1);

        // Set the initialized flag.
        _isInitialized = true;
    }

    void OnDisable() {

        // Release the compute shader buffers.
        sourceTrianglesBuffer?.Release();
        sourceVerticesBuffer?.Release();
        sourceNormalsBuffer?.Release();
        sourceUVsBuffer?.Release();
        drawArgsBuffer?.Release();

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
        instantiatedTerrainComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);

        // Dispatch the compute shader.
        instantiatedTerrainComputeShader.Dispatch(idVertexDisplacementKernel, vertexDisplacementDispatchSize.x, vertexDisplacementDispatchSize.y, vertexDisplacementDispatchSize.z);
        instantiatedTerrainComputeShader.Dispatch(idNormalsKernel, calculateNormalsDispatchSize.x, calculateNormalsDispatchSize.y, calculateNormalsDispatchSize.z);

        // // Draw bounds for debugging.
        Bounds bounds = meshRenderer.bounds;
        bounds.Expand(1000);

        // DrawProceduralIndirect queues a draw call for our generated mesh.
        Graphics.DrawProceduralIndirect(
            meshRenderer.sharedMaterial, 
            bounds,
            MeshTopology.Triangles,
            drawArgsBuffer,
            0, 
            null,
            null,
            ShadowCastingMode.On, 
            true,
            gameObject.layer
        );

        // Hide the source mesh.
        meshRenderer.enabled = false;
    }
}
