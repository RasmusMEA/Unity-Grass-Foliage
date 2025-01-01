using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VegetationLayer", menuName = "Vegetation Layer", order = 1)]
public class VegetationLayer : ScriptableObject {

    [Header("Layer Settings")]

    [Header("Influence Data Settings")]
    [SerializeField] private int influenceDataPoints;

    [Header("Jitter Grid Settings")]
    [SerializeField] private int cellSize;

    [Header("Vegetation Settings")]
    [SerializeField] private int maxInstances;
    [SerializeField] private List<VegetationInstance> vegetationInstances;

    // Compute buffer for vegetation instances.
    public ComputeBuffer instanceCounter;

    public ComputeBuffer influenceDataBuffer;      // Influence data for each vegetation type 
    public ComputeBuffer instancePositionsBuffer;  // Vegetation instances to be sorted
    public ComputeBuffer instanceIndexBuffer;      // <instanceCount, startIndex> for each vegetation type
    public ComputeBuffer instanceOffsetBuffer;     // Local offset when sorting for each vegetation type
    public ComputeBuffer instanceDataBuffer;       // Object to world matrix for each instance, sorted by vegetation type

    // Instancing arguments.
    private uint[] args;
    private int instanceCount;

    // Positions of the vegetation instances.
    private Matrix4x4[] instancePositions;

    public void Setup() {

        // Create compute buffers.
        instanceCounter = new ComputeBuffer(1, sizeof(int));
        influenceDataBuffer = new ComputeBuffer(vegetationInstances.Count, sizeof(float) * influenceDataPoints * 5);
        instancePositionsBuffer = new ComputeBuffer(maxInstances, sizeof(int) + sizeof(float) * 16);
        instanceIndexBuffer = new ComputeBuffer(vegetationInstances.Count, sizeof(int) * 2);
        instanceOffsetBuffer = new ComputeBuffer(vegetationInstances.Count, sizeof(int));
        instanceDataBuffer = new ComputeBuffer(maxInstances, sizeof(float) * 16);

        // Create instancing arguments.
        args = new uint[vegetationInstances.Count * 2];
        instancePositions = new Matrix4x4[maxInstances];

        // Set influence data
        float[] influenceData = new float[vegetationInstances.Count * influenceDataPoints * 5];
        for (int i = 0; i < vegetationInstances.Count; i++) {
            for (int j = 0; j < influenceDataPoints; j++) {
                influenceData[i * influenceDataPoints * 5 + j + influenceDataPoints * 0] = vegetationInstances[i].heightInfluence.Evaluate(j / (float)influenceDataPoints);
                influenceData[i * influenceDataPoints * 5 + j + influenceDataPoints * 1] = vegetationInstances[i].waterDepthInfluence.Evaluate(j / (float)influenceDataPoints);
                influenceData[i * influenceDataPoints * 5 + j + influenceDataPoints * 2] = vegetationInstances[i].relativeHeightInfluence.Evaluate(j / (float)influenceDataPoints);
                influenceData[i * influenceDataPoints * 5 + j + influenceDataPoints * 3] = vegetationInstances[i].slopeInfluence.Evaluate(j / (float)influenceDataPoints);
                influenceData[i * influenceDataPoints * 5 + j + influenceDataPoints * 4] = vegetationInstances[i].moistureInfluence.Evaluate(j / (float)influenceDataPoints);
            }
        }
        influenceDataBuffer.SetData(influenceData);
    }

    public void Release() {
        influenceDataBuffer?.Release();
        instancePositionsBuffer?.Release();
        instanceIndexBuffer?.Release();
        instanceOffsetBuffer?.Release();
        instanceDataBuffer?.Release();
    }

