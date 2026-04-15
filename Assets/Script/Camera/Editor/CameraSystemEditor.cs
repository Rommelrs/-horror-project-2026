using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CameraSystem))]
public class CameraSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        EditorGUILayout.Space();

        // Add button
        if (GUILayout.Button("Move Camera To Player Position"))
        {
            MoveCameraToPlayer();
        }
    }

    private void MoveCameraToPlayer()
    {
        CameraSystem cameraSystem = (CameraSystem)target;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("No GameObject with tag 'Player' found in scene.");
            return;
        }

        SerializedObject so = new SerializedObject(cameraSystem);
        SerializedProperty camTransformProp = so.FindProperty("m_CameraTransform");
        Transform camTransform = camTransformProp.objectReferenceValue as Transform;

        if (camTransform != null)
        {
            camTransform.position = player.transform.position;
            EditorUtility.SetDirty(cameraSystem);
        }
        else
        {
            Debug.LogWarning("CameraSystem: m_CameraTransform is not assigned.");
        }
    }
}
