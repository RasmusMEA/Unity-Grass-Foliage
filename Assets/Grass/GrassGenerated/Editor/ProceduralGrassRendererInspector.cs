using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;


[CustomEditor(typeof(ProceduralGrassRenderer)), CanEditMultipleObjects]
public class ProceduralGrassRendererInspector : Editor {

    // Reference to the ProceduralGrassRenderer script
    private ProceduralGrassRenderer grassRenderer;

    // Procedural Grass Renderer settings
    private SerializedProperty m_enableLOD;
    private SerializedProperty m_cameraLODFar;
    private SerializedProperty m_cameraLODNear;
    private SerializedProperty m_cameraLODFactor;

    // Called when the script is loaded or a value is changed in the inspector
    private void OnEnable() {
        grassRenderer = target as ProceduralGrassRenderer;

        // Get the serialized properties
        m_enableLOD = serializedObject.FindProperty("m_enableLOD");
        m_cameraLODFar = serializedObject.FindProperty("m_cameraLODFar");
        m_cameraLODNear = serializedObject.FindProperty("m_cameraLODNear");
        m_cameraLODFactor = serializedObject.FindProperty("m_cameraLODFactor");
    }

    // Custom inspector GUI
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        // // Update the serialized object
        // serializedObject.Update();
        // EditorGUI.BeginChangeCheck();

        // // Draw LOD settings
        // m_enableLOD.boolValue = EditorGUILayout.Toggle("Enable LOD", m_enableLOD.boolValue);
        // if (m_enableLOD.boolValue) {
        //     m_cameraLODFar.floatValue = EditorGUILayout.FloatField("Camera LOD Far", m_cameraLODFar.floatValue);
        //     m_cameraLODNear.floatValue = EditorGUILayout.FloatField("Camera LOD Near", m_cameraLODNear.floatValue);
        //     m_cameraLODFactor.floatValue = EditorGUILayout.FloatField("Camera LOD Factor", m_cameraLODFactor.floatValue);
        // }

        // // Apply the changes
        // if (EditorGUI.EndChangeCheck()) {
        //     serializedObject.ApplyModifiedProperties();
        // }
    }
}
