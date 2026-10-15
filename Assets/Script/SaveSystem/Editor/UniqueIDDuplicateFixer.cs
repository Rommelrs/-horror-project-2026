using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Two objects with the same UniqueID share one saved state: killing one enemy, taking one pickup or firing one trigger marks all of
/// its twins as done in the next save / checkpoint (they vanish or switch off on load). Copy-pasting a saveable object copies its ID,
/// so this happens easily. This finds them in the open scene(s) and gives every duplicate except one a fresh ID.
/// </summary>
public static class UniqueIDDuplicateFixer
{
    [MenuItem("Tools/Save System/Report Duplicate UniqueIDs (open scenes)")]
    public static void Report() { Run(false); }

    [MenuItem("Tools/Save System/Fix Duplicate UniqueIDs (open scenes)")]
    public static void Fix() { Run(true); }

    static string Path(Transform t) { string p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }

    static bool IsSaveable(GameObject go)
    {
        return go.GetComponent<SaveableEnemy>() || go.GetComponent<SaveableTrigger>() || go.GetComponent<SaveableSwitch>()
            || go.GetComponent<SaveableInteractable>() || go.GetComponent<SaveablePickup>() || go.GetComponent<SaveableContainer>()
            || go.GetComponent<SaveableSpawner>() || go.GetComponent<SaveableDisableState>();
    }

    /// <returns>how many IDs were (or would be) regenerated</returns>
    public static int Run(bool apply)
    {
        var groups = new Dictionary<string, List<UniqueID>>();
        foreach (UniqueID u in Object.FindObjectsByType<UniqueID>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string id = new SerializedObject(u).FindProperty("id").stringValue;
            if (string.IsNullOrEmpty(id)) continue;
            if (!groups.ContainsKey(id)) groups[id] = new List<UniqueID>();
            groups[id].Add(u);
        }

        int changed = 0;
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            // the one that keeps the ID (so existing saves keep matching it): a saveable one, then the first by hierarchy path
            kv.Value.Sort((a, b) =>
            {
                int sa = IsSaveable(a.gameObject) ? 0 : 1, sb = IsSaveable(b.gameObject) ? 0 : 1;
                return sa != sb ? sa.CompareTo(sb) : string.CompareOrdinal(Path(a.transform), Path(b.transform));
            });
            Debug.Log("[UniqueID] " + kv.Value.Count + " objects share " + kv.Key + ": keeping it on '" + Path(kv.Value[0].transform) + "'");
            for (int i = 1; i < kv.Value.Count; i++)
            {
                UniqueID u = kv.Value[i];
                string fresh = System.Guid.NewGuid().ToString();
                Debug.Log("[UniqueID]    " + (apply ? "new id " + fresh + " for" : "would change") + " '" + Path(u.transform) + "'");
                changed++;
                if (!apply) continue;
                var so = new SerializedObject(u);
                so.FindProperty("id").stringValue = fresh;      // through SerializedObject so a prefab instance records the override
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(u);
                EditorSceneManager.MarkSceneDirty(u.gameObject.scene);
            }
        }
        Debug.Log("[UniqueID] " + (apply ? "regenerated " : "found ") + changed + " duplicate id(s)");
        return changed;
    }
}
