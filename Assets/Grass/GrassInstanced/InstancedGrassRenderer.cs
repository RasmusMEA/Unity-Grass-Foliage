using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class InstancedGrassRenderer : MonoBehaviour {

    // References
    [Tooltip("The mesh to used to render the grass on.")]
    [SerializeField] private Mesh mesh;
    [Tooltip("The compute shader to use to generate the grass.")]
    [SerializeField] private ComputeShader grassComputeShader;
    [Tooltip("The material to use to render the grass.")]
    [SerializeField] private Material material;
    private MeshRenderer meshRenderer;

    // Grass mesh to render.
    [SerializeField] private Mesh grassMesh;

    // Instantiated compute shader and material.
    private ComputeShader instantiatedGrassComputeShader;       // Compute shader to generate the grass.
    private Material instantiatedMaterial;                      // Material to render the grass.

    // Compute buffers to generate and store the grass blades.
    private bool _isInitialized = false;                    // Flag to check if the renderer has been initialized.
    private ComputeBuffer sourceVerticesBuffer;             // Buffer to store the source mesh vertices.
    private ComputeBuffer sourceTrianglesBuffer;            // Buffer to store the source mesh triangles.
    private ComputeBuffer sourceNormalsBuffer;              // Buffer to store the source mesh normals.
    private ComputeBuffer sourceTangentsBuffer;             // Buffer to store the source mesh tangents.
    private ComputeBuffer proceduralGrassInstancesBuffer;   // Buffer to store the generated grass triangles.

    // Buffer to store the arguments for the draw command.
    private GraphicsBuffer indirectDrawIndexedArgs;         

    // Local instance compute shader variables.
    private int idGrassKernel;
    private Vector3Int dispatchSize = new Vector3Int(64, 8, 1);

    // The size of a singular element in the compute buffers.
    private const int SOURCE_TRIANGLE_STRIDE = sizeof(int);
    private const int SOURCE_VERTEX_STRIDE = sizeof(float) * 3;
    private const int SOURCE_NORMAL_STRIDE = sizeof(float) * 3;
    private const int SOURCE_TANGENT_STRIDE = sizeof(float) * 4;
    private const int GRASS_INSTANCE_STRIDE = sizeof(float) * 14 + sizeof(int) * 1;

    // The data to reset the indirectDrawIndexedArgs buffer with.
    // https://discussions.unity.com/t/indirectdrawindexedargs-from-compute-shader/925752/2
    // 0: indexCountPerInstance
    // 1: instanceCount
    // 2: startindex
    // 3: baseVertexIndex                                   
    // 4: startInstance
    private uint[] indirectDrawIndexedArgsReset = new uint[5] { 0, 0, 0, 0, 0 };

    // Grass generation compute shader variables.
    [Header("Grass LOD Settings")]
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

        // If initialized, call OnDisable to clean up.
        if (_isInitialized) { OnDisable(); }

        // Get mesh data, if no mesh then return.
        mesh = GetComponent<MeshFilter>().sharedMesh;
        meshRenderer = GetComponent<MeshRenderer>();

        // Check if the mesh, compute shader, and material are set.
        if (mesh == null || grassComputeShader == null || material == null) {
            Debug.Assert(mesh != null, "Mesh is not set.", this);
            Debug.Assert(grassComputeShader != null, "Compute shader is not set.", this);
            Debug.Assert(material != null, "Material is not set.", this);
            return;
        }

        // Create the grass mesh triangle.
        if (grassMesh == null || !Application.isPlaying) {
            grassMesh = new Mesh();
            // grassMesh.vertices = new Vector3[] {
            //     new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(-1, 0, 1/3f),
            //     new Vector3(1, 0, 1/3f), new Vector3(-1, 0, 2/3f), new Vector3(1, 0, 2/3f),
            //     new Vector3(0, 0, 1)
            // };
            // grassMesh.triangles = new int[] { 
            //     0, 1, 2,
            //     1, 3, 2,
            //     2, 3, 4,
            //     3, 5, 4,
            //     4, 5, 6
            // };
            // grassMesh.uv = new Vector2[] { 
            //     new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1/3f),
            //     new Vector2(1, 1/3f), new Vector2(0, 2/3f), new Vector2(1, 2/3f),
            //     new Vector2(0.5f, 1)
            // };

            grassMesh.vertices = new Vector3[] {
                new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1)
            };
            grassMesh.triangles = new int[] { 
                0, 1, 2
            };
            grassMesh.uv = new Vector2[] { 
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1)
            };
        }

        // Set the indirect draw indexed args reset data.
        indirectDrawIndexedArgsReset[0] = mesh.GetIndexCount(0);

        // Instantiate the compute shader and material.
        instantiatedGrassComputeShader = Instantiate(grassComputeShader);
        instantiatedMaterial = Instantiate(material);

        // Calculate the total number of grass triangles.
        int sourceTriangleCount = mesh.triangles.Length / 3;

        // Initialize the compute shader buffers.
        sourceTrianglesBuffer = new ComputeBuffer(mesh.triangles.Length, SOURCE_TRIANGLE_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceTrianglesBuffer.SetData(mesh.triangles);
        sourceVerticesBuffer = new ComputeBuffer(mesh.vertices.Length, SOURCE_VERTEX_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceVerticesBuffer.SetData(mesh.vertices);
        sourceNormalsBuffer = new ComputeBuffer(mesh.normals.Length, SOURCE_NORMAL_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceNormalsBuffer.SetData(mesh.normals);
        sourceTangentsBuffer = new ComputeBuffer(mesh.tangents.Length, SOURCE_TANGENT_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceTangentsBuffer.SetData(mesh.tangents);
        proceduralGrassInstancesBuffer = new ComputeBuffer(sourceTriangleCount * m_grassBladesPerTriangle, GRASS_INSTANCE_STRIDE, ComputeBufferType.Append);
        proceduralGrassInstancesBuffer.SetCounterValue(0);
        indirectDrawIndexedArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(int));
        indirectDrawIndexedArgs.SetData(indirectDrawIndexedArgsReset);

        // Cache the compute shader kernel.
        idGrassKernel = instantiatedGrassComputeShader.FindKernel("Main");

        // Set compute shader buffers.
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_SourceTriangles", sourceTrianglesBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_SourceVertices", sourceVerticesBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_SourceNormals", sourceNormalsBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_SourceTangents", sourceTangentsBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_ProceduralGrassInstances", proceduralGrassInstancesBuffer);
        instantiatedGrassComputeShader.SetBuffer(idGrassKernel, "_IndirectDrawIndexedArgs", indirectDrawIndexedArgs);

        // Set compute shader textures.
        if (m_windNoiseTexture != null)
            instantiatedGrassComputeShader.SetTexture(idGrassKernel, "_WindTexture", m_windNoiseTexture);
        else 
            instantiatedGrassComputeShader.SetTexture(idGrassKernel, "_WindTexture", Texture2D.whiteTexture);

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
        instantiatedMaterial.SetBuffer("_GrassInstances", proceduralGrassInstancesBuffer);

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
        if (_isInitialized) {
            _isInitialized = false;

            // Release the compute shader buffers.
            sourceTrianglesBuffer?.Release();
            sourceVerticesBuffer?.Release();
            sourceNormalsBuffer?.Release();
            sourceTangentsBuffer?.Release();
            proceduralGrassInstancesBuffer?.Release();
            indirectDrawIndexedArgs?.Release();

            // Destroy the instantiated compute shader and material.
            if (Application.isPlaying) {
                Destroy(instantiatedGrassComputeShader);
                Destroy(instantiatedMaterial);
            } else {
                DestroyImmediate(instantiatedGrassComputeShader);
                DestroyImmediate(instantiatedMaterial);
            }
        }
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

        // Clear the draw and indirect args buffers from the previous frame.
        proceduralGrassInstancesBuffer.SetCounterValue(0);
        indirectDrawIndexedArgs.SetData(indirectDrawIndexedArgsReset);

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

        // Set the render parameters.
        RenderParams rp = new RenderParams(instantiatedMaterial);
        rp.shadowCastingMode = ShadowCastingMode.Off;
        //rp.receiveShadows = true;

        // Set the world render bounds, expand the bounds to account for the grass width and height.
        Bounds bounds = meshRenderer.bounds;
        bounds.Expand(Mathf.Max(m_grassHeight + m_grassHeightVariation, m_grassWidth + m_grassWidthVariation) * 2.0f);
        rp.worldBounds = bounds;

        // Draw GPU Instanced cubes.
        Graphics.RenderMeshIndirect(rp, grassMesh, indirectDrawIndexedArgs);
    }
}
