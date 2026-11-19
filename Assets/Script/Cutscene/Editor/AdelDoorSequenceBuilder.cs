using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds "Alley 3 Gameplay/Adel Door Sequence" (AdelDoorSequence): Adel runs through the half-open whispering gate when
/// Khalid crosses the line across the Block 3 alley (x -32, the user's red line), the gate slides shut as he nears it,
/// and the locked door then gets Khalid's "That door was open... I saw him go through...". Re-runnable.
/// </summary>
public static class AdelDoorSequenceBuilder
{
    const string RootName = "Adel Door Sequence";
    const string Line = "That door was open... I saw him go through...";
    const string LineAr = "هالباب كان مفتوح... شفته دخل منه...";

    [MenuItem("Tools/Cutscenes/Build Adel Door Sequence (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[AdelDoor] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[AdelDoor] stop Play mode first"); return; }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var alley3 = all.FirstOrDefault(t => t.name == "Alley 3 Gameplay");
        var gate = all.FirstOrDefault(t => t.name == "Fence Gate" && t.parent != null && t.parent.name == "WallHoleInteractable");
        var door = all.FirstOrDefault(t => t.name == "DoorInteractableWhispering")?.GetComponent<DoorInteractable>();
        var bridgeAdel = all.FirstOrDefault(t => t.name == "Bridge Adel");
        if (alley3 == null || gate == null || door == null || bridgeAdel == null) { Debug.LogError("[AdelDoor] Alley 3 Gameplay / Fence Gate / DoorInteractableWhispering / Bridge Adel not found"); return; }

        var old = alley3.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName);
        root.transform.SetParent(alley3, false);
        root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
        var seq = root.AddComponent<AdelDoorSequence>();

        // Adel (a copy of the bridge Adel: Idle / Walk / Run)
        var adelGo = Object.Instantiate(bridgeAdel.gameObject, root.transform);
        adelGo.name = "Door Adel";
        adelGo.SetActive(false);
        var adelAnim = adelGo.GetComponentInChildren<Animator>(true);

        // the gap the gate leaves at the south side when half open, and his way through it
        var gb = gate.GetComponent<Renderer>().bounds;
        float gx = gb.center.x;
        // (user 2026-11-19: the gate slides the other way - into the SOUTH wall, the gap at the north side, where the whispering
        // door's handle is)
        Vector3 gap = new Vector3(gx, gb.min.y + 0.6f, -74.9f);
        Vector3 openOffset = new Vector3(0f, 0f, 3.4f);
        // down the north lane (the enemies stand at z -71, the table on the south wall), past the camera, into the gap,
        // out of sight beyond
        var path = new[] { new Vector3(-24.8f, 1f, -74.0f), new Vector3(-19.0f, 1f, -74.3f), new Vector3(gx, 1f, -74.9f), new Vector3(gx + 2.5f, 1f, -75.0f), new Vector3(gx + 10f, 1f, -75.2f) };
        // the camera against the north wall, 3 m short of the gate (the frozen enemies behind it)
        Vector3 shotPos = new Vector3(-19.35f, gb.min.y + 0.7f, -75.95f);
        for (int i = 1; i < path.Length; i++)
            if (Physics.Linecast(path[i - 1] + Vector3.up * 0.5f, path[i] + Vector3.up * 0.5f, out RaycastHit h, ~0, QueryTriggerInteraction.Ignore) && h.collider.transform != gate)
                Debug.LogWarning("[AdelDoor] Adel's way " + path[i - 1] + " -> " + path[i] + " blocked by " + h.collider.name + " at " + h.point);

        // Khalid's line, in the Subtitles table
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        var entry = col.SharedData.GetEntry(Line) ?? col.SharedData.AddKey(Line);
        foreach (var table in col.StringTables)
        {
            string text = table.LocaleIdentifier.Code.StartsWith("ar") ? LineAr : Line;
            var e = table.GetEntry(entry.Id);
            if (e == null) table.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(col.SharedData);

        var so = new SerializedObject(seq);
        so.FindProperty("gate").objectReferenceValue = gate;
        so.FindProperty("door").objectReferenceValue = door;
        so.FindProperty("adel").objectReferenceValue = adelAnim;
        var ap = so.FindProperty("adelPath"); ap.arraySize = path.Length;
        for (int i = 0; i < path.Length; i++) ap.GetArrayElementAtIndex(i).vector3Value = path[i];
        so.FindProperty("gapPoint").vector3Value = gap;
        so.FindProperty("closeShot").vector3Value = shotPos;
        so.FindProperty("gateOpenOffset").vector3Value = openOffset;
        so.FindProperty("gateOpenClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Door/Gate door/Gate open.mp3");
        so.FindProperty("gateCloseClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Door/Gate door/Gate close.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();
        var ls = new SerializedObject(seq).FindProperty("doorLine");
        ls.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = col.SharedData.TableCollectionNameGuid.ToString("N").Insert(0, "GUID:");
        ls.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = entry.Id;
        ls.serializedObject.ApplyModifiedPropertiesWithoutUndo();

        // the locked door's "failed" event -> his line
        for (int i = door.OnInteractionFailed.GetPersistentEventCount() - 1; i >= 0; i--)
            if (door.OnInteractionFailed.GetPersistentMethodName(i) == nameof(AdelDoorSequence.OnDoorTried)) UnityEventTools.RemovePersistentListener(door.OnInteractionFailed, i);
        UnityEventTools.AddPersistentListener(door.OnInteractionFailed, seq.OnDoorTried);
        EditorUtility.SetDirty(door);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[AdelDoor] built: gate " + gb.center + " gap " + gap + ", Adel " + string.Join(" -> ", path));
    }
}
