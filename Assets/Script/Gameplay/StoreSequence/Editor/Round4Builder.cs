using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Store redesign, playtest round 4 (2026-11-15): credit book model + "too dark" reading, delivery map in the Notes tab
/// with the [M] hint, fuse box status lamp, dust on every note, dust that stops once a thing is used, the bag's stray
/// whoosh loop, the Game store going dark after the exit, the exit voice line (gasp + "oh my god", without "what is that
/// in front of me"), cutscene subtitles that keep their voice timing. Tools > Store Makeover > Round 4 (Store + Game).
/// Works on whichever of the two scenes are loaded. Safe to run again.
/// </summary>
public static class Round4Builder
{
    const string TexDir = "Assets/Textures/Props/";
    const string MatDir = "Assets/Materials/Props/";
    const string MeshPath = "Assets/Models/StoreMakeover/Round4Meshes.asset";
    const string CreditBookItem = "Assets/Resources/Items/Notes/Credit Book.asset";
    const string MapItem = "Assets/Resources/Items/Notes/Delivery Map.asset";
    const string VoiceClip = "Assets/Audio/Voice Lines/City voice lines/Voice line 1 (gasp + oh my god).wav";

    const string DarkKey = "Too dark to read the credit book";
    const string DarkEn = "It's too dark... I can't read a thing.";
    const string DarkAr = "مظلم وايد... ما أقدر أقرا شي.";

    [MenuItem("Tools/Store Makeover/Round 4 (Store + Game)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Round4] stop Play first"); return; }
        System.IO.Directory.CreateDirectory(MatDir);
        var book = BookMesh();
        var sheet = MapSheetMesh();
        var bookMats = BookMaterials();
        var mapMats = new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DeliveryMap_Mat.mat"), Mat("RP_MapBack", null, new Color(0.86f, 0.84f, 0.78f), 0.05f) };
        var darkLine = Line("Subtitles", DarkKey, DarkEn, DarkAr);
        Line("GameUI", "Delivery Map", "Delivery Map", "خريطة التوصيل");
        Line("GameUI", "Delivery Map Description", "The delivery map from behind the counter. Press [M] to open it.", "خريطة التوصيل من ورا الكاونتر. اضغط [M] عشان تفتحها.");
        Items(darkLine);
        AssetDatabase.SaveAssets();

