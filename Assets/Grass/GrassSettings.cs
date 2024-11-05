using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GrassRenderingMethod {
    Procedural,
    GPUInstancing
}

public enum GrassVisuals {
    GrassMesh,
    GrassBillboard
}

[CreateAssetMenu(fileName = "GrassSettings", menuName = "Grass/Grass Settings", order = 1)]
public class GrassSettings : ScriptableObject {
    
    // Grass generation settings.
    [Header("Grass Rendering Method")]
    [SerializeField] public GrassRenderingMethod grassRenderingMethod = GrassRenderingMethod.Procedural;

    // Grass visuals settings.
    [Header("Grass Visual Settings")]
    [SerializeField] public GrassVisuals grassVisuals = GrassVisuals.GrassMesh;
    [SerializeField] public Mesh grassBladeMesh;
    [SerializeField] public Texture2D grassBladeTexture;

    [Header("Grass LOD Settings")]
    //[SerializeField] public bool m_enableLOD = false;
    [SerializeField] public float cameraLODNear = 0;
    [SerializeField] public float cameraLODFar = 0;
    [SerializeField] public float cameraLODFactor = 0;

    [Header("Grass Generation Settings")]
    [SerializeField] [Range(1, 5)] public int maxBladeSegments = 1;
    [SerializeField] [Range(1, 100000)] public int grassBladesPerTriangle = 1;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassHeight = 1.0f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassWidth = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassBend = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassSlant = 0.1f;
    [SerializeField] [Range(1.0f, 5.0f)] public float grassRigidity = 0.1f;

    [Header("Grass Generation Variation")]
    [SerializeField] [Range(0.0f, 1.0f)] public float grassHeightVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassWidthVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassBendVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassSlantVariation = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] public float grassRigidityVariation = 0.1f;

    [Header("Wind Settings")]
    [SerializeField] public Texture2D windNoiseTexture;
    [SerializeField] public float windStrength = 1.0f;
    [SerializeField] public float windTimeMultiplier = 1.0f;
    [SerializeField] public float windTextureScale = 1.0f;
    [SerializeField] public float windPositionScale = 1.0f;
}
