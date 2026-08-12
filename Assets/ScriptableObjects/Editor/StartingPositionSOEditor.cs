using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StartingPositionSO))]
public class StartingPositionSOEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Open Starting Position Editor"))
        {
            StartingPositionEditorWindow.Open((StartingPositionSO)target);
        }
    }
}
