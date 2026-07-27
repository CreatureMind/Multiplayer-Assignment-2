#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Position), true)]
public class PositionPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw the field's prefix label (e.g., "Muzzle Position")
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Check if the current polymorphic reference is unassigned (null)
        if (property.managedReferenceValue == null)
        {
            // Draw a temporary Object field accepting Unity objects (GameObject or Component)
            EditorGUI.BeginChangeCheck();
            Object tempTarget = EditorGUI.ObjectField(position, null, typeof(Object), true);
            
            if (EditorGUI.EndChangeCheck() && tempTarget != null)
            {
                // Construct the correct sub-wrapper dynamically based on what was dragged in
                if (tempTarget is GameObject go)
                {
                    property.managedReferenceValue = new GameObjectPosition(go);
                }
                else if (tempTarget is Component comp)
                {
                    property.managedReferenceValue = new ComponentPosition(comp);
                }
                
                property.serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUI.EndProperty();
            return;
        }

        // --- If it IS initialized, draw the child elements cleanly on one line ---
        SerializedProperty targetProp = property.FindPropertyRelative("target");
        SerializedProperty spaceProp = property.FindPropertyRelative("space");

        float targetWidth = position.width * 0.7f;
        float spaceWidth = position.width * 0.3f;

        Rect targetRect = new Rect(position.x, position.y, targetWidth - 5, position.height);
        Rect spaceRect = new Rect(position.x + targetWidth, position.y, spaceWidth, position.height);

        // 1. Draw the reference field (e.g. the specific Transform or GameObject assigned)
        if (targetProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(targetRect, targetProp, GUIContent.none);
            
            // If the user clears the object field (deletes it), reset the wrapper back to null
            if (EditorGUI.EndChangeCheck() && targetProp.objectReferenceValue == null)
            {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        // 2. Draw the World/Local Space dropdown menu
        if (spaceProp != null)
        {
            EditorGUI.PropertyField(spaceRect, spaceProp, GUIContent.none);
        }

        EditorGUI.EndProperty();
    }
}
#endif