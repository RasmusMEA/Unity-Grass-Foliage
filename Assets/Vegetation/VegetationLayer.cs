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
    [SerializeField] [Range(1, 4)] public int LODCount;
    [SerializeField] private List<VegetationInstance> vegetationInstances;

    // Compute buffer for vegetation instances.
    public ComputeBuffer instanceCounter;

    public ComputeBuffer vegetationData;            // Vegetation data for each vegetation type
    public ComputeBuffer influenceDataBuffer;       // Influence data for each vegetation type 
    public ComputeBuffer LODDataBuffer;             // LOD data for each vegetation type
    
    public ComputeBuffer instanceIndexBuffer;       // (<startIndex, instanceCount>, sortedCount, visibleCount) for each vegetation LOD type
    public ComputeBuffer instanceBuffer;            // Vegetation instances to be processed, vegetation type and objectToWorld matrix for each instance
    public ComputeBuffer instanceBufferSorted;      // Vegetation instances sorted and culled, objectToWorld matrix for each instance

    // Instancing arguments.
    private uint[] args;
    private int instanceCount;

    // Positions of the vegetation instances.
    private Matrix4x4[] instancePositions;

    public void Setup() {

        // Create compute buffers.
        instanceCounter = new ComputeBuffer(1, sizeof(int));
        vegetationData = new ComputeBuffer(vegetationInstances.Count, sizeof(float) * 11 + sizeof(int));
        influenceDataBuffer = new ComputeBuffer(vegetationInstances.Count * influenceDataPoints * 5, sizeof(float));
        LODDataBuffer = new ComputeBuffer(vegetationInstances.Count * LODCount, sizeof(float));
        instanceBuffer = new ComputeBuffer(maxInstances, sizeof(int) + sizeof(float) * 16);
        instanceIndexBuffer = new ComputeBuffer(vegetationInstances.Count * LODCount, sizeof(int) * 4);
        instanceBufferSorted = new ComputeBuffer(maxInstances, sizeof(float) * 16);

        // Create instancing arguments.
        args = new uint[vegetationInstances.Count * LODCount * 4];
        instancePositions = new Matrix4x4[maxInstances];

        // Set vegetation data, influence data, and LOD data.
        VegetationInstance.VegetationData[] vegetationDataArray = new VegetationInstance.VegetationData[vegetationInstances.Count];
        float[] influenceData = new float[vegetationInstances.Count * influenceDataPoints * 5];
        float[] LODData = new float[vegetationInstances.Count * LODCount];
        for (int i = 0; i < vegetationInstances.Count; i++) {
            vegetationDataArray[i] = vegetationInstances[i].GetVegetationData();
            vegetationInstances[i].GetInfluenceData(influenceDataPoints, i * influenceDataPoints * 5, ref influenceData);
            vegetationInstances[i].GetLODData(LODCount, i * LODCount, ref LODData);
        }
        vegetationData.SetData(vegetationDataArray);
        influenceDataBuffer.SetData(influenceData);
        LODDataBuffer.SetData(LODData);
    }

    public void Release() {
        vegetationData?.Release();
        influenceDataBuffer?.Release();
        LODDataBuffer?.Release();
        instanceBuffer?.Release();
        instanceIndexBuffer?.Release();
        instanceBufferSorted?.Release();
        instanceCounter?.Release();
    }

    public void DistributeVegetation(VegetationInstancer vegetationInstancer, ComputeShader vegetationInstancerShader) {

        // Get shader variables from the VegetationInstancer calling this method.
        Vector2Int dimensions = vegetationInstancer.dimensions;
        float scale = vegetationInstancer.scale;

        // Set shader variables.
        vegetationInstancerShader.SetInt("_Seed", seed);
        vegetationInstancerShader.SetInt("_LODCount", LODCount);
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
        instanceIndexBuffer.SetData(new int[vegetationInstances.Count * LODCount * 4]);

        // Calculate the dispatch size.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.distributionKernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
        Vector3Int dispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)dimensions.x * scale / cellSize / threadGroupSizeX),
            Mathf.CeilToInt((float)dimensions.y * scale / cellSize / threadGroupSizeY),
            1
        );
        
        // Distribute vegetation instances.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_VegetationData", vegetationData);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InfluenceData", influenceDataBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_LODData", LODDataBuffer);

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

        // Get shader variables from the VegetationInstancer calling this method.
        Vector2Int dimensions = vegetationInstancer.dimensions;
        float scale = vegetationInstancer.scale;

        // Set shader variables.
        vegetationInstancerShader.SetInt("_Seed", seed);
        vegetationInstancerShader.SetInt("_LODCount", LODCount);
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
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_VegetationData", vegetationData);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_LODData", LODDataBuffer);

        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_InstancePositions", instanceBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.LODKernelID, "_InstanceIndex", instanceIndexBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.LODKernelID, LODDispatchSize.x, LODDispatchSize.y, LODDispatchSize.z);

        // Compute the prefix sum.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.prefixSumKernelID, "_InstanceIndex", instanceIndexBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.prefixSumKernelID, 1, 1, 1);

        // Calculate the dispatch size for culling.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.cullKernelID, out threadGroupSizeX, out _, out _);
        Vector3Int cullDispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)instanceCount / threadGroupSizeX),
            1,
            1
        );

        // Cull the instances.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_VegetationData", vegetationData);

        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_InstanceIndex", instanceIndexBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_Positions", instanceBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.cullKernelID, "_InstancesSorted", instanceBufferSorted);

        vegetationInstancerShader.Dispatch(vegetationInstancer.cullKernelID, cullDispatchSize.x, cullDispatchSize.y, cullDispatchSize.z);

        // Read back the instance index buffer. TODO: these should never be read back
        instanceIndexBuffer.GetData(args);
        
        // Read back the instance positions.
        instanceBufferSorted.GetData(instancePositions);
    }

    public void RenderVegetation(VegetationInstancer vegetationInstancer) {

        // Render the vegetation.
        for (int i = 0; i < vegetationInstances.Count; i++) {
            VegetationInstance vegetationInstance = vegetationInstances[i];

            // If the vegetation instance is not a LOD group, render it directly.
            // Otherwise, render each LOD level separately.
            if (vegetationInstance.prefab.GetComponent<LODGroup>() == null) {
                Renderer renderer = vegetationInstance.prefab.GetComponent<Renderer>();
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

                    // Render batches of instances.
                    int batches = Mathf.CeilToInt(args[i * LODCount * 4 + 3] / 1023f);
                    for (int k = 0; k < batches; k++) {

                        // Calculate the dispatch size and draw the vegetation.
                        uint dispatchSize = (uint)Mathf.Min(1023, args[i * LODCount * 4 + 3] - k * 1023);
                        uint startIndex = (uint)args[i * LODCount * 4] + (uint)k * 1023;
                        Graphics.RenderMeshInstanced(rp, mesh, j, instancePositions, (int)dispatchSize, (int)startIndex);
                    }
                }
            } else {
                LOD[] lods = vegetationInstance.prefab.GetComponent<LODGroup>().GetLODs();
                for (int j = 0; j < lods.Length; j++) {
                    Renderer[] renderers = lods[j].renderers;
                    if (renderers == null) continue;
                    for (int k = 0; k < renderers.Length; k++) {
                        Mesh mesh = renderers[k].GetComponent<MeshFilter>().sharedMesh;
                        Material[] materials = renderers[k].sharedMaterials;

                        // Render all submeshes with given material.
                        for (int l = 0; l < materials.Length; l++) {
                            Material material = materials[l];
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

                            // Render batches of instances.
                            int batches = Mathf.CeilToInt(args[(i * LODCount + j) * 4 + 3] / 1023f);
                            for (int m = 0; m < batches; m++) {

                                // Calculate the dispatch size and draw the vegetation.
                                uint dispatchSize = (uint)Mathf.Min(1023, args[(i * LODCount + j) * 4 + 3] - m * 1023);
                                uint startIndex = (uint)args[(i * LODCount + j) * 4] + (uint)m * 1023;
                                Graphics.RenderMeshInstanced(rp, mesh, l, instancePositions, (int)dispatchSize, (int)startIndex);
                            }
                        }
                    }
                }
            }
        }
    }
}
