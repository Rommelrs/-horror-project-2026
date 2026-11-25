using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

public static partial class Nov25Builder
{
    // ------------------------------------------------------------------ 2 + 7: two more rows on the Options page

    public static string OptionRows()
    {
        const string path = "Assets/Prefab/OptionsManager.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform settings = null, ammo = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "SettingsGroup") settings = t;
                if (t.name == "AmmoCounterRow") ammo = t;
            }
            foreach (var n in new[] { "HintsRow", "StabilityTutorialRow" })
            {
                var old = settings.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var language = settings.Find("Image (5)");
            var controls = settings.Find("ControlsButtonRow");

            // Puzzle hints: the Language row with the ammo counter's check box instead of the dropdown
            var hints = Object.Instantiate(language.gameObject, settings);
            hints.name = "HintsRow";
            hints.transform.SetSiblingIndex(language.GetSiblingIndex() + 1);
            Object.DestroyImmediate(hints.transform.Find("Dropdown").gameObject);
            var toggle = Object.Instantiate(ammo.Find("Toggle").gameObject, hints.transform);
            toggle.name = "Toggle";
            var trt = (RectTransform)toggle.transform;
            trt.anchoredPosition = new Vector2(-30f, 0f); trt.sizeDelta = new Vector2(44f, 44f);
            AgentTools.SetStr("GameUI", "Puzzle Hints", "Puzzle Hints", "\u062a\u0644\u0645\u064a\u062d\u0627\u062a \u0627\u0644\u0623\u0644\u063a\u0627\u0632");
            AgentTools.SetStr("GameUI", "Stability Tutorial", "Stability Tutorial  >", "\u0634\u0631\u062d \u0627\u0644\u0627\u062a\u0632\u0627\u0646");
            Relabel(hints.transform, "Puzzle Hints", "Puzzle Hints");

            // Stability tutorial: a button row like Controls
            var row = Object.Instantiate(controls.gameObject, settings);
            row.name = "StabilityTutorialRow";
            row.transform.SetSiblingIndex(controls.GetSiblingIndex() + 1);
            row.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
            var label = row.GetComponentInChildren<Text>(true);
            label.text = "Stability Tutorial  >";

            // nine rows fit the page
            foreach (Transform r in settings)
            {
                var rt = (RectTransform)r;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 74f);
            }
            var so = new SerializedObject(root.GetComponent<OptionsManager>());
            so.FindProperty("hintsToggle").objectReferenceValue = toggle.GetComponent<Toggle>();
            so.FindProperty("stabilityTutorialButton").objectReferenceValue = row.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return "option rows built";
    }

    static void Relabel(Transform row, string key, string text)
    {
        var t = row.GetComponentInChildren<Text>(true);
        t.text = text;
        var loc = t.GetComponent<LocalizeStringEvent>();
        if (loc == null) return;
        loc.StringReference.SetReference("GameUI", key);
        // the copied event still points at the old row's Text
        var so = new SerializedObject(loc);
        var calls = so.FindProperty("m_UpdateString.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++) calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue = t;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ 5 / 53: the lock's hint line

    public static string LockHint()
    {
        AgentTools.SetStr("Subtitles", CombinationLockInteractable.HintLine, "Maybe it's related to these removed numbers",
            "\u0631\u0628\u0645\u0627 \u0644\u0647 \u0639\u0644\u0627\u0642\u0629 \u0628\u0647\u0630\u0647 \u0627\u0644\u0623\u0631\u0642\u0627\u0645 \u0627\u0644\u0645\u062d\u0630\u0648\u0641\u0629");
        AssetDatabase.SaveAssets();
        return AgentTools.Str("Subtitles", CombinationLockInteractable.HintLine);
    }

    // ------------------------------------------------------------------ 40: the park's main entrance

    public static string ParkGateBuild()
    {
        var env = GameObject.Find("------------Environment----------").transform.Find("Environment7");
        var old = env.Find("Park Gate");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        const float x = -140.15f, z0 = 66.91f, z1 = 70.97f;
        float y = float.MinValue;
        foreach (float z in new[] { z0 + 0.3f, (z0 + z1) * 0.5f, z1 - 0.3f })
            foreach (var h in Physics.RaycastAll(new Vector3(x, 12f, z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.name != "Player" && h.point.y < 6f) y = Mathf.Max(y, h.point.y);
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Textures/AlleyReal/ParkFence.mat");
        var root = new GameObject("Park Gate").transform;
        root.SetParent(env, false);
        root.position = new Vector3(x, y, (z0 + z1) * 0.5f);
        var open = new GameObject("Open").transform; open.SetParent(root, false);
        var closed = new GameObject("Closed").transform; closed.SetParent(root, false);
        float w = (z1 - z0) * 0.5f - 0.05f;
        var mesh = GateLeafMesh(w, false); var mesh2 = GateLeafMesh(w, true);
        // hinged on the two end posts; open = swung into the park, along the walkway
        Leaf(open, mesh, mat, new Vector3(x - 0.05f, y, z0 + 0.06f), Quaternion.Euler(0f, 180f + 94f, 0f), false);
        Leaf(open, mesh2, mat, new Vector3(x - 0.05f, y, z1 - 0.06f), Quaternion.Euler(0f, 180f + 86f, 0f), false);
        Leaf(closed, mesh, mat, new Vector3(x, y, z0 + 0.03f), Quaternion.identity, true);
        Leaf(closed, mesh2, mat, new Vector3(x, y, z1 - 0.03f), Quaternion.Euler(0f, 180f, 0f), true);
        Chain(closed, new Vector3(x, y + 0.95f, (z0 + z1) * 0.5f));
        // nothing walks round it
        var block = closed.gameObject.AddComponent<BoxCollider>();
        block.center = new Vector3(0f, 1.3f, 0f); block.size = new Vector3(0.25f, 3.2f, z1 - z0);
        var ob = closed.gameObject.AddComponent<NavMeshObstacle>();
        ob.shape = NavMeshObstacleShape.Box; ob.center = block.center; ob.size = new Vector3(0.5f, 3f, z1 - z0); ob.carving = true;
        var gate = root.gameObject.AddComponent<ParkGate>();
        var so = new SerializedObject(gate);
        so.FindProperty("open").objectReferenceValue = open.gameObject;
        so.FindProperty("closed").objectReferenceValue = closed.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        closed.gameObject.SetActive(false);
        Dirty(root.gameObject);
        return "gate at y " + y;
    }

    static void Leaf(Transform parent, Mesh mesh, Material mat, Vector3 hinge, Quaternion rot, bool shut)
    {
        var go = new GameObject("Leaf");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(hinge, rot);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (shut) return;
        // the open leaves are thin, but solid
        var c = go.AddComponent<BoxCollider>();
        c.center = new Vector3(0f, 0.8f, mesh.bounds.center.z); c.size = new Vector3(0.06f, 1.6f, mesh.bounds.size.z);
    }

    // a leaf in the fence's own pattern, from its hinge along +z: a frame of square bar, the panel, and the meeting stile
    static Mesh GateLeafMesh(float w, bool flip)
    {
        const float panelH = 1.75f;
        Vector2 panelU = new Vector2(0f, 224f / 256f), barU = new Vector2(230f / 256f, 254f / 256f);
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 u, float v0, float v1)
        {
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.AddRange(new[] { new Vector2(u.x, v0), new Vector2(u.y, v0), new Vector2(u.y, v1), new Vector2(u.x, v1) });
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        void Box(Vector3 c, Vector3 h)
        {
            Vector3 x = Vector3.right * h.x, y = Vector3.up * h.y, z = Vector3.forward * h.z;
            Quad(c - z - y + x, c + z - y + x, c + z + y + x, c - z + y + x, Vector3.right, barU, 0.05f, 0.9f);
            Quad(c + z - y - x, c - z - y - x, c - z + y - x, c + z + y - x, Vector3.left, barU, 0.05f, 0.9f);
            Quad(c + x - y + z, c - x - y + z, c - x + y + z, c + x + y + z, Vector3.forward, barU, 0.05f, 0.9f);
            Quad(c - x - y - z, c + x - y - z, c + x + y - z, c - x + y - z, Vector3.back, barU, 0.05f, 0.9f);
            Quad(c - x + y - z, c + x + y - z, c + x + y + z, c - x + y + z, Vector3.up, barU, 0.8f, 0.9f);
            Quad(c - x - y + z, c + x - y + z, c + x - y - z, c - x - y - z, Vector3.down, barU, 0.05f, 0.1f);
        }
        const float t = 0.035f;
        Box(new Vector3(0f, 0.84f, t), new Vector3(t, 0.8f, t));
        Box(new Vector3(0f, 0.84f, w - t), new Vector3(t, 0.8f, t));
        Box(new Vector3(0f, 0.1f, w * 0.5f), new Vector3(t * 0.8f, t, w * 0.5f));
        Box(new Vector3(0f, 1.2f, w * 0.5f), new Vector3(t * 0.8f, t, w * 0.5f));
        Quad(new Vector3(0f, 0.06f, t * 2f), new Vector3(0f, 0.06f, w - t * 2f), new Vector3(0f, 0.06f + panelH, w - t * 2f), new Vector3(0f, 0.06f + panelH, t * 2f), flip ? Vector3.left : Vector3.right, panelU, 0f, 1f);
        var mesh = new Mesh { name = "ParkGateLeaf" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        string p = "Assets/Models/ParkFence/ParkGateLeaf" + (flip ? "2" : "") + ".asset";
        var e = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (e != null) { e.Clear(); EditorUtility.CopySerialized(mesh, e); EditorUtility.SetDirty(e); return e; }
        AssetDatabase.CreateAsset(mesh, p);
        return mesh;
    }

    static Material PlainMat(string name, Color c, float gloss)
    {
        string p = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); System.IO.Directory.CreateDirectory("Assets/Materials"); AssetDatabase.CreateAsset(m, p); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // the rusty chain round the two meeting stiles and its padlock
    static void Chain(Transform parent, Vector3 at)
    {
        var rust = PlainMat("GateChain", new Color(0.27f, 0.2f, 0.15f), 0.2f);
        var brass = PlainMat("GatePadlock", new Color(0.42f, 0.34f, 0.16f), 0.35f);
        var root = new GameObject("Chain").transform;
        root.SetParent(parent, false); root.position = at;
        var link = LinkMesh();
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            var p = new Vector3(Mathf.Cos(a) * 0.085f, -0.035f * Mathf.Cos(a * 2f), Mathf.Sin(a) * 0.15f);
            var q = new Vector3(Mathf.Cos(a + 0.05f) * 0.085f, -0.035f * Mathf.Cos((a + 0.05f) * 2f), Mathf.Sin(a + 0.05f) * 0.15f);
            var go = new GameObject("Link");
            go.transform.SetParent(root, false);
            go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.LookRotation(q - p, Vector3.up) * Quaternion.Euler(0f, 0f, i % 2 * 90f);
            go.AddComponent<MeshFilter>().sharedMesh = link;
            go.AddComponent<MeshRenderer>().sharedMaterial = rust;
        }
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.name = "Padlock"; body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0.1f, -0.12f, 0f); body.transform.localScale = new Vector3(0.035f, 0.085f, 0.075f);
        body.GetComponent<Renderer>().sharedMaterial = brass;
        var shackle = new GameObject("Shackle");
        shackle.transform.SetParent(root, false);
        shackle.transform.localPosition = new Vector3(0.1f, -0.06f, 0f);
        shackle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shackle.transform.localScale = new Vector3(1.1f, 1.1f, 1.2f);
        shackle.AddComponent<MeshFilter>().sharedMesh = link;
        shackle.AddComponent<MeshRenderer>().sharedMaterial = rust;
    }

    static Mesh LinkMesh()
    {
        const string p = "Assets/Models/ParkFence/ChainLink.asset";
        var e = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (e != null) return e;
        const int seg = 10, side = 5;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 c = new Vector3(Mathf.Cos(a) * 0.022f, 0f, Mathf.Sin(a) * 0.034f);
            Vector3 outw = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int k = 0; k < side; k++)
            {
                float b = k * Mathf.PI * 2f / side;
                v.Add(c + (outw * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b)) * 0.0075f);
            }
        }
        for (int i = 0; i < seg; i++)
            for (int k = 0; k < side; k++)
            {
                int a = i * side + k, b = i * side + (k + 1) % side, c = ((i + 1) % seg) * side + k, d = ((i + 1) % seg) * side + (k + 1) % side;
                tri.AddRange(new[] { a, b, c, b, d, c });
            }
        var m = new Mesh { name = "ChainLink" };
        m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
}
