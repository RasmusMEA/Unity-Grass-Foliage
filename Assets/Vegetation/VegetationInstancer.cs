using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class VegetationInstancer : MonoBehaviour {

    // Compute shader.
    [SerializeField] private ComputeShader vegetationInstancer;
    private ComputeShader instantiatedVegetationInstancer;

    [SerializeField] private bool distributeEveryFrame = false;
    [SerializeField] private bool cullAndLODEveryFrame = false;
    [SerializeField] private bool render = true;

    [Header("Vegetation Layers")]
    [Tooltip("Uses Indirect GPU Instancing (Requires compatible shaders).")]
    [SerializeField] public bool useIndirectInstancing = false;
    [SerializeField] private List<VegetationLayer> vegetationLayers;
    private List<VegetationLayer> instantiatedVegetationLayers = new List<VegetationLayer>();

    [Header("Grid Settings")]
    [SerializeField] public Vector2Int dimensions = new Vector2Int(1024, 1024);
    [SerializeField] public float scale = 0.5f;

    // Kernel IDs.
    [HideInInspector] public int distributionKernelID;
    [HideInInspector] public int LODKernelID;
    [HideInInspector] public int prefixSumKernelID;
    [HideInInspector] public int cullKernelID;

    void OnEnable() {
        
        // Instantiate the compute shader.
        instantiatedVegetationInstancer = Instantiate(vegetationInstancer);

        // Initialize the vegetation layers.
        foreach (VegetationLayer layer in vegetationLayers) {
            instantiatedVegetationLayers.Add(Instantiate(layer));
        }

        // Initialize the kernel IDs.
        distributionKernelID = instantiatedVegetationInstancer.FindKernel("DistributeVegetation");
        LODKernelID = instantiatedVegetationInstancer.FindKernel("GenerateInstanceLODs");
        prefixSumKernelID = instantiatedVegetationInstancer.FindKernel("ComputePrefixSum");
        cullKernelID = instantiatedVegetationInstancer.FindKernel("CullInstances");

        // Get TextureMapsGenerator component, this fails depending on the order of components in the inspector.
        TextureMapsGenerator textureMapsGenerator = GetComponent<TextureMapsGenerator>();
        if (textureMapsGenerator != null) {
            textureMapsGenerator.SetupShaderVariables(instantiatedVegetationInstancer, distributionKernelID);
            instantiatedVegetationInstancer.EnableKeyword("_USE_TEXTURE_MAPS");
        } else {
            instantiatedVegetationInstancer.DisableKeyword("_USE_TEXTURE_MAPS");
        }

        // Set up the layers.
        foreach (VegetationLayer layer in instantiatedVegetationLayers) {
            layer.Setup();
        }

        // Set up the vegetation instancer.
        instantiatedVegetationInstancer.SetVector("_CameraPositionWS", Camera.main.transform.position);
        instantiatedVegetationInstancer.SetVector("_CameraDirectionWS", Camera.main.transform.forward);
        instantiatedVegetationInstancer.SetFloat("_CameraFOV", Camera.main.fieldOfView);

        // Update Camera Frustum Planes.
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        Vector4[] frustumPlanesArray = new Vector4[6];
        for (int i = 0; i < 6; i++) {
            frustumPlanesArray[i] = new Vector4(frustumPlanes[i].normal.x, frustumPlanes[i].normal.y, frustumPlanes[i].normal.z, frustumPlanes[i].distance);
        }
        instantiatedVegetationInstancer.SetVectorArray("_CameraFrustumPlanes", frustumPlanesArray);

        // Distribute the vegetation.
        int coverageChannel = 0;
        foreach (VegetationLayer layer in instantiatedVegetationLayers) {
            instantiatedVegetationInstancer.SetInt("_CoverageChannel", coverageChannel);
            layer.DistributeVegetation(this, instantiatedVegetationInstancer);
            coverageChannel ^= 1;
        }
    }

    void OnDisable() {

        // Release the buffers.
        foreach (VegetationLayer layer in instantiatedVegetationLayers) {
            layer.Release();
            DestroyImmediate(layer);
        }
        instantiatedVegetationLayers.Clear();
    }

    // Update is called once per frame
    void Update() {

        if (distributeEveryFrame) {

            // Regenerate the Texture maps.
            TextureMapsGenerator textureMapsGenerator = GetComponent<TextureMapsGenerator>();
            textureMapsGenerator.UpdateTextureMaps();
            
            // Set up the layers.
            OnDisable();
            OnEnable();

            // Distribute the vegetation.
            int coverageChannel = 0;
            foreach (VegetationLayer layer in instantiatedVegetationLayers) {
                instantiatedVegetationInstancer.SetInt("_CoverageChannel", coverageChannel);
                layer.DistributeVegetation(this, instantiatedVegetationInstancer);
                coverageChannel ^= 1;
            }
        }

        if (cullAndLODEveryFrame) {
            
            // Set up the vegetation instancer.
            instantiatedVegetationInstancer.SetVector("_CameraPositionWS", Camera.main.transform.position);
            instantiatedVegetationInstancer.SetVector("_CameraDirectionWS", Camera.main.transform.forward);
            instantiatedVegetationInstancer.SetFloat("_CameraFOV", Camera.main.fieldOfView);

            // Update Camera Frustum Planes.
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            Vector4[] frustumPlanesArray = new Vector4[6];
            for (int i = 0; i < 6; i++) {
                frustumPlanesArray[i] = new Vector4(frustumPlanes[i].normal.x, frustumPlanes[i].normal.y, frustumPlanes[i].normal.z, frustumPlanes[i].distance);
            }
            instantiatedVegetationInstancer.SetVectorArray("_CameraFrustumPlanes", frustumPlanesArray);

            foreach (VegetationLayer layer in instantiatedVegetationLayers) {
                layer.UpdateVegetation(this, instantiatedVegetationInstancer);
            }
        }

        if (render) {
            
            // Render the instances.
            foreach (VegetationLayer layer in instantiatedVegetationLayers) {
                layer.RenderVegetation(this);
            }
        }
    }
}
