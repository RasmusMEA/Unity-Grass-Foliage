using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VegetationInstance", menuName = "Vegetation Instance")]
public class VegetationInstance : ScriptableObject {
    
    [Header("Vegetation Settings")]
    [SerializeField] public GameObject prefab;

    [SerializeField] public float alignToNormal = 1.0f; // Align the instance to the normal of the terrain (0 = none, 1 = fully aligned)
    
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

    public struct VegetationData {
        public Vector3 offset;                          // Offset of the vegetation instance
        public Vector3 scale;                           // Scale of the vegetation instance
        public Vector3 rotation;                        // Rotation of the vegetation instance

        public float size;                              // Size of the vegetation instance, used for culling
        public float alignToNormal;                     // Align the instance to the normal of the terrain (0 = none, 1 = fully aligned)

        public int influenceMask;                       // Influence mask for vegetation influences
    }

    public static int GetBufferSize(int influenceDataPoints) {
        return sizeof(float) * influenceDataPoints * 5;
    }

    public VegetationData GetVegetationData() {
        float modelSize = 1.0f;
        LODGroup lodGroup = null;
        MeshFilter meshFilter = null;
        if ((lodGroup = prefab.GetComponent<LODGroup>()) != null) {
            modelSize = lodGroup.size;
        } else if ((meshFilter = prefab.GetComponent<MeshFilter>()) != null) {
            modelSize = meshFilter.sharedMesh.bounds.size.magnitude * 2.0f;
        }

        int influenceMaskData = 0;
        if (useHeightInfluence) influenceMaskData |= 1 << 0;
        if (useWaterDepthInfluence) influenceMaskData |= 1 << 1;
        if (useRelativeHeightInfluence) influenceMaskData |= 1 << 2;
        if (useSlopeInfluence) influenceMaskData |= 1 << 3;
        if (useMoistureInfluence) influenceMaskData |= 1 << 4;

        return new VegetationData {
            offset = prefab.transform.position,
            scale = prefab.transform.localScale,
            rotation = prefab.transform.rotation.eulerAngles,
            
            size = modelSize,
            alignToNormal = this.alignToNormal,
            
            influenceMask = influenceMaskData
        };
    }

    public void GetInfluenceData(int influenceDataPoints, int offset, ref float[] influenceData) {
        for (int i = 0; i < influenceDataPoints; i++) {
            influenceData[offset + i + influenceDataPoints * 0] = heightInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 1] = waterDepthInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 2] = relativeHeightInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 3] = slopeInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[offset + i + influenceDataPoints * 4] = moistureInfluence.Evaluate(i / (float)influenceDataPoints);
        }
    }

    public void GetLODData(int LODCount, int offset, ref float[] LODData) {
        LODGroup lodGroup = prefab.GetComponent<LODGroup>();
        if (lodGroup == null) {
            for (int i = 0; i < LODCount; i++) {
                LODData[offset + i] = 0;
            }
            return;
        }

        LOD[] lods = lodGroup.GetLODs();
        for (int i = 0; i < LODCount; i++) {
            if (lodGroup.lodCount > i) {
                LODData[offset + i] = lods[i].screenRelativeTransitionHeight;
            } else {
                LODData[offset + i] = 0;
            }
        }
    }
}
