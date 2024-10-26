using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode, DefaultExecutionOrder(+1), RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralGrassRenderer : MonoBehaviour {

    // References
    [Tooltip("The compute shader to use to generate the grass.")]
    [SerializeField] private ComputeShader grassComputeShader;
    [Tooltip("The material to use to render the grass.")]
    [SerializeField] private Material material;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    // Instantiated compute shader and material.
    private ComputeShader instantiatedGrassComputeShader;       // Compute shader to generate the grass.
    private Material instantiatedMaterial;                      // Material to render the grass.

    // Buffers that store the mesh data.
    private GraphicsBuffer indexBuffer;
    private GraphicsBuffer vertexBuffer;

    // Compute buffers to store the grass blades.
    private bool _isInitialized = false;                // Flag to check if the renderer has been initialized.
    private ComputeBuffer drawTrianglesBuffer;          // Buffer to store the generated grass triangles.
    private ComputeBuffer drawArgsBuffer;               // Buffer to store the arguments for the draw command.

    // Local instance compute shader variables.
    private int idGrassKernel;
    private Vector3Int dispatchSize = new Vector3Int(8, 8, 1);

    // The size of a singular element in the compute buffers.
    private const int DRAW_TRIANGLE_STRIDE = sizeof(float) * (3 + 2) * 3 + sizeof(float) * 3;
    private const int DRAW_ARGS_STRIDE = sizeof(int) * 4;

    // The data to reset the drawArgsBuffer with each frame.
    // 0: Vertex count per instance.
    // 1: Instance count.
    // 2: Start vertex location if using a GraphicsBuffer.
    // 3: Start instance location if using a GraphicsBuffer.
    private int[] drawArgsBufferReset = new int[4] { 0, 1, 0, 0 };

    // Grass generation compute shader variables.
    [Header("Grass LOD Settings")]
    //[SerializeField] private bool m_enableLOD = false;
    [SerializeField] private float m_cameraLODNear = 0;
    [SerializeField] private float m_cameraLODFar = 0;
    [SerializeField] private float m_cameraLODFactor = 0;

    [Header("Grass Generation Settings")]
    [SerializeField] [Range(1, 5)] private int m_maxBladeSegments = 1;
    [SerializeField] [Range(1, 100000)] private int m_grassBladesPerTriangle = 1;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassHeight = 1.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassWidth = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassBend = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassSlant = 0.1f;
    [SerializeField] [Range(1.0f, 5.0f)] private float m_grassRigidity = 0.1f;

    [Header("Grass Generation Variation")]
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassHeightVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassWidthVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassBendVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassSlantVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_grassRigidityVariation = 0.1f;

    [Header("Wind Settings")]
    [SerializeField] private Texture2D m_windNoiseTexture;
    [SerializeField] private float m_windStrength = 1.0f;
    [SerializeField] private float m_windTimeMultiplier = 1.0f;
    [SerializeField] private float m_windTextureScale = 1.0f;
    [SerializeField] private float m_windPositionScale = 1.0f;

    void OnEnable() {

        // Get mesh data, if no mesh then return.
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        Mesh mesh = meshFilter.sharedMesh;

        // Check if the mesh, compute shader, and material are set.
        if (mesh == null || grassComputeShader == null || material == null) {
            Debug.Assert(mesh != null, "Mesh is not set.", this);
            Debug.Assert(grassComputeShader != null, "Compute shader is not set.", this);
            Debug.Assert(material != null, "Material is not set.", this);
            return;
        }

        // If initialized, call OnDisable to clean up.
        if (_isInitialized) { OnDisable(); }

        // Instantiate the compute shader and material.
        instantiatedGrassComputeShader = Instantiate(grassComputeShader);
        instantiatedMaterial = Instantiate(material);

        // Cache the compute shader kernel.
        idGrassKernel = instantiatedGrassComputeShader.FindKernel("Main");

        // Calculate the total number of grass triangles.
        int sourceTriangleCount = mesh.triangles.Length / 3;
        int grassBladeTriangleCount = (2 * m_maxBladeSegments - 1);

        // Get the vertex and index buffers from the mesh.
        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        ProceduralTerrainRenderer terrainRenderer = GetComponent<ProceduralTerrainRenderer>();
        indexBuffer = mesh.GetIndexBuffer();
        vertexBuffer = mesh.GetVertexBuffer(0);

        // Set vertex buffer and other data for the compute shader.
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_VertexBuffer", vertexBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_IndexBuffer", indexBuffer);

        instantiatedGrassComputeShader.SetInt("_IndexCount", (int)mesh.GetIndexCount(0));
        instantiatedGrassComputeShader.SetInt("_VertexCount", mesh.vertexCount);
        instantiatedGrassComputeShader.SetInt("_IndexStride", mesh.indexFormat == IndexFormat.UInt32 ? 4 : 2);
        instantiatedGrassComputeShader.SetInt("_VertexStride", mesh.GetVertexBufferStride(0));

        instantiatedGrassComputeShader.SetInt("_PositionOffset", mesh.GetVertexAttributeOffset(VertexAttribute.Position));
        instantiatedGrassComputeShader.SetInt("_NormalOffset", mesh.GetVertexAttributeOffset(VertexAttribute.Normal));
        instantiatedGrassComputeShader.SetInt("_TangentOffset", mesh.GetVertexAttributeOffset(VertexAttribute.Tangent));

        // Initialize the compute shader buffers.
        drawTrianglesBuffer = new ComputeBuffer(sourceTriangleCount * grassBladeTriangleCount * m_grassBladesPerTriangle, DRAW_TRIANGLE_STRIDE, ComputeBufferType.Append);
        drawTrianglesBuffer.SetCounterValue(0);
        drawArgsBuffer = new ComputeBuffer(1, DRAW_ARGS_STRIDE, ComputeBufferType.IndirectArguments);

        // Set compute shader buffers.
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_ProceduralTriangles", drawTrianglesBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_IndirectArgsBuffer", drawArgsBuffer);
        instantiatedGrassComputeShader.SetVectorArray("_CameraFrustumPlanes", new Vector4[6]);

        // Set compute shader textures.
        instantiatedGrassComputeShader.SetTexture(idGrassKernel, "_WindTexture", m_windNoiseTexture != null ? m_windNoiseTexture : Texture2D.blackTexture);

        // Set compute shader variables.
        instantiatedGrassComputeShader.SetVector("_LODSettings", new Vector3(m_cameraLODNear, m_cameraLODFar, m_cameraLODFactor));

        instantiatedGrassComputeShader.SetInt("_SourceTriangleCount", sourceTriangleCount);
        instantiatedGrassComputeShader.SetInt("_MaxBladeSegments", m_maxBladeSegments);
        instantiatedGrassComputeShader.SetInt("_GrassBladesPerTriangle", m_grassBladesPerTriangle);

        instantiatedGrassComputeShader.SetFloat("_GrassHeight", m_grassHeight);
        instantiatedGrassComputeShader.SetFloat("_GrassWidth", m_grassWidth);
        instantiatedGrassComputeShader.SetFloat("_GrassBend", m_grassBend);
        instantiatedGrassComputeShader.SetFloat("_GrassSlant", m_grassSlant);
        instantiatedGrassComputeShader.SetFloat("_GrassRigidity", Mathf.Max(1, m_grassRigidity));

        instantiatedGrassComputeShader.SetFloat("_GrassHeightVariation", m_grassHeightVariation);
        instantiatedGrassComputeShader.SetFloat("_GrassWidthVariation", m_grassWidthVariation);
        instantiatedGrassComputeShader.SetFloat("_GrassBendVariation", m_grassBendVariation);
        instantiatedGrassComputeShader.SetFloat("_GrassSlantVariation", m_grassSlantVariation);
        instantiatedGrassComputeShader.SetFloat("_GrassRigidityVariation", m_grassRigidityVariation);

        instantiatedGrassComputeShader.SetFloat("_WindStrength", m_windStrength);
        instantiatedGrassComputeShader.SetFloat("_WindTimeMultiplier", m_windTimeMultiplier);
        instantiatedGrassComputeShader.SetFloat("_WindTextureScale", m_windTextureScale);
        instantiatedGrassComputeShader.SetFloat("_WindPositionScale", m_windPositionScale);

        // Set the material properties.
        instantiatedMaterial.SetBuffer("_DrawTriangles", drawTrianglesBuffer);

        // Calculate the dispatch size.
        instantiatedGrassComputeShader.GetKernelThreadGroupSizes(idGrassKernel, out uint threadGroupSizeTriangles, out uint threadGroupSizePerTriangle, out _);
        dispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)sourceTriangleCount / threadGroupSizeTriangles),
            Mathf.CeilToInt((float)m_grassBladesPerTriangle / threadGroupSizePerTriangle),
            1
        );

        // Set the initialized flag.
        _isInitialized = true;
    }

    void OnDisable() {

        // Release the compute shader buffers.
        indexBuffer?.Release();
        vertexBuffer?.Release();
        drawTrianglesBuffer?.Release();
        drawArgsBuffer?.Release();

        // Destroy the instantiated compute shader and material.
        if (Application.isPlaying) {
            Destroy(instantiatedGrassComputeShader);
            Destroy(instantiatedMaterial);
        } else {
            DestroyImmediate(instantiatedGrassComputeShader);
            DestroyImmediate(instantiatedMaterial);
        }

        // Set the initialized flag.
        _isInitialized = false;
    }

    // LateUpdate is called after all Update functions have been called.
    void LateUpdate() {
        if (!_isInitialized) { return; }

        // Clear the draw and indirect args buffers from the previous frame.
        drawTrianglesBuffer.SetCounterValue(0);
        drawArgsBuffer.SetData(drawArgsBufferReset);

        // Update compute shader with frame specific data.
        instantiatedGrassComputeShader.SetVector("_Time", new Vector4(0, Time.timeSinceLevelLoad, 0, 0));
        instantiatedGrassComputeShader.SetVector("_CameraPositionWS", Camera.main.transform.position);
        instantiatedGrassComputeShader.SetVector("_CameraDirectionWS", Camera.main.transform.forward);
        instantiatedGrassComputeShader.SetFloat("_CameraFOV", Camera.main.fieldOfView);
        instantiatedGrassComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);

        // Update Camera Frustum Planes.
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        Vector4[] frustumPlanesArray = new Vector4[6];
        for (int i = 0; i < 6; i++) {
            frustumPlanesArray[i] = new Vector4(frustumPlanes[i].normal.x, frustumPlanes[i].normal.y, frustumPlanes[i].normal.z, frustumPlanes[i].distance);
        }
        instantiatedGrassComputeShader.SetVectorArray("_CameraFrustumPlanes", frustumPlanesArray);

        // Dispatch the compute shader.
        instantiatedGrassComputeShader.Dispatch(idGrassKernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Get the bounds of the mesh renderer.
        Bounds bounds = meshRenderer.bounds;
        bounds.Expand(Mathf.Max(m_grassHeight + m_grassHeightVariation, m_grassWidth + m_grassWidthVariation) * 2.0f);

        // DrawProceduralIndirect queues a draw call for our generated mesh.
        Graphics.DrawProceduralIndirect(
            instantiatedMaterial, 
            bounds,
            MeshTopology.Triangles,
            drawArgsBuffer, 
            0, 
            null,
            null,
            ShadowCastingMode.Off, 
            true,
            gameObject.layer
        );

        // TODO: Switch to Graphics.RenderPrimitivesIndexedIndirect with index buffer for better performance.
    }
}
