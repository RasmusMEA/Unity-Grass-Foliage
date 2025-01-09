using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class VegetationInstancer : MonoBehaviour {

    // Compute shader.
    [SerializeField] private ComputeShader vegetationInstancer;
    private ComputeShader instantiatedVegetationInstancer;

    [SerializeField] private bool updateEveryFrame = false;

    [Header("Vegetation Layers")]
    [SerializeField] private List<VegetationLayer> vegetationLayers;

    [Header("Grid Settings")]
    [SerializeField] public Vector2Int dimensions = new Vector2Int(1024, 1024);
    [SerializeField] public float scale = 0.5f;

    // Kernel IDs.
    [HideInInspector] public int distributionKernelID;
    [HideInInspector] public int prefixSumKernelID;
    [HideInInspector] public int sortKernelID;

    void OnEnable() {
        
        // Instantiate the compute shader.
        instantiatedVegetationInstancer = Instantiate(vegetationInstancer);

        // Initialize the kernel IDs.
        distributionKernelID = instantiatedVegetationInstancer.FindKernel("DistributeVegetation");
        prefixSumKernelID = instantiatedVegetationInstancer.FindKernel("ComputePrefixSum");
        sortKernelID = instantiatedVegetationInstancer.FindKernel("SortInstances");

        // Get TextureMapsGenerator component, this fails depending on the order of components in the inspector.
        if (GetComponent<TextureMapsGenerator>() != null) {
            GetComponent<TextureMapsGenerator>().SetupShaderVariables(instantiatedVegetationInstancer, distributionKernelID);
            instantiatedVegetationInstancer.EnableKeyword("_USE_TEXTURE_MAPS");
        } else {
            instantiatedVegetationInstancer.DisableKeyword("_USE_TEXTURE_MAPS");
        }

        // Set up the layers.
        foreach (VegetationLayer layer in vegetationLayers) {
            layer.Setup();
        }

        // Distribute the vegetation.
        int coverageChannel = 0;
        foreach (VegetationLayer layer in vegetationLayers) {
            instantiatedVegetationInstancer.SetInt("_CoverageChannel", coverageChannel);
            layer.DistributeVegetation(this, instantiatedVegetationInstancer);
            coverageChannel ^= 1;
        }
    }

    void OnDisable() {

        // Release the buffers.
        foreach (VegetationLayer layer in vegetationLayers) {
            layer.Release();
        }
    }

    // Update is called once per frame
    void Update() {

        if (updateEveryFrame) {
            
            // Set up the layers.
            foreach (VegetationLayer layer in vegetationLayers) {
                layer.Release();
                layer.Setup();
            }

            // Distribute the vegetation.
            int coverageChannel = 0;
            foreach (VegetationLayer layer in vegetationLayers) {
                instantiatedVegetationInstancer.SetInt("_CoverageChannel", coverageChannel);
                layer.DistributeVegetation(this, instantiatedVegetationInstancer);
                coverageChannel ^= 1;
            }
        }

        // Render the instances.
        foreach (VegetationLayer layer in vegetationLayers) {
            layer.RenderVegetation();
        }
    }
}
