using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


[CreateAssetMenu(fileName = "VegetationInstance", menuName = "Vegetation Instance")]
public class VegetationInstance : ScriptableObject {
    
    [Header("Vegetation Settings")]
    [SerializeField] public GameObject[] prefabs = new GameObject[0];
    [SerializeField] public float[] prefabWeights = new float[0];

    [SerializeField] [Range(0, 1)] public float alignToNormal = 1.0f; // Align the instance to the normal of the terrain (0 = none, 1 = fully aligned)
    
    [SerializeField] public bool useHeightInfluence = true;
    [SerializeField] public bool useWaterDepthInfluence = true;
    [SerializeField] public bool useRelativeHeightInfluence = true;
    [SerializeField] public bool useSlopeInfluence = true;
    [SerializeField] public bool useMoistureInfluence = true;

    [Header("Vegetation Distribution")]
    [SerializeField] public AnimationCurve heightInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve waterDepthInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve relativeHeightInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve slopeInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve moistureInfluence = AnimationCurve.Linear(0, 0, 1, 1);

    public struct VegetationType {

        // Prefab Indexing
        public uint startIndex;
        public uint prefabCount;

        // Bit flags
        // (0-5): Height, Water Depth, Relative Height, Slope, Moisture
        // (6-31): Unused
        public uint flags;
    };

    public struct VegetationPrefab {

        // Vegetation index and prefab weight
        public uint vegetationIndex;
        public float weight;

        // Transform
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        
        // LOD & indexing
        public uint startIndex;                        // Start index of the LODs
        public uint lodCount;                          // Number of LODs
        public float radius;                           // Radius of the vegetation instance, used for culling

        public float alignToNormal;                    // Align instance to normal of terrain (0 = none, 1 = fully aligned)                
    };

    void OnValidate() {

        // Validate the prefab weights
        if (prefabWeights.Length != prefabs.Length) {
            prefabWeights = new float[prefabs.Length];
            for (int i = 0; i < prefabWeights.Length; i++) {
                prefabWeights[i] = 1.0f / prefabWeights.Length;
            }
        }
    }

    // Adds the vegetation type to the list and returns the new start index for the vegetation prefabs
    public uint GetVegetationType(uint vegetationIndex, uint startIndex, ref VegetationType[] vegetationTypes) {
        VegetationType vegetationType = new VegetationType();

        // Set the prefab index and count
        vegetationType.startIndex = startIndex;
        vegetationType.prefabCount = (uint)prefabs.Length;

        // Set the flags for the vegetation type
        vegetationType.flags = 0;
        if (useHeightInfluence) vegetationType.flags |= 1 << 0;
        if (useWaterDepthInfluence) vegetationType.flags |= 1 << 1;
        if (useRelativeHeightInfluence) vegetationType.flags |= 1 << 2;
        if (useSlopeInfluence) vegetationType.flags |= 1 << 3;
        if (useMoistureInfluence) vegetationType.flags |= 1 << 4;

        // Set the vegetation type and return the new start index
        vegetationTypes[vegetationIndex] = vegetationType;
        return startIndex + (uint)prefabs.Length;
    }

    // Adds all vegetation prefabs to the list and returns the new start index for the prefab LODs
    public uint GetVegetationPrefabs(uint vegetationIndex, uint startIndex, ref List<VegetationPrefab> vegetationPrefabs) {
        for (int i = 0; i < prefabs.Length; i++) {
            VegetationPrefab vegetationPrefab = new VegetationPrefab();
            GameObject prefab = prefabs[i];

            // Set the vegetation index and weight
            vegetationPrefab.vegetationIndex = vegetationIndex;
            vegetationPrefab.weight = prefabWeights[i];

            // Set the transform
            vegetationPrefab.position = prefab.transform.position;
            vegetationPrefab.rotation = prefab.transform.rotation.eulerAngles;
            vegetationPrefab.scale = prefab.transform.localScale;

            // Set the LOD & indexing
            vegetationPrefab.startIndex = startIndex;
            if (prefab.TryGetComponent<LODGroup>(out LODGroup lodGroup)) {
                vegetationPrefab.lodCount = (uint)lodGroup.lodCount;
                vegetationPrefab.radius = lodGroup.size;
            } else if (prefab.TryGetComponent<MeshFilter>(out MeshFilter meshFilter)) {
                vegetationPrefab.lodCount = 1;
                vegetationPrefab.radius = meshFilter.sharedMesh.bounds.size.magnitude;
            } else {
                continue;
            }

            // Set the align to normal value
            vegetationPrefab.alignToNormal = alignToNormal;

            // Add the vegetation prefab to the list
            vegetationPrefabs.Add(vegetationPrefab);
            startIndex += vegetationPrefab.lodCount;
        }

        return startIndex;
    }

    // Adds all LODs to the list and returns the new start index
    public void GetPrefabLODs(ref List<float> prefabLODs) {
        foreach (GameObject prefab in prefabs) {
            if (prefab.TryGetComponent<LODGroup>(out LODGroup lodGroup)) {
                foreach (LOD lod in lodGroup.GetLODs()) {
                    prefabLODs.Add(lod.screenRelativeTransitionHeight);
                }
            } else if (prefab.TryGetComponent<MeshFilter>(out MeshFilter meshFilter)) {
                prefabLODs.Add(0);
            }
        }
    }

    // Adds all Influence Weights to the array
    public void GetInfluenceData(int influenceDataPoints, int offset, ref float[] influenceData) {
        for (int i = 0; i < influenceDataPoints; i++) {
            influenceData[offset + i + influenceDataPoints * 0] = heightInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 1] = waterDepthInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 2] = relativeHeightInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 3] = slopeInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 4] = moistureInfluence.Evaluate(i / (float)influenceDataPoints);
        }
    }

    // Adds all GameObjects to the array and returns the new start index
    public uint GetGameObjects(uint startIndex, ref List<Renderer>[] vegetation) {
        foreach (GameObject prefab in prefabs) {
            if (prefab.TryGetComponent<LODGroup>(out LODGroup lodGroup)) {
                foreach (LOD lod in lodGroup.GetLODs()) {
                    vegetation[startIndex++] = new List<Renderer>(lod.renderers);
                }
            } else if (prefab.TryGetComponent<MeshFilter>(out MeshFilter meshFilter)) {
                vegetation[startIndex++] = new List<Renderer>() {
                    meshFilter.GetComponent<Renderer>()
                };
            }
        }

        return startIndex;
    }
}
