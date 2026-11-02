using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Loot Containers: every LootContainer in the open scene(s) with exactly what it gives. Edit a container's items
/// here (or in its Inspector), select / frame it in the Scene view, and see the totals per item and which ones are empty.
/// </summary>
public class LootContainersWindow : EditorWindow
{
    const string PickupFolder = "Assets/Prefab/ItemsPickups";

    List<LootContainer> containers = new List<LootContainer>();
    readonly HashSet<int> expanded = new HashSet<int>();
    GameObject[] pickups = new GameObject[0];
    string[] pickupNames = new string[0];
    string search = "";
    Vector2 scroll;

    [MenuItem("Tools/Loot Containers")]
    static void Open() => GetWindow<LootContainersWindow>("Loot Containers");

    void OnEnable()
    {
        Refresh();
        EditorApplication.hierarchyChanged += Refresh;
    }

    void OnDisable() => EditorApplication.hierarchyChanged -= Refresh;

    void Refresh()
    {
        containers = FindObjectsByType<LootContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => !EditorUtility.IsPersistent(c))
            .OrderBy(c => Path(c.transform))
            .ToList();

        pickups = AssetDatabase.FindAssets("t:Prefab", new[] { PickupFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && (p.GetComponent<ItemPickup>() != null || p.GetComponent<InspectableItemPickup>() != null))
            .OrderBy(p => p.name)
            .ToArray();
        pickupNames = new[] { "+ Add item..." }.Concat(pickups.Select(p => p.name)).ToArray();
        Repaint();
    }

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
            if (GUILayout.Button("Expand all", EditorStyles.toolbarButton, GUILayout.Width(70)))
                foreach (var c in containers) if (c != null) expanded.Add(c.GetInstanceID());
            if (GUILayout.Button("Collapse all", EditorStyles.toolbarButton, GUILayout.Width(75))) expanded.Clear();
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        }

        containers.RemoveAll(c => c == null);
        DrawSummary();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var c in containers)
        {
            string path = Path(c.transform);
            string contentText = Describe(c.Contents);
            if (!string.IsNullOrEmpty(search) &&
                path.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                contentText.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            DrawRow(c, path, contentText);
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawSummary()
    {
        int empty = containers.Count(c => c.Contents.All(p => p == null));
        var totals = containers.SelectMany(c => c.Contents).Where(p => p != null)
            .GroupBy(p => p.name).OrderBy(g => g.Key).Select(g => g.Key + " x" + g.Count());

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(containers.Count + " containers in the open scene" + (empty > 0 ? ",  " + empty + " empty" : ""), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Join(",  ", totals), EditorStyles.wordWrappedMiniLabel);
        }
    }

    void DrawRow(LootContainer c, string path, string contentText)
    {
        int id = c.GetInstanceID();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool open = EditorGUILayout.Foldout(expanded.Contains(id), path, true);
                if (open) expanded.Add(id); else expanded.Remove(id);
                if (GUILayout.Button("Select", GUILayout.Width(55))) { Selection.activeObject = c.gameObject; EditorGUIUtility.PingObject(c.gameObject); }
                if (GUILayout.Button("Frame", GUILayout.Width(50))) { Selection.activeObject = c.gameObject; SceneView.FrameLastActiveSceneView(); }
            }

            var style = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            if (c.Contents.All(p => p == null)) style.normal.textColor = new Color(0.9f, 0.45f, 0.35f);
            EditorGUILayout.LabelField(contentText, style);

            if (!expanded.Contains(id)) return;

            var so = new SerializedObject(c);
            EditorGUILayout.PropertyField(so.FindProperty("contents"), new GUIContent("Gives"), true);
            so.ApplyModifiedProperties();

            int pick = EditorGUILayout.Popup(0, pickupNames);
            if (pick > 0)
            {
                Undo.RecordObject(c, "Add loot item");
                c.Contents.Add(pickups[pick - 1]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                EditorUtility.SetDirty(c);
            }
        }
    }

    static string Describe(List<GameObject> items)
    {
        var names = items.Where(p => p != null).Select(p => p.name).ToList();
        return names.Count == 0 ? "(empty)" : string.Join(", ", names);
    }

    static string Path(Transform t)
    {
        string p = t.name;
        for (var parent = t.parent; parent != null; parent = parent.parent) p = parent.name + " / " + p;
        return p;
    }
}
