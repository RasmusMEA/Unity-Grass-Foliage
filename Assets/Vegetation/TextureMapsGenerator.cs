using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TextureGenerator : MonoBehaviour {

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
    private RenderTexture terrainNormalMap;

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

        // Initialize the texture maps.
        terrainMap = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGBHalf);
        terrainMap.enableRandomWrite = true;
        terrainMap.Create();

        terrainNormalMap = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.RGB111110Float);
        terrainNormalMap.enableRandomWrite = true;
        terrainNormalMap.Create();

        blurMap1 = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGBHalf);
        blurMap1.enableRandomWrite = true;
        blurMap1.Create();

        blurMap2 = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGBHalf);
        blurMap2.enableRandomWrite = true;
        blurMap2.Create();

        // Set Shader Variables
        // instantiatedTextureMapComputeShader.SetVector("_PositionWS", transform.position);
        // instantiatedTextureMapComputeShader.SetVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        // instantiatedTextureMapComputeShader.SetFloat("_Scale", scale);

        Shader.SetGlobalVector("_PositionWS", transform.position);
        Shader.SetGlobalVector("_Dimensions", new Vector4(dimensions.x, dimensions.y, 0, 0));
        Shader.SetGlobalFloat("_Scale", scale);

        // Set the texture maps.
        Shader.SetGlobalTexture("_TerrainMap", terrainMap);
        Shader.SetGlobalTexture("_NormalMap", terrainNormalMap);

        // Initialize the kernel IDs.
        terrainMapKernelID = instantiatedTextureMapComputeShader.FindKernel("GenerateTerrainMaps");
        normalMapKernelID = instantiatedTextureMapComputeShader.FindKernel("GenerateNormalMap");
        moistureMapKernelID = instantiatedTextureMapComputeShader.FindKernel("GenerateMoistureMap");
        blurMapKernelID = instantiatedBlurComputeShader.FindKernel("BlurTexture");

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
        instantiatedBlurComputeShader.SetInt("_HorizontalBlurDirection", 1);

        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurSource", terrainMap);
        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurTarget", blurMap1);

        instantiatedBlurComputeShader.Dispatch(blurMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Vertical blur of the terrain map
        instantiatedBlurComputeShader.SetInt("_HorizontalBlurDirection", 0);

        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurSource", blurMap1);
        instantiatedBlurComputeShader.SetTexture(blurMapKernelID, "_BlurTarget", blurMap2);
        
        instantiatedBlurComputeShader.Dispatch(blurMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);

        // Generate moisture map
        instantiatedTextureMapComputeShader.SetTexture(moistureMapKernelID, "_BlurredMap", blurMap2);
        instantiatedTextureMapComputeShader.Dispatch(moistureMapKernelID, dispatchSize.x, dispatchSize.y, dispatchSize.z);
    }

    void OnDisable() {
        
        // Release the texture maps.
        terrainMap.Release();
        terrainNormalMap.Release();
        blurMap1.Release();
        blurMap2.Release();
    }

    // Update is called once per frame
    void Update() {
        
    }

    // Display the texture maps.
    private void OnGUI() {
        if (!debug) { return; }

        GUI.DrawTexture(new Rect(0, 0, 256, 256), terrainMap);
        GUI.DrawTexture(new Rect(256, 0, 256, 256), terrainNormalMap);
        GUI.DrawTexture(new Rect(0, 256, 256, 256), blurMap1);
        GUI.DrawTexture(new Rect(256, 256, 256, 256), blurMap2);
    }
}
