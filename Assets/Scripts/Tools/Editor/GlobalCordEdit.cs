using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldTransformCoordinates))]
public class WorldTransformCoordinatesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WorldTransformCoordinates script = (WorldTransformCoordinates)target;
        Transform objectTransform = script.transform;

        EditorGUILayout.LabelField("Глобальные координаты", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        Vector3 newWorldPosition = EditorGUILayout.Vector3Field(
            "World Position",
            objectTransform.position
        );

        Vector3 newWorldRotation = EditorGUILayout.Vector3Field(
            "World Rotation",
            objectTransform.eulerAngles
        );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(objectTransform, "Change World Transform");

            objectTransform.position = newWorldPosition;
            objectTransform.eulerAngles = newWorldRotation;

            EditorUtility.SetDirty(objectTransform);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Локальные координаты", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true);

        EditorGUILayout.Vector3Field(
            "Local Position",
            objectTransform.localPosition
        );

        EditorGUILayout.Vector3Field(
            "Local Rotation",
            objectTransform.localEulerAngles
        );

        EditorGUILayout.Vector3Field(
            "Local Scale",
            objectTransform.localScale
        );

        EditorGUI.EndDisabledGroup();
    }
}