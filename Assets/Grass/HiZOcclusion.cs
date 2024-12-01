using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class HiZOcclusion : MonoBehaviour {

    [Range(0, 8)] public int mipLevel = 0;
    public bool displayDepthTexture = false;
    [SerializeField] public GameObject targetObject;

    // Compute Shader
    [SerializeField] private ComputeShader HiZOcclusionShader;

    // Dispatch Size
    private Vector3Int dispatchSize;

    // Kernel IDs
    private int ClearKernelID;
    private int CopyDepthKernelID;
    private int HiZOcclusionKernelID;
    
    // Render Texture for HiZ Occlusion
    private RenderTexture HiZOcclusionTexture;

    void OnEnable() {
        
        // Get Kernel ID and Dispatch Size
        ClearKernelID = HiZOcclusionShader.FindKernel("ClearHiZMipMap");
        CopyDepthKernelID = HiZOcclusionShader.FindKernel("CopyDepthToHiZMipMap");
        HiZOcclusionKernelID = HiZOcclusionShader.FindKernel("GenerateHiZMipMap");
        HiZOcclusionShader.GetKernelThreadGroupSizes(HiZOcclusionKernelID, out uint x, out uint y, out uint z);
        dispatchSize = new Vector3Int((int)x, (int)y, (int)z);

        // Create Render Texture for HiZ Occlusion
        HiZOcclusionTexture = new RenderTexture(Screen.width, Mathf.CeilToInt(Screen.height * 1.5f), 0, RenderTextureFormat.R8);
        HiZOcclusionTexture.enableRandomWrite = true;
        HiZOcclusionTexture.Create();

        // Set HiZ Occlusion Texture to Shader
        Shader.SetGlobalTexture("_HiZBuffer", HiZOcclusionTexture);

        // Enable Depth Texture
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
    }

    // Update is called once per frame
    void Update() {
        if (Shader.GetGlobalTexture("_CameraDepthTexture") == null) { return; }
        
        Shader.SetGlobalMatrix("_viewMatrix", Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);

        // Check if HiZ Texture size matches with Screen size, otherwise recreate it
        if (HiZOcclusionTexture.width != Screen.width || HiZOcclusionTexture.height != Screen.height) {
            HiZOcclusionTexture.Release();
            HiZOcclusionTexture.width = Screen.width;
            HiZOcclusionTexture.height = Mathf.CeilToInt(Screen.height * 1.5f);
            HiZOcclusionTexture.Create();
        }

        // Clear HiZ Occlusion Texture
        HiZOcclusionShader.Dispatch(ClearKernelID, Mathf.CeilToInt(Screen.width * Screen.height / (float)dispatchSize.x), 1, 1);

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
        //GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), HiZOcclusionTexture);

        // Draw part of the texture based on mip level
        Vector2Int mipOffset = GetMipOffset(mipLevel);
        
        // Copy part of the texture to a new texture
        Texture2D tex = new Texture2D(Screen.width >> mipLevel, Screen.height >> mipLevel, TextureFormat.R8, false);
        RenderTexture.active = HiZOcclusionTexture;
        tex.ReadPixels(new Rect(mipOffset.x, mipOffset.y, Screen.width >> mipLevel, Screen.height >> mipLevel), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // Draw the texture
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex);

        // Check if target object is occluded
        if (targetObject != null) {
            if (IsOccluded(targetObject.transform.position, Mathf.Max(targetObject.transform.localScale.x, targetObject.transform.localScale.y, targetObject.transform.localScale.z))) {
                GUI.Label(new Rect(10, 10, 200, 20), "Object is occluded");
            } else {
                GUI.Label(new Rect(10, 10, 200, 20), "Object is visible");
            }
        }
    }

    // Get the mip offset of the Hi-Z buffer
    Vector2Int GetMipOffset(int mipLevel) {
        Vector2Int mipOffset = new Vector2Int(0, 0);
        for (int i = 0; i < mipLevel; i++) {
            mipOffset += new Vector2Int(0, Screen.height >> i) * (1 - (i % 2));
            mipOffset += new Vector2Int(Screen.width >> i, 0) * (i % 2);
        }
        return mipOffset;
    }


    // Get the texel coordinates of the Hi-Z buffer at a specific mip level
    Vector2Int GetTexelCoords(Vector2 uv, int mipLevel) {
        return new Vector2Int((int)(uv.x * Screen.width) >> mipLevel, (int)(uv.y * Screen.height) >> mipLevel);
    }

    // Get the mip level of the Hi-Z buffer
    int GetMipLevel(float size) {
        return Mathf.FloorToInt(Mathf.Log(size, 2));
    }

    // Checks if a sphere is occluded by the Hi-Z buffer
    bool IsOccluded(Vector3 positionWS, float radius) {

        // Get the position of the sphere in screen space
        Vector4 positionCS = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix * new Vector4(positionWS.x, positionWS.y, positionWS.z, 1);
        Vector2 positionSS = new Vector2(positionCS.x / positionCS.w, positionCS.y / positionCS.w);
        positionSS = new Vector2(positionSS.x * 0.5f + 0.5f, positionSS.y * 0.5f + 0.5f);

        // Get the mip level of the sphere
        int mipLevel = GetMipLevel(radius * 2 / positionCS.w);
        mipLevel = 2;

        // Get the texel coordinates of the sphere
        Vector2Int texelCoords = GetTexelCoords(positionSS, mipLevel);
        Vector2Int mipOffset = GetMipOffset(mipLevel);

        // Get the depth of the sphere
        float depth = 1 - positionCS.z / positionCS.w;

        if (mipLevel < 0) { return true; }

        Texture2D tex = new Texture2D(Screen.width >> mipLevel, Screen.height >> mipLevel, TextureFormat.R8, false);

        // Get the depth of the Hi-Z buffer at the texel coordinates
        RenderTexture.active = HiZOcclusionTexture;
        tex.ReadPixels(new Rect(mipOffset.x, mipOffset.y, Screen.width >> mipLevel, Screen.height >> mipLevel), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Vector4 texelDepth = new Vector4(
            tex.GetPixel(mipOffset.x + texelCoords.x, mipOffset.y + texelCoords.y).r,
            tex.GetPixel(mipOffset.x + texelCoords.x + 1, mipOffset.y + texelCoords.y).r,
            tex.GetPixel(mipOffset.x + texelCoords.x, mipOffset.y + texelCoords.y + 1).r,
            tex.GetPixel(mipOffset.x + texelCoords.x + 1, mipOffset.y + texelCoords.y + 1).r
        );

        // Get the depth of the Hi-Z buffer at the sphere
        float hiZDepth = Mathf.Max(texelDepth.x, texelDepth.y, texelDepth.z, texelDepth.w);

        Debug.Log("Depth: " + depth + " HiZ Depth: " + hiZDepth + " at uv: " + positionSS + " mip level: " + mipLevel);

        // Discard the texture
        if (Application.isPlaying) { Destroy(tex); } else { DestroyImmediate(tex); }

        // Check if the sphere is occluded
        return depth < hiZDepth;
    }
}
