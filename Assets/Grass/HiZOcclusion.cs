using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class HiZOcclusion : MonoBehaviour {

    [Range(-1, 8)] public int mipLevel = 0;
    public bool displayDepthTexture = false;
    [SerializeField] public GameObject targetObject;

    // Compute Shader
    [SerializeField] private ComputeShader HiZOcclusionShader;

    // Dispatch Size
    private Vector3Int dispatchSize;

    // Kernel IDs
    private int CopyDepthKernelID;
    private int HiZOcclusionKernelID;
    
    // Render Texture for HiZ Occlusion
    private RenderTexture HiZOcclusionTexture;

    void OnEnable() {
        
        // Get Kernel ID and Dispatch Size
        CopyDepthKernelID = HiZOcclusionShader.FindKernel("CopyDepthToHiZMipMap");
        HiZOcclusionKernelID = HiZOcclusionShader.FindKernel("GenerateHiZMipMap");
        HiZOcclusionShader.GetKernelThreadGroupSizes(HiZOcclusionKernelID, out uint x, out uint y, out uint z);
        dispatchSize = new Vector3Int((int)x, (int)y, (int)z);

        // Create Render Texture for HiZ Occlusion
        HiZOcclusionTexture = new RenderTexture(Screen.width, Mathf.CeilToInt(Screen.height * 1.5f), 0, RenderTextureFormat.RFloat);
        HiZOcclusionTexture.enableRandomWrite = true;
        HiZOcclusionTexture.Create();

        // Set HiZ Occlusion Texture to Shader
        Shader.SetGlobalVector("_Dimensions", new Vector4(Camera.main.pixelWidth, Camera.main.pixelHeight, 0, 0));
        Shader.SetGlobalTexture("_HiZBuffer", HiZOcclusionTexture);

        // Enable Depth Texture
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
    }

    // Update is called once per frame
    void Update() {
        if (Shader.GetGlobalTexture("_CameraDepthTexture") == null) { return; }
        
        Shader.SetGlobalMatrix("_viewMatrix", Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);

        // Check if HiZ Texture size matches with Screen size, otherwise recreate it
        if (HiZOcclusionTexture.width != Screen.width || HiZOcclusionTexture.height != Mathf.CeilToInt(Screen.height * 1.5f)) {
            Shader.SetGlobalVector("_Dimensions", new Vector4(Camera.main.pixelWidth, Camera.main.pixelHeight, 0, 0));

            HiZOcclusionTexture.Release();
            HiZOcclusionTexture.width = Screen.width;
            HiZOcclusionTexture.height = Mathf.CeilToInt(Screen.height * 1.5f);
            HiZOcclusionTexture.Create();
        }

        // Copy Depth Buffer to HiZ Occlusion Texture
        HiZOcclusionShader.Dispatch(CopyDepthKernelID, Mathf.CeilToInt(Screen.width * Screen.height / (float)dispatchSize.x), 1, 1);

        // Generate HiZ Occlusion MipMap chain
        for (int i = 1; i < Mathf.Ceil(Mathf.Log(Mathf.Min(Screen.width, Screen.height), 2)); i++) {
            HiZOcclusionShader.SetInt("_MipLevel", i);
            HiZOcclusionShader.Dispatch(HiZOcclusionKernelID, Mathf.CeilToInt((Screen.width >> i) * (Screen.height >> i) / (float)dispatchSize.x), 1, 1);
        }
    }

    // Display HiZ Occlusion Texture on screen
    private void OnGUI() {
        if (!displayDepthTexture) { return; }

        // Draw full screen texture
        if (mipLevel == -1) {
            GUI.DrawTexture(new Rect(0, 0, Camera.main.pixelWidth, Camera.main.pixelHeight), HiZOcclusionTexture);
        
        // Draw specific mip level texture
        } else {
            Texture2D tex = new Texture2D(Camera.main.pixelWidth >> mipLevel, Camera.main.pixelHeight >> mipLevel, TextureFormat.R8, false);
            RenderTexture.active = HiZOcclusionTexture;
            Vector2Int mipOffset = GetMipOffset(mipLevel);
            tex.ReadPixels(new Rect(mipOffset.x, mipOffset.y, tex.width, tex.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            // Draw texture on screen
            GUI.DrawTexture(new Rect(0, 0, Camera.main.pixelWidth, Camera.main.pixelHeight), tex);
        }
    }

    // Get the mip level of the Hi-Z buffer
    int GetMipLevel(float size) {
        return Mathf.FloorToInt(Mathf.Log(size, 2));
    }

    // Get the offset of the mip level
    Vector2Int GetMipOffset(int mipLevel) {
        Vector2Int mipOffset = new Vector2Int(0, 0);
        for (int i = 0; i < mipLevel; i++) {
            mipOffset += new Vector2Int(0, (int)Screen.height >> i) * (1 - (i % 2));
            mipOffset += new Vector2Int((int)Screen.width >> i, 0) * (i % 2);
        }
        return mipOffset;
    }
}
