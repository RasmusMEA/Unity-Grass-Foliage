using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VegetationLayer", menuName = "Vegetation Layer", order = 1)]
public class VegetationLayer : ScriptableObject {

    [Header("Layer Settings")]
    [SerializeField] private int seed;

    [Header("Influence Data Settings")]
    [SerializeField] [Range(2, 100)] private int influenceDataPoints;

    [Header("Jitter Grid Settings")]
    [SerializeField] private float cellSize;

    [Header("Vegetation Settings")]
    [SerializeField] private int maxInstances;
    [SerializeField] private List<VegetationInstance> vegetationInstances;
    
    public struct VegetationIndex {
        public uint startIndex;                     // Start index of the vegetation instance
        public uint instanceCount;                  // Number of instances
        public uint visibleCount;                   // Number of visible instances
    }

    // Input buffers for vegetation types and prefabs.
    ComputeBuffer vegetationTypesBuffer;            // Buffer for vegetation types
    ComputeBuffer vegetationPrefabsBuffer;          // Buffer for vegetation prefabs

    ComputeBuffer prefabBuffer;                  // Buffer for LOD data
    ComputeBuffer vegetationInfluenceWeightsBuffer; // Buffer for influence weights

    // Output buffers for vegetation instances.
    ComputeBuffer instanceCounter;                  // Total number of instances
    ComputeBuffer instanceIndexBuffer;              // (startIndex, instanceCount, visibleCount) for each vegetation LOD type
    GraphicsBuffer indirectDrawIndexedArgs;         // Indirect draw arguments for the vegetation instances
    
    ComputeBuffer instanceBuffer;                   // Vegetation instances to be processed, vegetation type and objectToWorld matrix for each instance
    ComputeBuffer instanceBufferSorted;             // Vegetation instances sorted and culled, objectToWorld matrix for each instance

    // CPU-side buffers.
    private int instanceCount;                      // Total number of instances
    VegetationIndex[] vegetationIndexArray;         // Vegetation index buffer for all prefab lods
    private Matrix4x4[] vegetationPositions;        // Positions of the vegetation instances
    List<Renderer>[] vegetationRenderers;           // Prefabs for each vegetation type

    public void Setup() {

        // Get vegetation types, prefabs, lods and influence weights from the vegetation instances.
        VegetationInstance.VegetationType[] vegetationTypes = new VegetationInstance.VegetationType[vegetationInstances.Count];
        List<VegetationInstance.VegetationPrefab> vegetationPrefabs = new List<VegetationInstance.VegetationPrefab>();
        List<VegetationInstance.PrefabData> prefabs = new List<VegetationInstance.PrefabData>();
        List<GraphicsBuffer.IndirectDrawIndexedArgs> args = new List<GraphicsBuffer.IndirectDrawIndexedArgs>();
        float[] vegetationInfluenceWeights = new float[vegetationInstances.Count * influenceDataPoints * 5];

        int argsCount = 0;
        for (int i = 0; i < vegetationInstances.Count; i++) {
            vegetationInstances[i].GetVegetationType((uint)i, (uint)vegetationPrefabs.Count, ref vegetationTypes);
            vegetationInstances[i].GetVegetationPrefabs((uint)i, (uint)prefabs.Count, ref vegetationPrefabs);
            argsCount = vegetationInstances[i].GetPrefabs(argsCount, ref prefabs, ref args);
            vegetationInstances[i].GetInfluenceData(influenceDataPoints, i * influenceDataPoints * 5, ref vegetationInfluenceWeights);
        }

        // Create vegetation index buffer and vegetation prefabs.
        vegetationIndexArray = new VegetationIndex[prefabs.Count];
        vegetationPositions = new Matrix4x4[maxInstances];
        vegetationRenderers = new List<Renderer>[prefabs.Count];

        uint startIndex = 0;
        for (int i = 0; i < vegetationInstances.Count; i++) {
            startIndex = vegetationInstances[i].GetGameObjects(startIndex, ref vegetationRenderers);
        }

        // Create compute buffers.
        if (vegetationPrefabs.Count > 0) {

            // Create input buffers.
            vegetationTypesBuffer = new ComputeBuffer(vegetationTypes.Length, sizeof(uint) * 3);
            vegetationPrefabsBuffer = new ComputeBuffer(vegetationPrefabs.Count, sizeof(uint) * 3 + sizeof(float) * 12);
            prefabBuffer = new ComputeBuffer(prefabs.Count, sizeof(float) + sizeof(int) * 2);
            vegetationInfluenceWeightsBuffer = new ComputeBuffer(vegetationInfluenceWeights.Length, sizeof(float));

            // Create output buffers.
            instanceCounter = new ComputeBuffer(1, sizeof(int));
            instanceIndexBuffer = new ComputeBuffer(prefabs.Count, sizeof(uint) * 3);
            indirectDrawIndexedArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, args.Count, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            instanceBuffer = new ComputeBuffer(maxInstances, sizeof(int) * 2 + sizeof(float) * 16);
            instanceBufferSorted = new ComputeBuffer(maxInstances, sizeof(float) * 16);

            // Setup input compute buffers.
            vegetationTypesBuffer.SetData(vegetationTypes);
            vegetationPrefabsBuffer.SetData(vegetationPrefabs.ToArray());
            prefabBuffer.SetData(prefabs.ToArray());
            vegetationInfluenceWeightsBuffer.SetData(vegetationInfluenceWeights);

            // Setup indirect draw arguments buffer.
            indirectDrawIndexedArgs.SetData(args.ToArray());
        }
    }