    public void DistributeVegetation(VegetationInstancer vegetationInstancer, ComputeShader vegetationInstancerShader) {

        // Get shader variables from the VegetationInstancer calling this method.
        Vector2Int dimensions = vegetationInstancer.dimensions;
        float scale = vegetationInstancer.scale;

        // Calculate the dispatch size.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.distributionKernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
        Vector3Int dispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)dimensions.x * scale / cellSize / threadGroupSizeX),
            Mathf.CeilToInt((float)dimensions.y * scale / cellSize / threadGroupSizeY),
            1
        );

        // Set shader variables.
        vegetationInstancerShader.SetInt("_DataPoints", influenceDataPoints);
        vegetationInstancerShader.SetInt("_MaxInstanceCount", maxInstances);

        // Reset counters.
        instancePositionsBuffer.SetCounterValue(0);
        vegetationInstancerShader.SetInt("_InstanceCount", 0);
        instanceIndexBuffer.SetData(new int[vegetationInstances.Count * 2]);
        instanceCounter.SetData(new int[] { 0 });
        
        // Distribute large vegetation instances.
        vegetationInstancerShader.SetVector("_CellCount", new Vector4(
            Mathf.CeilToInt((float)dimensions.x * scale / cellSize),
            Mathf.CeilToInt((float)dimensions.y * scale / cellSize),
            0,
            0
        ));
        vegetationInstancerShader.SetFloat("_CellSize", cellSize);
        vegetationInstancerShader.SetInt("_VegetationCount", vegetationInstances.Count);

        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InfluenceData", influenceDataBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InstancePositions", instancePositionsBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.distributionKernelID, "_InstanceIndex", instanceIndexBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.distributionKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Compute the prefix sum.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.prefixSumKernelID, "_InstanceIndex", instanceIndexBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.prefixSumKernelID, "_InstanceOffset", instanceOffsetBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.prefixSumKernelID, 1, 1, 1);

        // Read back the instance index buffer.
        instanceIndexBuffer.GetData(args);

        // Read back the instance count.
        int[] counter = new int[1];
        instanceCounter.GetData(counter);
        instanceCount = counter[0];

        // Calculate the dispatch size for sorting.
        vegetationInstancerShader.GetKernelThreadGroupSizes(vegetationInstancer.sortKernelID, out threadGroupSizeX, out _, out _);
        Vector3Int sortDispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)instanceCount / threadGroupSizeX),
            1,
            1
        );

        // Sort the instances.
        vegetationInstancerShader.SetBuffer(vegetationInstancer.sortKernelID, "_InstanceCount", instanceCounter);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.sortKernelID, "_Positions", instancePositionsBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.sortKernelID, "_InstanceIndex", instanceIndexBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.sortKernelID, "_InstanceOffset", instanceOffsetBuffer);
        vegetationInstancerShader.SetBuffer(vegetationInstancer.sortKernelID, "_InstanceData", instanceDataBuffer);

        vegetationInstancerShader.Dispatch(vegetationInstancer.sortKernelID, sortDispatchSize.x, sortDispatchSize.y, sortDispatchSize.z);

        // Read back the instance positions.
        instanceDataBuffer.GetData(instancePositions);
    }

    public void RenderVegetation() {

        // Read back instance counter for debugging.
        int[] instanceCount = new int[1];
        instanceCounter.GetData(instanceCount);
        Debug.Log("Instance count: " + instanceCount[0]);

        // Render the vegetation.
        for (int i = 0; i < vegetationInstances.Count; i++) {
            VegetationInstance vegetationInstance = vegetationInstances[i];

            // Setup RenderParams.
            RenderParams rp = new RenderParams(vegetationInstance.material);
            rp.worldBounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));

            // Render batches of instances.
            uint batches = args[i * 2] / 1023 + 1;
            for (int j = 0; j < batches; j++) {

                // Calculate the dispatch size and draw the vegetation.
                uint dispatchSize = (uint)Mathf.Min(1023, args[i * 2] - j * 1023);
                uint startIndex = (uint)args[i * 2 + 1] + (uint)j * 1023;
                Graphics.RenderMeshInstanced(rp, vegetationInstance.mesh, 0, instancePositions, (int)dispatchSize, (int)startIndex);
            }
        }
    }
}
