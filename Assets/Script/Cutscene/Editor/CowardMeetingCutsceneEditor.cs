using UnityEditor;
using UnityEngine;

// Inspector + menu button to play the coward meeting straight away in Play mode (no knife handle, rope shown burned).
[CustomEditor(typeof(CowardMeetingCutscene))]
public class CowardMeetingCutsceneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button(Application.isPlaying ? "Play Cutscene Now (debug)" : "Play Cutscene Now (debug) - enter Play mode first", GUILayout.Height(28)))
                ((CowardMeetingCutscene)target).DebugPlayNow();
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    [MenuItem("Tools/Cutscenes/Play Coward Meeting 1 Now (debug) %#k")]
    static void PlayNow()
    {
        var cs = Object.FindFirstObjectByType<CowardMeetingCutscene>();
        if (cs != null) cs.DebugPlayNow();
    }

    [MenuItem("Tools/Cutscenes/Play Coward Meeting 1 Now (debug) %#k", true)]
    static bool PlayNowValid() => Application.isPlaying && Object.FindFirstObjectByType<CowardMeetingCutscene>() != null;
}
