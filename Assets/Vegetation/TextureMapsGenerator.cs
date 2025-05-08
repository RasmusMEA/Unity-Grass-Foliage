using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, DefaultExecutionOrder(-1)]
public class TextureMapsGenerator : MonoBehaviour {

    // Compute shader.
    [SerializeField] private ComputeShader textureMapComputeShader;
    [SerializeField] private ComputeShader textureBlurComputeShader;
    private ComputeShader instantiatedTextureMapComputeShader;
    private ComputeShader instantiatedBlurComputeShader;

    // Debug settings.
    [Tooltip("Display the texture maps on the screen.")]
    [SerializeField] private bool debug = false;

    // Texture map properties.
    [Header("Texture map properties")]
    [SerializeField] private Vector2Int dimensions = new Vector2Int(1024, 1024);
    [SerializeField] private float scale = 0.5f;

    // Blur properties
    [Header("Blur settings")]
    [SerializeField] private int blurRadius = 50;
    [SerializeField] private float distanceSigma = 3f;

    // Texture map references.
    private RenderTexture terrainMap;
    private RenderTexture normalMap;
    private RenderTexture coverageMap;

    private RenderTexture blurMap1;
    private RenderTexture blurMap2;

    // Kernel IDs and dispatch sizes.
    private Vector3Int dispatchSize;
    private int terrainMapKernelID;
    private int normalMapKernelID;
    private int moistureMapKernelID;
    private int blurMapKernelID;

    void OnEnable() {

        // Instantiate the compute shader.
        instantiatedTextureMapComputeShader = Instantiate(textureMapComputeShader);
        instantiatedBlurComputeShader = Instantiate(textureBlurComputeShader);

        // Initialize the kernel IDs.
        terrainMapKernelID = instantiatedTextureMapComputeShader.FindKernel("GenerateTerrainMaps");
        normalMapKernelID = instantiatedTextureMapComputeShader.FindKernel("GenerateNormalMap");
        moistureMapKernelID = instantiatedTextureMapComputeShader.FindKernel("GenerateMoistureMap");
        blurMapKernelID = instantiatedBlurComputeShader.FindKernel("BlurTexture");

        // Set Shader Variables
        instantiatedTextureMapComputeShader.SetVector("_PositionWS", transform.position);
        instantiatedTextureMapComputeShader.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        instantiatedTextureMapComputeShader.SetFloat("_Scale", scale);

        // Initialize the texture maps.
        terrainMap = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGBHalf);
        terrainMap.enableRandomWrite = true;
        terrainMap.Create();