    public void Release() {

        // Release input compute buffers.
        vegetationTypesBuffer?.Release();
        vegetationPrefabsBuffer?.Release();
        prefabBuffer?.Release();
        vegetationInfluenceWeightsBuffer?.Release();

        // Release output compute buffers.
        instanceCounter?.Release();
        instanceIndexBuffer?.Release();
        indirectDrawIndexedArgs?.Release();
        instanceBuffer?.Release();
        instanceBufferSorted?.Release();
    }

    public void DistributeVegetation(VegetationInstancer vegetationInstancer, ComputeShader vegetationInstancerShader) {
        if (vegetationPrefabsBuffer == null) return;

        // Get shader variables from the VegetationInstancer calling this method.
        Vector2Int dimensions = vegetationInstancer.dimensions;
        float scale = vegetationInstancer.scale;

        // Set shader variables.
        vegetationInstancerShader.SetInt("_Seed", seed);
        vegetationInstancerShader.SetInt("_DataPoints", influenceDataPoints);
        vegetationInstancerShader.SetInt("_MaxInstanceCount", maxInstances);
        vegetationInstancerShader.SetInt("_VegetationCount", vegetationInstances.Count);
        vegetationInstancerShader.SetVector("_PositionWS", vegetationInstancer.transform.position);
        vegetationInstancerShader.SetVector("_CellCount", new Vector4(
            Mathf.CeilToInt((float)dimensions.x * scale / cellSize),
            Mathf.CeilToInt((float)dimensions.y * scale / cellSize),
            0,
            0
        ));
        vegetationInstancerShader.SetFloat("_CellSize", cellSize);
        
        // Reset counters.
        instanceBuffer.SetCounterValue(0);
        instanceCounter.SetData(new int[] { 0 });
        instanceIndexBuffer.SetData(new uint[vegetationIndexArray.Length * 3]);

        // Calculate the dispatch size.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.distributionKernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
        Vector3Int dispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)dimensions.x * scale / cellSize / threadGroupSizeX),
            Mathf.CeilToInt((float)dimensions.y * scale / cellSize / threadGroupSizeY),
            1
        );
        
        // Distribute vegetation instances.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_VegetationTypes", vegetationTypesBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_VegetationPrefabs", vegetationPrefabsBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_Prefabs", prefabBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_VegetationInfluenceWeights", vegetationInfluenceWeightsBuffer);

        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InstancePositions", instanceBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InstanceIndex", instanceIndexBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.distributionKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Read back the instance count.
        int[] counter = new int[1];
        instanceCounter.GetData(counter);
        instanceCount = counter[0];
    }

    public void UpdateVegetation(VegetationInstancer vegetationInstancer, ComputeShader vegetationInstancerShader) {
        if (instanceCount == 0) return;

        // Get shader variables from the VegetationInstancer calling this method.
        Vector2Int dimensions = vegetationInstancer.dimensions;
        float scale = vegetationInstancer.scale;

        // Set shader variables.
        vegetationInstancerShader.SetInt("_Seed", seed);
        vegetationInstancerShader.SetInt("_DataPoints", influenceDataPoints);
        vegetationInstancerShader.SetInt("_MaxInstanceCount", maxInstances);
        vegetationInstancerShader.SetInt("_VegetationCount", vegetationInstances.Count);
        vegetationInstancerShader.SetVector("_PositionWS", vegetationInstancer.transform.position);
        vegetationInstancerShader.SetVector("_CellCount", new Vector4(
            Mathf.CeilToInt((float)dimensions.x * scale / cellSize),
            Mathf.CeilToInt((float)dimensions.y * scale / cellSize),
            0,
            0
        ));
        vegetationInstancerShader.SetFloat("_CellSize", cellSize);

        // Calculate the dispatch size for LOD generation.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.LODKernelID, out uint threadGroupSizeX, out _, out _);
        Vector3Int LODDispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)instanceCount / threadGroupSizeX),
            1,
            1
        );

        // Generate LODs for the instances.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_VegetationTypes", vegetationTypesBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_VegetationPrefabs", vegetationPrefabsBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_Prefabs", prefabBuffer);

        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_InstancePositions", instanceBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_InstanceIndex", instanceIndexBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.LODKernelID, LODDispatchSize.x, LODDispatchSize.y, LODDispatchSize.z);

        // Compute the prefix sum.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.prefixSumKernelID, "_Prefabs", prefabBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.prefixSumKernelID, "_InstanceIndex", instanceIndexBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.prefixSumKernelID, "_IndirectDrawIndexedArgs", indirectDrawIndexedArgs);

        vegetationInstancerShader.Dispatch(vegetationInstancer.prefixSumKernelID, 1, 1, 1);

        // Calculate the dispatch size for culling.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.cullKernelID, out threadGroupSizeX, out _, out _);
        Vector3Int cullDispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)instanceCount / threadGroupSizeX),
            1,
            1
        );

        // Cull the instances.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_VegetationTypes", vegetationTypesBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_VegetationPrefabs", vegetationPrefabsBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_Prefabs", prefabBuffer);

        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_InstanceIndex", instanceIndexBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_IndirectDrawIndexedArgs", indirectDrawIndexedArgs);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_Positions", instanceBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_InstancesSorted", instanceBufferSorted);

        vegetationInstancerShader.Dispatch(vegetationInstancer.cullKernelID, cullDispatchSize.x, cullDispatchSize.y, cullDispatchSize.z);

        // Read back index and position data.
        if (!vegetationInstancer.useIndirectInstancing) {
            instanceIndexBuffer.GetData(vegetationIndexArray);
            instanceBufferSorted.GetData(vegetationPositions);
        }
    }

    public void RenderVegetation(VegetationInstancer vegetationInstancer) {

        // Render the vegetation instances.
        int startCommand = 0;
        for (int i = 0; i < vegetationIndexArray.Length; i++) {
            VegetationIndex vi = vegetationIndexArray[i];
            //if (vi.visibleCount == 0 || vegetationRenderers[i] == null) continue;

            // Get the vegetation instance and its renderers.
            foreach (Renderer renderer in vegetationRenderers[i]) {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Material[] materials = renderer.sharedMaterials;

                // Render all submeshes with given material.
                for (int j = 0; j < materials.Length; j++) {
                    Material material = materials[j];
                    material.enableInstancing = true;

                    // Setup RenderParams.
                    RenderParams rp = new RenderParams(material);
                    rp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    rp.worldBounds = new Bounds(
                        vegetationInstancer.transform.position, 
                        new Vector3(
                            vegetationInstancer.dimensions.x * vegetationInstancer.scale, 
                            1000, 
                            vegetationInstancer.dimensions.y * vegetationInstancer.scale
                        )
                    );

                    // Render using indirect draw.
                    if (vegetationInstancer.useIndirectInstancing) {
                        rp.matProps = new MaterialPropertyBlock();
                        rp.matProps.SetBuffer("_ObjectToWorld", instanceBufferSorted);
                        Graphics.RenderMeshIndirect(rp, mesh, indirectDrawIndexedArgs, 1, startCommand++);
                    
                    // Render batches of instances.
                    } else {
                        int batches = Mathf.CeilToInt(vi.visibleCount / 1023f);
                        for (int k = 0; k < batches; k++) {

                            // Calculate the dispatch size and draw the vegetation.
                            uint dispatchSize = (uint)Mathf.Min(1023, vi.visibleCount - k * 1023);
                            uint startIndex = vi.startIndex + (uint)k * 1023;
                            Graphics.RenderMeshInstanced(rp, mesh, j, vegetationPositions, (int)dispatchSize, (int)startIndex);
                        }
                    }
                }
            }
        }
    }
}
