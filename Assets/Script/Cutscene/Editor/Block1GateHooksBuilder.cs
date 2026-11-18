using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Cutscenes > Build Block 1 Gate Hooks (Game scene), user 2026-11-18:
///  - GateEntryLock on DoorInteractable (Entrance): going through the gate into Block 1 fires the "can't leave yet" lock.
///  - the "He might be there!" line (the park chase's back-to-the-gate reminder, KidnapSequence).
/// Safe to run again.
/// </summary>
public static class Block1GateHooksBuilder
{
    [MenuItem("Tools/Cutscenes/Build Block 1 Gate Hooks (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[Block1Gate] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[Block1Gate] stop Play mode first"); return; }

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var door = all.Select(t => t.GetComponent<DoorInteractable>()).FirstOrDefault(d => d != null && d.name == "DoorInteractable (Entrance)");
        var trig = all.Select(t => t.GetComponent<OnPlayerTrigger>())
                      .FirstOrDefault(o => o != null && o.name == "Trigger" && o.transform.parent != null && o.transform.parent.name == "CantLeaveScenario");
        var knife = all.FirstOrDefault(t => t.name == "KnifeHandlePickup");
        if (door == null || trig == null || knife == null) { Debug.LogError("[Block1Gate] Entrance door / CantLeaveScenario/Trigger / KnifeHandlePickup not found"); return; }

        var lockComp = door.GetComponent<GateEntryLock>();
        if (lockComp == null) lockComp = door.gameObject.AddComponent<GateEntryLock>();
        var so = new SerializedObject(lockComp);
        so.FindProperty("door").objectReferenceValue = door;
        so.FindProperty("lockTrigger").objectReferenceValue = trig;
        so.FindProperty("lockTriggerId").stringValue = trig.GetComponent<UniqueID>() != null ? trig.GetComponent<UniqueID>().ID : "";
        so.FindProperty("knifeHandleId").stringValue = knife.GetComponent<UniqueID>() != null ? knife.GetComponent<UniqueID>().ID : "";
        so.ApplyModifiedPropertiesWithoutUndo();

        Line("He might be there!", "He might be there!", "يمكن يكون هناك!");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Block1Gate] built: GateEntryLock on " + door.name + " (lock trigger id " + so.FindProperty("lockTriggerId").stringValue + ")");
    }

    static void Line(string key, string en, string ar)
    {
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        var entry = col.SharedData.GetEntry(key) ?? col.SharedData.AddKey(key);
        foreach (var t in col.StringTables)
        {
            string text = t.LocaleIdentifier.Code.StartsWith("ar") ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;
            var e = t.GetEntry(entry.Id);
            if (e == null) t.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(t);
        }
        EditorUtility.SetDirty(col.SharedData);
    }
}