        normalMap = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.RGB111110Float);
        normalMap.enableRandomWrite = true;
        normalMap.Create();

        coverageMap = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGB32);
        coverageMap.enableRandomWrite = true;
        coverageMap.Create();

        blurMap1 = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGBHalf);
        blurMap1.enableRandomWrite = true;
        blurMap1.Create();

        blurMap2 = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGBHalf);
        blurMap2.enableRandomWrite = true;
        blurMap2.Create();

        // Set the texture maps.
        instantiatedTextureMapComputeShader.SetTexture(terrainMapKernelID, "_TerrainMap", terrainMap);
        instantiatedTextureMapComputeShader.SetTexture(normalMapKernelID, "_TerrainMap", terrainMap);
        instantiatedTextureMapComputeShader.SetTexture(normalMapKernelID, "_NormalMap", normalMap);
        instantiatedTextureMapComputeShader.SetTexture(moistureMapKernelID, "_TerrainMap", terrainMap);
        instantiatedTextureMapComputeShader.SetTexture(moistureMapKernelID, "_NormalMap", normalMap);

        // Calculate the dispatch size.
        instantiatedTextureMapComputeShader.GetKernelThreadGroupSizes(terrainMapKernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
        dispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)dimensions.x / threadGroupSizeX),
            Mathf.CeilToInt((float)dimensions.y / threadGroupSizeY),
            1
        );

        // Generate terrain and normal maps.
        instantiatedTextureMapComputeShader.Dispatch(terrainMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);
        instantiatedTextureMapComputeShader.Dispatch(normalMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Set the blur shader variables.
        instantiatedBlurComputeShader.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        instantiatedBlurComputeShader.SetFloat("_Scale", scale);

        instantiatedBlurComputeShader.SetInt("_BlurRadius", blurRadius);
        instantiatedBlurComputeShader.SetFloat("_DistanceSigma", distanceSigma);

        // Horizontal blur of the terrain map
        instantiatedBlurComputeShader.SetBool("_HorizontalBlurDirection", true);

        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurSource", terrainMap);
        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurTarget", blurMap1);

        instantiatedBlurComputeShader.Dispatch(blurMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Vertical blur of the terrain map
        instantiatedBlurComputeShader.SetBool("_HorizontalBlurDirection", false);

        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurSource", blurMap1);
        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurTarget", blurMap2);
        
        instantiatedBlurComputeShader.Dispatch(blurMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Generate moisture map
        instantiatedTextureMapComputeShader.SetTexture(moistureMapKernelID, "_BlurredMapView", blurMap2);
        instantiatedTextureMapComputeShader.Dispatch(moistureMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);
    }

    void OnDisable() {
        
        // Release the texture maps.
        terrainMap?.Release();
        normalMap?.Release();
        coverageMap?.Release();
        blurMap1?.Release();
        blurMap2?.Release();
    }

    public void UpdateTextureMaps() {

        // Set Shader Variables
        instantiatedTextureMapComputeShader.SetVector("_PositionWS", transform.position);
        instantiatedTextureMapComputeShader.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        instantiatedTextureMapComputeShader.SetFloat("_Scale", scale);

        // Calculate the dispatch size.
        instantiatedTextureMapComputeShader.GetKernelThreadGroupSizes(terrainMapKernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
        dispatchSize = new Vector3Int(
            Mathf.CeilToInt((float)dimensions.x / threadGroupSizeX),
            Mathf.CeilToInt((float)dimensions.y / threadGroupSizeY),
            1
        );

        // Generate terrain and normal maps.
        instantiatedTextureMapComputeShader.Dispatch(terrainMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);
        instantiatedTextureMapComputeShader.Dispatch(normalMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Set the blur shader variables.
        instantiatedBlurComputeShader.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        instantiatedBlurComputeShader.SetFloat("_Scale", scale);

        instantiatedBlurComputeShader.SetInt("_BlurRadius", blurRadius);
        instantiatedBlurComputeShader.SetFloat("_DistanceSigma", distanceSigma);

        // Horizontal blur of the terrain map
        instantiatedBlurComputeShader.SetBool("_HorizontalBlurDirection", true);

        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurSource", terrainMap);
        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurTarget", blurMap1);

        instantiatedBlurComputeShader.Dispatch(blurMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Vertical blur of the terrain map
        instantiatedBlurComputeShader.SetBool("_HorizontalBlurDirection", false);

        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurSource", blurMap1);
        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurTarget", blurMap2);
        
        instantiatedBlurComputeShader.Dispatch(blurMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Generate moisture map
        instantiatedTextureMapComputeShader.SetTexture(moistureMapKernelID, "_BlurredMapView", blurMap2);
        instantiatedTextureMapComputeShader.Dispatch(moistureMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);
    }

    // Display the texture maps.
    private void OnGUI() {
        if (!debug) { return; }

        GUI.DrawTexture(new Rect(0, 0, 256, 256), terrainMap);
        GUI.DrawTexture(new Rect(256, 0, 256, 256), normalMap);
        GUI.DrawTexture(new Rect(0, 256, 256, 256), blurMap1);
        GUI.DrawTexture(new Rect(256, 256, 256, 256), blurMap2);
    }

    // Enable the shader to access the shader variables.
    public void SetupShaderVariables(ComputeShader shader, int kernelID) {
        shader.SetVector("_PositionWS", transform.position);
        shader.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        shader.SetFloat("_Scale", scale);

        // Set the texture maps.
        shader.SetTexture(kernelID, "_TerrainMap", terrainMap);
        shader.SetTexture(kernelID, "_TerrainMapView", terrainMap);
        shader.SetTexture(kernelID, "_NormalMap", normalMap);
        shader.SetTexture(kernelID, "_NormalMapView", normalMap);
        shader.SetTexture(kernelID, "_CoverageMap", coverageMap);
        shader.SetTexture(kernelID, "_CoverageMapView", coverageMap);
        shader.SetTexture(kernelID, "_BlurredMapView", blurMap2);
    }

    // Enable the material to access the shader variables.
    public void SetupMaterialVariables(Material material) {
        material.SetVector("_PositionWS", transform.position);
        material.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        material.SetFloat("_Scale", scale);

        // Set the texture maps.
        material.SetTexture("_TerrainMap", terrainMap);
        material.SetTexture("_TerrainMapView", terrainMap);
        material.SetTexture("_NormalMap", normalMap);
        material.SetTexture("_NormalMapView", normalMap);
        material.SetTexture("_CoverageMap", coverageMap);
        material.SetTexture("_CoverageMapView", coverageMap);
        material.SetTexture("_BlurredMapView", blurMap2);
    }
}