        var store = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        var game = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        foreach (var scene in new[] { store, game })
        {
            if (!scene.isLoaded) continue;
            InspectionModels(scene, book, bookMats, sheet, mapMats);
            SpokenCutsceneSubtitles(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        if (store.isLoaded) StoreScene(store, book, bookMats, game);
        if (game.isLoaded) GameScene(game);
        AssetDatabase.SaveAssets();
        Debug.Log("[Round4] done (store " + store.isLoaded + ", game " + game.isLoaded + ")");
    }

    // ------------------------------------------------------------------ assets

    static Material Mat(string name, string tex, Color c, float gloss)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.color = c;
        m.mainTexture = tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex) : null;
        m.SetFloat("_Glossiness", gloss);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material[] BookMaterials()
    {
        foreach (var t in new[] { "credit_book_cover.png", "credit_book_back.png", "credit_book_spine.png", "credit_book_pages.png" })
        {
            var imp = AssetImporter.GetAtPath(TexDir + t) as TextureImporter;
            if (imp != null && (imp.maxTextureSize != 1024 || imp.wrapMode != TextureWrapMode.Clamp)) { imp.maxTextureSize = 1024; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
        }
        return new[]
        {
            Mat("RP_CreditBookFront", "credit_book_cover.png", Color.white, 0.12f),
            Mat("RP_CreditBookBack", "credit_book_back.png", Color.white, 0.12f),
            Mat("RP_CreditBookSpine", "credit_book_spine.png", Color.white, 0.18f),
            Mat("RP_CreditBookPages", "credit_book_pages.png", Color.white, 0.05f),
        };
    }

    class MB
    {
        public List<Vector3> v = new List<Vector3>(); public List<Vector3> n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>();
        public List<int>[] sub;
        public MB(int subs) { sub = new List<int>[subs]; for (int i = 0; i < subs; i++) sub[i] = new List<int>(); }
        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d); n.Add(nn); n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            sub[s].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        /// <summary>Axis-aligned box; 'faces' gives the submesh and uv rect for -x,+x,-y,+y,-z,+z (null = no face).</summary>
        public void Box(Vector3 min, Vector3 max, (int s, Rect r)?[] faces)
        {
            Vector3 p(float x, float y, float z) => new Vector3(x, y, z);
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            void F(int k, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var f = faces[k]; if (f == null) return;
                var r = f.Value.r;
                Quad(f.Value.s, a, b, c, d, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin));
            }
            // each quad: bottom-left, top-left, top-right, bottom-right seen from outside
            F(0, p(x0, y0, z1), p(x0, y1, z1), p(x0, y1, z0), p(x0, y0, z0));   // -x
            F(1, p(x1, y0, z0), p(x1, y1, z0), p(x1, y1, z1), p(x1, y0, z1));   // +x
            F(2, p(x0, y0, z1), p(x0, y0, z0), p(x1, y0, z0), p(x1, y0, z1));   // -y
            F(3, p(x0, y1, z0), p(x0, y1, z1), p(x1, y1, z1), p(x1, y1, z0));   // +y
            F(4, p(x0, y0, z0), p(x0, y1, z0), p(x1, y1, z0), p(x1, y0, z0));   // -z
            F(5, p(x1, y0, z1), p(x1, y1, z1), p(x0, y1, z1), p(x0, y0, z1));   // +z
        }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Length;
            for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }

    static Mesh Save(Mesh m)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MeshPath));
        Mesh old = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshPath)) if (o is Mesh x && x.name == m.name) old = x;
        if (old != null) { EditorUtility.CopySerialized(m, old); EditorUtility.SetDirty(old); return old; }
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null) AssetDatabase.CreateAsset(m, MeshPath); else AssetDatabase.AddObjectToAsset(m, MeshPath);
        return m;
    }

    /// <summary>A hardback ledger 170 x 240 x 26 mm, real size, centred. Front cover faces -Z, spine on +X (an Arabic
    /// book opens to the left, so seen from the front its spine is on the right). Submeshes: front, back, spine, pages.</summary>
    static Mesh BookMesh()
    {
        const float W = 0.17f, H = 0.24f, T = 0.026f, board = 0.0026f, over = 0.0032f, r = T * 0.5f;
        var mb = new MB(4);
        Rect all = new Rect(0, 0, 1, 1), edgeF = new Rect(0.0f, 0.0f, 0.012f, 1f), edgeB = new Rect(0.0f, 0.0f, 0.012f, 1f);
        float xs = W * 0.5f - r * 0.35f;                   // where the boards meet the spine's round
        // front board: its face, its three cut edges (cloth wrapped round them)
        mb.Box(new Vector3(-W * 0.5f, -H * 0.5f, -T * 0.5f), new Vector3(xs, H * 0.5f, -T * 0.5f + board),
            new (int, Rect)?[] { (0, edgeF), null, (0, new Rect(0, 0, 1, 0.012f)), (0, new Rect(0, 0.988f, 1, 0.012f)), (0, all), (0, edgeF) });
        mb.Box(new Vector3(-W * 0.5f, -H * 0.5f, T * 0.5f - board), new Vector3(xs, H * 0.5f, T * 0.5f),
            new (int, Rect)?[] { (1, edgeB), null, (1, new Rect(0, 0, 1, 0.012f)), (1, new Rect(0, 0.988f, 1, 0.012f)), (1, edgeB), (1, new Rect(1, 0, -1, 1)) });
        // the page block, set back from the board edges
        mb.Box(new Vector3(-W * 0.5f + over, -H * 0.5f + over, -T * 0.5f + board), new Vector3(xs, H * 0.5f - over, T * 0.5f - board),
            new (int, Rect)?[] { (3, new Rect(0, 0, 1, 1)), null, (3, new Rect(0, 0, 1, 1)), (3, new Rect(0, 0, 1, 1)), null, null });
        // the spine: a half round of black cloth, and the cloth tape that runs onto both boards
        const int seg = 10;
        for (int i = 0; i < seg; i++)
        {
            float a0 = -90f + 180f * i / seg, a1 = -90f + 180f * (i + 1) / seg;
            Vector3 c0 = new Vector3(xs + Mathf.Cos(a0 * Mathf.Deg2Rad) * r * 0.75f, 0, Mathf.Sin(a0 * Mathf.Deg2Rad) * r);
            Vector3 c1 = new Vector3(xs + Mathf.Cos(a1 * Mathf.Deg2Rad) * r * 0.75f, 0, Mathf.Sin(a1 * Mathf.Deg2Rad) * r);
            float u0 = 0.2f + 0.6f * i / seg, u1 = 0.2f + 0.6f * (i + 1) / seg;
            mb.Quad(2, c0 + Vector3.down * H * 0.5f, c0 + Vector3.up * H * 0.5f, c1 + Vector3.up * H * 0.5f, c1 + Vector3.down * H * 0.5f,
                new Vector2(u0, 0), new Vector2(u0, 1), new Vector2(u1, 1), new Vector2(u1, 0));
            // spine ends (head / tail) closed with pages colour
            foreach (float sy in new[] { -1f, 1f })
            {
                Vector3 cc = new Vector3(xs, sy * H * 0.5f, 0);
                Vector3 p0 = c0 + Vector3.up * sy * H * 0.5f, p1 = c1 + Vector3.up * sy * H * 0.5f;
                if (sy < 0) mb.Quad(3, cc, p0, p1, cc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), new Vector2(0.6f, 0.6f), new Vector2(0.5f, 0.5f));
                else mb.Quad(3, cc, p1, p0, cc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), new Vector2(0.6f, 0.6f), new Vector2(0.5f, 0.5f));
            }
        }
        const float tape = 0.03f, lift = 0.0004f;
        mb.Quad(2, new Vector3(xs - tape, -H * 0.5f, -T * 0.5f - lift), new Vector3(xs - tape, H * 0.5f, -T * 0.5f - lift), new Vector3(xs, H * 0.5f, -T * 0.5f - lift), new Vector3(xs, -H * 0.5f, -T * 0.5f - lift),
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.2f, 1), new Vector2(0.2f, 0));
        mb.Quad(2, new Vector3(xs, -H * 0.5f, T * 0.5f + lift), new Vector3(xs, H * 0.5f, T * 0.5f + lift), new Vector3(xs - tape, H * 0.5f, T * 0.5f + lift), new Vector3(xs - tape, -H * 0.5f, T * 0.5f + lift),
            new Vector2(0.8f, 0), new Vector2(0.8f, 1), new Vector2(1f, 1), new Vector2(1f, 0));
        return Save(mb.ToMesh("Credit Book"));
    }

    /// <summary>An A3 sheet (420 x 297 mm) with a soft wave from being folded and carried, two-sided (back: plain paper).</summary>
    static Mesh MapSheetMesh()
    {
        const float W = 0.42f, H = 0.297f; const int nx = 16, ny = 10;
        var mb = new MB(2);
        Vector3 P(int i, int j)
        {
            float x = -W * 0.5f + W * i / nx, y = -H * 0.5f + H * j / ny;
            float z = 0.006f * Mathf.Sin(i / (float)nx * Mathf.PI * 2f) + 0.004f * Mathf.Abs(Mathf.Sin(j / (float)ny * Mathf.PI)) - (Mathf.Abs(i - nx / 2) < 1 ? 0.0025f : 0f);
            return new Vector3(x, y, z);
        }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                Vector2 u00 = new Vector2(i / (float)nx, j / (float)ny), u11 = new Vector2((i + 1) / (float)nx, (j + 1) / (float)ny);
                mb.Quad(0, P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j), u00, new Vector2(u00.x, u11.y), u11, new Vector2(u11.x, u00.y));
                Vector3 b = new Vector3(0, 0, 0.0006f);
                mb.Quad(1, P(i + 1, j) + b, P(i + 1, j + 1) + b, P(i, j + 1) + b, P(i, j) + b, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        var m = mb.ToMesh("Delivery Map Sheet");
        m.RecalculateNormals();
        return Save(m);
    }

    static LocalizedString Line(string table, string key, string en, string ar)
    {
        var col = LocalizationEditorSettings.GetStringTableCollection(table);
        var entry = col.SharedData.GetEntry(key) ?? col.SharedData.AddKey(key);
        foreach (var t in col.StringTables)
        {
            string text = t.LocaleIdentifier.Code.StartsWith("ar") ? ArabicSupport.ArabicFixer.Fix(ar, false, false) : en;
            var e = t.GetEntry(entry.Id);
            if (e == null) t.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(t);
        }
        EditorUtility.SetDirty(col.SharedData);
        return new LocalizedString { TableReference = col.SharedData.TableCollectionNameGuid, TableEntryReference = entry.Id };
    }

    static void Items(LocalizedString darkLine)
    {
        var cb = AssetDatabase.LoadAssetAtPath<Item>(CreditBookItem);
        if (cb != null) { cb.readOnOpen = true; cb.needsLightToRead = true; cb.tooDarkLine = darkLine; EditorUtility.SetDirty(cb); }
        var map = AssetDatabase.LoadAssetAtPath<Item>(MapItem);
        if (map == null) { map = ScriptableObject.CreateInstance<Item>(); AssetDatabase.CreateAsset(map, MapItem); }
        map.itemName = "Delivery Map"; map.description = "The delivery map from behind the counter";
        map.itemType = ItemType.Note; map.showReadPrompt = false; map.isStackable = false; map.maxStackSize = 1;
        EditorUtility.SetDirty(map);
    }

    // ------------------------------------------------------------------ both scenes

    static T FindIn<T>(Scene s) where T : Component
    {
        foreach (var r in s.GetRootGameObjects()) { var c = r.GetComponentInChildren<T>(true); if (c != null) return c; }
        return null;
    }

    static Transform FindPath(Scene s, string name, string parent = null)
    {
        foreach (var r in s.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name && (parent == null || (t.parent != null && t.parent.name == parent))) return t;
        return null;
    }

    static T Need<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void AddToList(Object owner, string prop, Object item)
    {
        var so = new SerializedObject(owner);
        var arr = so.FindProperty(prop);
        for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
        arr.arraySize++;
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }

    /// <summary>The models the inspection camera and the inventory show: a real book for the credit book, the map sheet.</summary>
    static void InspectionModels(Scene scene, Mesh book, Material[] bookMats, Mesh sheet, Material[] mapMats)
    {
        var handler = FindIn<ItemInspectionHandler>(scene);
        var desc = FindIn<InventoryDescriptionUI>(scene);
        if (handler == null) return;
        var soH = new SerializedObject(handler);
        var arrH = soH.FindProperty("inspectionItemObjects");
        Transform itemRoot = null;
        for (int i = 0; i < arrH.arraySize && itemRoot == null; i++)
        {
            var o = arrH.GetArrayElementAtIndex(i).objectReferenceValue;
            if (o is Component c) itemRoot = c.transform.parent;
            else if (o is GameObject g) itemRoot = g.transform.parent;
        }
        if (itemRoot == null) { Debug.LogError("[Round4] no ItemRoot in " + scene.name); return; }
        var elemType = arrH.arraySize > 0 ? arrH.GetArrayElementAtIndex(0).objectReferenceValue.GetType() : typeof(Transform);

        Object Elem(Transform t) => elemType == typeof(GameObject) ? (Object)t.gameObject : t.GetComponent(elemType);

        // credit book: the flat page quad was the "model" (and it never made it into the inventory list)
        var cb = itemRoot.Find("Credit Book");
        if (cb != null)
        {
            var quad = cb.GetComponent<MeshRenderer>(); if (quad != null) quad.enabled = false;
            cb.localScale = Vector3.one; cb.localRotation = Quaternion.identity;
            var model = cb.Find("Credit Book Model");
            if (model == null) { model = new GameObject("Credit Book Model").transform; model.SetParent(cb, false); }
            model.gameObject.layer = cb.gameObject.layer;
            var mf = Need<MeshFilter>(model.gameObject);
            var mr = Need<MeshRenderer>(model.gameObject);
            mf.sharedMesh = book; mr.sharedMaterials = bookMats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            model.localPosition = new Vector3(0f, 0f, 0f);
            model.localRotation = Quaternion.Euler(-14f, -24f, -6f);
            model.localScale = Vector3.one * 1.75f;
            if (desc != null) AddToList(desc, "inspectionItemObjects", Elem(cb));
        }
        var gl = itemRoot.Find("Grocery List");
        if (gl != null && desc != null) AddToList(desc, "inspectionItemObjects", Elem(gl));

        var map = itemRoot.Find("Delivery Map");
        if (map == null) { map = new GameObject("Delivery Map").transform; map.SetParent(itemRoot, false); }
        map.gameObject.layer = itemRoot.gameObject.layer;
        map.localPosition = Vector3.zero; map.localRotation = Quaternion.Euler(-10f, 8f, 3f); map.localScale = Vector3.one * 1.25f;
        var mmf = Need<MeshFilter>(map.gameObject);
        var mmr = Need<MeshRenderer>(map.gameObject);
        mmf.sharedMesh = sheet; mmr.sharedMaterials = mapMats; mmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        map.gameObject.SetActive(false);
        AddToList(handler, "inspectionItemObjects", Elem(map));
        if (desc != null) AddToList(desc, "inspectionItemObjects", Elem(map));
    }

    /// <summary>Subtitles fired from a cutscene timeline keep pace with it: shown whole, closed after their time (every other
    /// subtitle now waits for the player). Their time = what they used to stay up for (typing + the old auto close).</summary>
    static void SpokenCutsceneSubtitles(Scene scene)
    {
        var mgr = FindIn<SubtitleManager>(scene);
        float autoClose = 3f;
        if (mgr != null) autoClose = new SerializedObject(mgr).FindProperty("autoDismissDelay").floatValue;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var rc in root.GetComponentsInChildren<SignalReceiver>(true))
                foreach (var sig in rc.GetRegisteredSignals())
                {
                    var ev = rc.GetReaction(sig);
                    if (ev == null) continue;
                    for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                    {
                        if (!(ev.GetPersistentTarget(i) is SubtitleTrigger st) || !ev.GetPersistentMethodName(i).StartsWith("TriggerSubtitle")) continue;
                        var so = new SerializedObject(st);
                        if (so.FindProperty("spokenLine").boolValue) continue;
                        string text = "";
                        try { text = ((LocalizedString)so.FindProperty("subtitleRef").boxedValue).GetLocalizedString(); } catch { }
                        so.FindProperty("spokenLine").boolValue = true;
                        so.FindProperty("lifeTime").floatValue = Mathf.Max(1.5f, text.Length * 0.03f + autoClose);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(st);
                        Debug.Log("[Round4] spoken cutscene line: " + st.name + " '" + text + "' " + so.FindProperty("lifeTime").floatValue.ToString("F1") + " s");
                    }
                }
    }

    // ------------------------------------------------------------------ Store

    static void StoreScene(Scene s, Mesh book, Material[] bookMats, Scene game)
    {
        // dust on every note that can be picked up
        var dustSrc = FindPath(s, "DustEffect", "PainKillerPickup");
        foreach (var p in s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InspectableItemPickup>(true)))
        {
            if (p.itemToPickup == null || p.itemToPickup.itemType != ItemType.Note || p.GetComponentInChildren<ProximityParticle>(true) != null || dustSrc == null) continue;
            var d = Object.Instantiate(dustSrc.gameObject, p.transform);
            d.name = "DustEffect";
            Bounds b = new Bounds(p.transform.position, Vector3.zero); bool any = false;
            foreach (var r in p.GetComponentsInChildren<Renderer>(true)) { if (r.GetComponent<ParticleSystem>() != null) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            d.transform.position = new Vector3(b.center.x, b.max.y + 0.08f, b.center.z);
            Debug.Log("[Round4] dust on note " + p.name);
        }

        // the register's dust (it sits on the old register prop, not under the interactable)
        var register = FindIn<CashRegisterInteractable>(s);
        foreach (var pp in s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ProximityParticle>(true)))
            if (pp.transform.parent != null && pp.transform.parent.name == "cash register")
            { var so = new SerializedObject(pp); so.FindProperty("owner").objectReferenceValue = register; so.ApplyModifiedPropertiesWithoutUndo(); }

        // the bag on the back door is a thrown-bag projectile: its flying whoosh looped all the time near the door
        var bag = FindPath(s, "Lock (1)", "DoorInteractable (BackMain)");
        if (bag != null) foreach (var a in bag.GetComponents<AudioSource>()) { a.playOnAwake = false; a.enabled = false; }

        // fuse box: a red fault lamp (green once the fuse is in)
        var fusebox = FindIn<Fusebox>(s);
        var enclosure = FindPath(s, "Fuse Box Enclosure");
        if (fusebox != null && enclosure != null)
        {
            var old = fusebox.transform.Find("Status Lamp"); if (old != null) Object.DestroyImmediate(old.gameObject);
            Bounds eb = new Bounds(enclosure.position, Vector3.zero); bool any = false;
            foreach (var r in enclosure.GetComponentsInChildren<Renderer>(true)) { if (!any) { eb = r.bounds; any = true; } else eb.Encapsulate(r.bounds); }
            var lamp = new GameObject("Status Lamp").transform;
            lamp.SetParent(fusebox.transform, true);
            Vector3 face = Vector3.left;                                     // the box hangs on the east wall, door to the west
            // on the door's upper corner, where a panel's indicator sits
            var door = enclosure.Find("Fuse Box Enclosure Door");
            Bounds db = door != null && door.GetComponent<Renderer>() != null ? door.GetComponent<Renderer>().bounds : eb;
            lamp.position = new Vector3(db.min.x - 0.002f, db.max.y - 0.07f, db.max.z - 0.07f);
            lamp.rotation = Quaternion.LookRotation(face);
            var bezel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(bezel.GetComponent<Collider>());
            bezel.name = "Bezel"; bezel.transform.SetParent(lamp, false);
            bezel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); bezel.transform.localScale = new Vector3(0.026f, 0.006f, 0.026f); bezel.transform.localPosition = new Vector3(0, 0, 0.006f);
            bezel.GetComponent<MeshRenderer>().sharedMaterial = Mat("RP_LampBezel", null, new Color(0.08f, 0.08f, 0.09f), 0.6f);
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.name = "Lens"; lens.transform.SetParent(lamp, false);
            lens.transform.localScale = new Vector3(0.016f, 0.016f, 0.012f); lens.transform.localPosition = new Vector3(0, 0, 0.012f);
            var lensMat = Mat("RP_LampLens", null, new Color(0.5f, 0.05f, 0.04f), 0.9f);
            lensMat.EnableKeyword("_EMISSION"); lensMat.SetColor("_EmissionColor", new Color(2f, 0.1f, 0.05f)); lensMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            lens.GetComponent<MeshRenderer>().sharedMaterial = lensMat;
            var lg = new GameObject("Glow").AddComponent<Light>();
            lg.transform.SetParent(lamp, false); lg.transform.localPosition = new Vector3(0, 0, 0.07f);
            lg.type = LightType.Point; lg.range = 1.3f; lg.intensity = 1.1f; lg.color = new Color(1f, 0.12f, 0.08f); lg.shadows = LightShadows.None;
            lg.renderMode = LightRenderMode.ForcePixel;
            foreach (var t in lamp.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = fusebox.gameObject.layer;
            var sl = Need<FuseboxStatusLight>(fusebox.gameObject);
            var so = new SerializedObject(sl);
            so.FindProperty("fusebox").objectReferenceValue = fusebox;
            so.FindProperty("lens").objectReferenceValue = lens.GetComponent<MeshRenderer>();
            so.FindProperty("glow").objectReferenceValue = lg;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Round4] fuse box lamp at " + lamp.position + " (enclosure " + eb.min + " .. " + eb.max + ")");
        }

        // the credit book in the drawer: the real book instead of a box
        var prop = FindPath(s, "CreditBookProp");
        if (prop != null)
        {
            var mf = prop.GetComponent<MeshFilter>(); var mr = prop.GetComponent<MeshRenderer>();
            Vector3 c = mr.bounds.center; Quaternion r0 = prop.rotation;
            if (mf.sharedMesh != book)
            {
                mf.sharedMesh = book; mr.sharedMaterials = bookMats;
                prop.rotation = r0 * Quaternion.Euler(90f, 0f, 0f);
                Vector3 ps = prop.parent != null ? prop.parent.lossyScale : Vector3.one;
                prop.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
                prop.position += c - mr.bounds.center;
                var bc = prop.GetComponent<BoxCollider>(); if (bc != null) { bc.center = Vector3.zero; bc.size = new Vector3(0.17f, 0.24f, 0.026f); }
            }
        }

        // delivery map: into the Notes, and the [M] hint once it is closed
        var mapPickup = FindIn<MapPickup>(s);
        var hint = FindIn<MapHintPrompt>(s);
        if (hint == null && game.isLoaded)
        {
            var gameHint = FindIn<MapHintPrompt>(game);
            var gamePrompt = gameHint != null ? new SerializedObject(gameHint).FindProperty("prompt").objectReferenceValue as Component : null;
            var storeCanvas = FindPath(s, "GameCanvas");
            if (gameHint != null && gamePrompt != null && storeCanvas != null)
            {
                var promptCopy = (GameObject)Object.Instantiate(gamePrompt.gameObject, storeCanvas, false);
                promptCopy.name = gamePrompt.name;
                promptCopy.transform.SetSiblingIndex(Mathf.Min(gamePrompt.transform.GetSiblingIndex(), storeCanvas.childCount - 1));
                var hintGo = new GameObject("MapHintPrompt");
                SceneManager.MoveGameObjectToScene(hintGo, s);
                var gp = FindPath(s, "------------Gameplay------------"); if (gp != null) hintGo.transform.SetParent(gp, false);
                hint = hintGo.AddComponent<MapHintPrompt>();
                EditorUtility.CopySerialized(gameHint, hint);
                var so = new SerializedObject(hint);
                so.FindProperty("prompt").objectReferenceValue = promptCopy.GetComponent(gamePrompt.GetType());
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Round4] map hint prompt copied into the Store (" + promptCopy.name + ")");
            }
        }
        if (mapPickup != null)
        {
            var so = new SerializedObject(mapPickup);
            so.FindProperty("notesEntry").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>(MapItem);
            so.FindProperty("mapHint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(s);
    }

    // ------------------------------------------------------------------ Game

    static void GameScene(Scene s)
    {
        // the store behind Khalid goes dead once he is out
        var power = FindIn<StoreInteriorPowerOff>(s);
        if (power == null)
        {
            var go = new GameObject("Store Power (off after the store)");
            SceneManager.MoveGameObjectToScene(go, s);
            var gp = FindPath(s, "------------Gameplay------------"); if (gp != null) go.transform.SetParent(gp, false);
            power = go.AddComponent<StoreInteriorPowerOff>();
        }
        var roots = new List<Transform>();
        var interior = FindPath(s, "Store Interior (from Store scene)"); if (interior != null) roots.Add(interior);
        foreach (var r in s.GetRootGameObjects()) if (r.name == "Store Makeover") roots.Add(r.transform);
        var pso = new SerializedObject(power);
        var arr = pso.FindProperty("roots"); arr.arraySize = roots.Count;
        for (int i = 0; i < roots.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
        pso.ApplyModifiedPropertiesWithoutUndo();

        var cutscene = FindPath(s, "Cutscene_AfterStore");
        if (cutscene != null)
        {
            if (cutscene.GetComponent<PowerOffStoreOnEnable>() == null) cutscene.gameObject.AddComponent<PowerOffStoreOnEnable>();

            // the voice: the gasp and "oh my god" back, "what is that in front of me" gone
            var dir = cutscene.GetComponent<PlayableDirector>();
            var tl = dir != null ? dir.playableAsset as TimelineAsset : null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceClip);
            if (tl != null && clip != null)
                foreach (var tr in tl.GetOutputTracks())
                    foreach (var c in tr.GetClips())
                        if (c.asset is AudioPlayableAsset ap && c.displayName.StartsWith("Voice line 1"))
                        {
                            ap.clip = clip;
                            var so = new SerializedObject(ap);
                            so.FindProperty("m_ClipProperties.volume").floatValue = 1f;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            c.duration = clip.length;
                            c.displayName = "Voice line 1 (gasp + oh my god)";
                            EditorUtility.SetDirty(ap); EditorUtility.SetDirty(tl);
                        }
            // "God protect us..." stays with the voice; it is a spoken line
            var sub = cutscene.Find("Subtitle1");
            if (sub != null)
            {
                var so = new SerializedObject(sub.GetComponent<SubtitleTrigger>());
                so.FindProperty("spokenLine").boolValue = true;
                so.FindProperty("lifeTime").floatValue = 2.8f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorSceneManager.MarkSceneDirty(s);
    }
}
