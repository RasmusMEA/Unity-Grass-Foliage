using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VegetationInstance", menuName = "Vegetation Instance")]
public class VegetationInstance : ScriptableObject {
    
    [Header("Vegetation Settings")]
    [SerializeField] public Mesh mesh;
    [SerializeField] public Material material;

    [Header("Vegetation Distribution")]
    [SerializeField] public AnimationCurve heightInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve waterDepthInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve relativeHeightInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve slopeInfluence = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] public AnimationCurve moistureInfluence = AnimationCurve.Linear(0, 0, 1, 1);

    public static int GetBufferSize(int influenceDataPoints) {
        return sizeof(float) * influenceDataPoints * 5;
    }

    public float[] GetInfluenceData(int influenceDataPoints) {
        float[] influenceData = new float[influenceDataPoints * 5];
        for (int i = 0; i < influenceDataPoints; i++) {
            influenceData[i + influenceDataPoints * 0] = heightInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[i + influenceDataPoints * 1] = waterDepthInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[i + influenceDataPoints * 2] = relativeHeightInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[i + influenceDataPoints * 3] = slopeInfluence.Evaluate(i / (float)influenceDataPoints);
            influenceData[i + influenceDataPoints * 4] = moistureInfluence.Evaluate(i / (float)influenceDataPoints);
        }
        return influenceData;
    }
}
