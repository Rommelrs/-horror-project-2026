using UnityEditor;
using UnityEngine;

// Inspector buttons for framing a fixed room camera: fly the Scene view to the shot you want and press "Set Anchor From
// Scene View", or press "Look Through Anchor" to put the Scene view exactly where the game camera will be.
[CustomEditor(typeof(CameraFixedZone))]
public class CameraFixedZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var zone = (CameraFixedZone)target;
        EditorGUILayout.Space();
        if (zone.cameraAnchor == null)
        {
            EditorGUILayout.HelpBox("Assign a Camera Anchor (an empty child Transform).", MessageType.Warning);
            return;
        }

        SceneView sv = SceneView.lastActiveSceneView;
        using (new EditorGUI.DisabledScope(sv == null))
        {
            if (GUILayout.Button("Set Anchor From Scene View", GUILayout.Height(26)))
            {
                Undo.RecordObject(zone.cameraAnchor, "Set Fixed Camera Anchor");
                Transform c = sv.camera.transform;
                zone.cameraAnchor.SetPositionAndRotation(c.position, c.rotation);
                EditorUtility.SetDirty(zone.cameraAnchor);
            }
            if (GUILayout.Button("Look Through Anchor (Scene View)", GUILayout.Height(22)))
            {
                Camera game = Camera.main;
                if (game != null) sv.cameraSettings.fieldOfView = game.fieldOfView;   // same lens as the game, so the framing matches
                sv.orthographic = false;
                sv.LookAtDirect(zone.cameraAnchor.position + zone.GetCameraRotation() * Vector3.forward * sv.cameraDistance,
                                zone.GetCameraRotation());
                sv.Repaint();
            }
        }
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Changes made in Play mode are lost when you stop. Right-click the anchor's Transform > Copy Component, stop, then Paste Component Values.", MessageType.Info);
    }
}
