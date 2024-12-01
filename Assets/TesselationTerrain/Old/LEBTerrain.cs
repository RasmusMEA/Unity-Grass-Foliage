using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LEBTerrain : MonoBehaviour {

    // LEB-CBT compute shader
    [SerializeField] private ComputeShader LEBTerrainComputeShader;
    
    [Range(5, 58)]
    [SerializeField] private int maxDepth;

    // Instantiated compute shader to modify mesh.
    private ComputeShader instantiatedLEBTerrainComputeShader;

    // Local instance compute shader variables.
    private bool isInitialized = false;
    private int idSplit = 0;                    // The kernel used to split the cbt
    private int idMerge = 0;                    // The kernel used to merge the cbt
    private int idUpdateMesh = 0;               // The kernel used to update the mesh

    // CBT buffer
    private GraphicsBuffer instantiatedCBTBuffer;

    // Buffers that store the mesh data.
    private GraphicsBuffer indexBuffer;
    private GraphicsBuffer vertexBuffer;

    // Start is called before the first frame update
    void Start() {

        // Instantiate compute shader
        instantiatedLEBTerrainComputeShader = Instantiate(LEBTerrainComputeShader);

        // Get all kernels
        idSplit = instantiatedLEBTerrainComputeShader.FindKernel("Split");
        idSplit = instantiatedLEBTerrainComputeShader.FindKernel("Merge");
        idSplit = instantiatedLEBTerrainComputeShader.FindKernel("UpdateMesh");
        
        // Generate base triangle to subdivide
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
        mesh.triangles = new int[] { 0, 1, 2 };
        mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0) };

        // Create CBT buffer
        instantiatedCBTBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 12);
        instantiatedCBTBuffer.SetData(new int[] { 0, 0, 0 });

        // Set buffer to compute shader
        instantiatedLEBTerrainComputeShader.SetBuffer(idSplit, "cbt", instantiatedCBTBuffer);
        instantiatedLEBTerrainComputeShader.SetBuffer(idMerge, "cbt", instantiatedCBTBuffer);
        instantiatedLEBTerrainComputeShader.SetBuffer(idUpdateMesh, "cbt", instantiatedCBTBuffer);

        // Get vertex and index buffers from the mesh
        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        indexBuffer = mesh.GetIndexBuffer();
        vertexBuffer = mesh.GetVertexBuffer(0);

        // Upload mesh data to the GPU
        mesh.UploadMeshData(false);
        GetComponent<MeshRenderer>().additionalVertexStreams = mesh;
        GetComponent<MeshFilter>().mesh = mesh;
    }

    // Update is called once per frame
    void Update() {
        
    }
}
