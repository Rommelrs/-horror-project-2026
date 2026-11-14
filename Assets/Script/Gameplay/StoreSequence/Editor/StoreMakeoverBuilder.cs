using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The grocery store makeover (Nov 2026, from the user's real-baqala photos): white tiled drop ceiling, painted walls,
/// dark sign band with Arabic/English section signs, white shelves, a navy counter with a glass guard and a lift-up flap,
/// flag / cameras / wall AC units, the BAQALA board outside, and the blood trail to the storage room that only shows
/// once the power is back. Layout of the store is unchanged.
///
/// Everything new lives under one root ("Store Makeover"); everything original that was hidden / changed is listed in
/// its StoreMakeoverRecord, so Remove brings the scene back exactly.
/// Build works on the Store scene (offset 0) and on the Game scene's copy of the store (offset = Store->Game offset).
/// </summary>
public static class StoreMakeoverBuilder
{
    const string RootName = "Store Makeover";
    const string TexDir = "Assets/Textures/StoreMakeover/";
    const string MatDir = "Assets/Materials/StoreMakeover/";
    const string MeshAsset = "Assets/Models/StoreMakeover/StoreMakeoverMeshes.asset";
    public static readonly Vector3 GameOffset = new Vector3(-0.769f, 1.921f, -0.002f);

    // ---- store interior in Store-scene coordinates
    // FloorY = the floor you walk on (the marble slab top); the tiled mesh at 1.23 is hidden under it
    const float FloorY = 1.61f, CeilY = 5.62f, BandBot = 4.72f;
    const float XW = -239.96f, XE = -229.51f, ZS = 94.24f, ZN = 106.41f;
    const float DoorX0 = -231.38f, DoorX1 = -229.96f;     // storage door in the south wall
    const float CounterTop = 2.74f;

    static Vector3 off;
    static float southSkinZ = ZS;   // Game scene: the cafe next door has a wall face 0.26 m inside the store
    static Transform root;
    static StoreMakeoverRecord rec;

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/Store Makeover/Build in Store scene")]
    static void BuildStoreMenu()
    {
        var s = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!s.isLoaded) { Debug.LogError("[StoreMakeover] open the Store scene first"); return; }
        Build(s, Vector3.zero, false);
    }

    [MenuItem("Tools/Store Makeover/Build in Game scene (store copy)")]
    static void BuildGameMenu()
    {
        var s = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!s.isLoaded) { Debug.LogError("[StoreMakeover] open the Game scene first"); return; }
        Build(s, GameOffset, true);
    }

    [MenuItem("Tools/Store Makeover/Remove from Game scene")]
    static void RemoveGameMenu()
    {
        var s = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (s.isLoaded) Remove(s);
    }

    [MenuItem("Tools/Store Makeover/Remove from Store scene")]
    static void RemoveStoreMenu()
    {
        var s = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (s.isLoaded) Remove(s);
    }

    // ------------------------------------------------------------------ build / remove

    public static GameObject Build(Scene scene, Vector3 offset, bool gameCopy)
    {
        Remove(scene);
        off = offset;
        southSkinZ = gameCopy ? 94.51f : ZS;
        var go = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(go, scene);
        root = go.transform;
        rec = go.AddComponent<StoreMakeoverRecord>();

        EnsureMaterials();
        BuildCeiling();
        BuildWalls();
        BuildBand();
        BuildSigns();
        BuildCounter(scene);
        BuildProps();
        if (!gameCopy) BuildBloodTrail(scene);   // the Game copy is only ever seen before the store events / from outside
        BuildOutsideBoard(scene);
        HideExtras(scene);
        BuildFloorAndLights();
        BuildFittings(scene, gameCopy);
        if (!gameCopy) BuildStoreroom(scene);     // the Game scene has no storage room
        if (!gameCopy) BuildFuseCabinetLamp(scene);

        // the marble slab under the shop is a baked occluder that reaches above the floor: without this anything lying on the
        // floor (the blood trail) is culled as "hidden"
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.allowOcclusionWhenDynamic = false;

        // Game copy: the store interior there lives on layer 17 ("StoreInteriorLit") so only the store's own lamps light it
        if (gameCopy)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!t.IsChildOf(root.Find("Outside Sign"))) t.gameObject.layer = 17;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[StoreMakeover] built in " + scene.name + " (offset " + offset + ")");
        return go;
    }

    public static void Remove(Scene scene)
    {
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name != RootName) continue;
            var record = r.GetComponent<StoreMakeoverRecord>();
            if (record != null)
            {
                foreach (var rr in scene.GetRootGameObjects())
                    foreach (var tt in rr.GetComponentsInChildren<Transform>(true))
                        if (tt != null && tt.name == BagTieName) Object.DestroyImmediate(tt.gameObject);
                foreach (var x in record.hiddenRenderers) if (x) x.enabled = true;
                foreach (var x in record.disabledColliders) if (x) x.enabled = true;
                foreach (var x in record.deactivatedObjects) if (x) x.SetActive(true);
                foreach (var sw in record.materialSwaps)
                    if (sw.renderer) { var m = sw.renderer.sharedMaterials; if (sw.slot < m.Length) { m[sw.slot] = sw.original; sw.renderer.sharedMaterials = m; } }
                foreach (var rb in record.madeKinematic) if (rb) rb.isKinematic = false;
                foreach (var bc in record.boolChanges)
                {
                    if (bc.component == null) continue;
                    var so = new SerializedObject(bc.component);
                    var sp = so.FindProperty(bc.property);
                    if (sp != null) { sp.boolValue = bc.original; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
                foreach (var rc in record.refChanges)
                {
                    if (rc.component == null) continue;
                    var so = new SerializedObject(rc.component);
                    var sp = so.FindProperty(rc.property);
                    if (sp != null) { sp.objectReferenceValue = rc.original; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
                for (int i = record.reparented.Count - 1; i >= 0; i--)
                {
                    var rp = record.reparented[i];
                    if (rp.target == null) continue;
                    rp.target.SetParent(rp.originalParent, false);
                    rp.target.localPosition = rp.originalLocalPosition;
                }
                for (int i = record.moves.Count - 1; i >= 0; i--) { var mv = record.moves[i]; if (mv.target) { mv.target.position = mv.originalPosition; if (mv.hasRotation) mv.target.rotation = mv.originalRotation; } }
            }
            Object.DestroyImmediate(r);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[StoreMakeover] removed from " + scene.name);
        }
    }

    // ------------------------------------------------------------------ materials

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    static Material Mat(string name, string tex, Color color, float gloss = 0.1f, bool fade = false, bool cutout = false, string shader = "Standard")
    {
        if (mats.TryGetValue(name, out var cached) && cached) return cached;
        System.IO.Directory.CreateDirectory(MatDir);
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find(shader);
        m.color = color;
        if (tex != null) m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (fade)
        {
            m.SetFloat("_Mode", 2f); m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0); m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
        else if (cutout)
        {
            m.SetFloat("_Mode", 1f); m.SetOverrideTag("RenderType", "TransparentCutout");
            m.SetInt("_SrcBlend", 1); m.SetInt("_DstBlend", 0); m.SetInt("_ZWrite", 1);
            m.EnableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.SetFloat("_Cutoff", 0.35f);
            m.renderQueue = 2450;
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static void EnsureMaterials()
    {
        mats.Clear();
        AssetDatabase.Refresh();
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/StoreMakeover" });
        foreach (var gid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(gid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            string n = System.IO.Path.GetFileNameWithoutExtension(path);
            bool alpha = n.StartsWith("blood") || n.StartsWith("decal") || n.StartsWith("crate_") || n == "fit_wire" || n == "fit_basket" || n == "fit_glass_dirty" || n == "fit_frost" || n == "downlight";
            bool clamp = n.StartsWith("sr_wall") || n == "sr_floor" || n == "sr_ceiling" || n.StartsWith("blood") || n.StartsWith("sign_") || n.StartsWith("header") || n.StartsWith("panel") || n == "baqala_board" || n == "floor_room" || n == "floor_storeroom" || n == "products_atlas" || n == "produce_atlas" || n == "ceiling_hole" || n == "downlight" || n == "led_panel";
            int max = (n == "products_atlas" || n == "floor_room" || n.StartsWith("sr_wall")) ? 4096 : 2048;
            var wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (ti.wrapMode != wrap || ti.alphaIsTransparency != alpha || ti.maxTextureSize != max || ti.anisoLevel != 4)
            {
                ti.wrapMode = wrap; ti.alphaIsTransparency = alpha; ti.maxTextureSize = max; ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
        }
    }

    // ------------------------------------------------------------------ meshes (kept as sub-assets so both scenes can share them)

    static Mesh QuadMesh(string name, float w, float h, float uRep, float vRep, float u0 = 0f, float v0 = 0f)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MeshAsset));
        Mesh mesh = null;
        var all = AssetDatabase.LoadAllAssetsAtPath(MeshAsset);
        foreach (var o in all) if (o is Mesh m && m.name == name) mesh = m;
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = name };
        mesh.Clear();
        float hw = w * 0.5f, hh = h * 0.5f;
        mesh.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(-hw, hh, 0), new Vector3(hw, hh, 0), new Vector3(hw, -hh, 0) };
        mesh.uv = new[] { new Vector2(u0, v0), new Vector2(u0, v0 + vRep), new Vector2(u0 + uRep, v0 + vRep), new Vector2(u0 + uRep, v0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
        mesh.RecalculateBounds();
        if (isNew)
        {
            if (AssetDatabase.LoadMainAssetAtPath(MeshAsset) == null) AssetDatabase.CreateAsset(mesh, MeshAsset);
            else AssetDatabase.AddObjectToAsset(mesh, MeshAsset);
        }
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    /// <summary>A quad facing direction n (the side you see it from), centred at c (Store coordinates).</summary>
    static GameObject Quad(string name, Transform parent, Vector3 c, Vector3 n, Vector3 up, float w, float h, Material m,
                           float uRep = 1f, float vRep = 1f, bool shadows = false, string meshKey = null, float u0 = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = c + off;
        go.transform.rotation = Quaternion.LookRotation(-n, up);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = QuadMesh(meshKey ?? (name + "_" + w.ToString("F2") + "x" + h.ToString("F2") + "_" + uRep.ToString("F2") + "x" + vRep.ToString("F2") + (u0 != 0f ? "_o" + u0.ToString("F2") : "")), w, h, uRep, vRep, u0);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// <summary>A box (Unity cube) from min to max corner (Store coordinates), optionally solid.</summary>
    static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = (min + max) * 0.5f + off;
        go.transform.localScale = max - min;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Transform Group(string name) { var g = new GameObject(name).transform; g.SetParent(root, false); return g; }

    // ------------------------------------------------------------------ parts

    static void BuildCeiling()
    {
        var g = Group("Ceiling");
        float w = XE - XW, d = ZN - ZS;
        Quad("DropCeiling", g, new Vector3((XW + XE) * 0.5f, CeilY, (ZS + ZN) * 0.5f), Vector3.down, Vector3.forward, w, d,
             Mat("SM_Ceiling", "ceiling_tiles.png", new Color(0.86f, 0.86f, 0.84f), 0.05f), w / 2.4f, d / 2.4f);
        // tiles that fell out: the dark void above, old pipes and cables
        var hole = Mat("SM_CeilingHole", "ceiling_hole.png", new Color(0.8f, 0.8f, 0.8f), 0.05f);
        foreach (var p in new[] { new Vector2(-231.4f, 104.9f), new Vector2(-237.6f, 99.7f), new Vector2(-233.2f, 96.3f) })
            Quad("Missing Tile", g, new Vector3(p.x, CeilY - 0.004f, p.y), Vector3.down, Vector3.forward, 0.6f, 0.6f, hole, 1f, 1f, false, "CeilHole");
    }

    static void BuildWalls()
    {
        var g = Group("Wall Paint");
        var paint = Mat("SM_WallPaint", "wall_paint.png", Color.white, 0.05f);
        var skirt = Mat("SM_Skirting", "fit_metal_cream.png", new Color(0.34f, 0.33f, 0.31f), 0.15f);
        float h = BandBot - FloorY, cy = (FloorY + BandBot) * 0.5f;
        const float e = 0.008f;
        float uo = 0f;
        void Wall(string n, Vector3 c, Vector3 nrm, float len)
        {
            Quad(n, g, c, nrm, Vector3.up, len, h, paint, len / 7.0f, 1f, false, null, uo);
            uo += 0.37f;
            Quad(n + " Skirting", g, new Vector3(c.x, FloorY + 0.07f, c.z) + nrm * 0.004f, nrm, Vector3.up, len, 0.14f, skirt);
        }
        Wall("North", new Vector3((XW + XE) * 0.5f, cy, ZN - e), Vector3.back, XE - XW);
        Wall("East", new Vector3(XE - e, cy, (ZS + ZN) * 0.5f), Vector3.left, ZN - ZS);
        if (southSkinZ != ZS)   // Game copy: no storage door there, one piece over the cafe wall
            Wall("South", new Vector3((-237.55f + XE) * 0.5f, cy, southSkinZ + e), Vector3.forward, XE + 237.55f);   // starts after the shelves behind the counter
        else
        {
            Wall("South A", new Vector3((XW + DoorX0) * 0.5f, cy, southSkinZ + e), Vector3.forward, DoorX0 - XW);
            Wall("South B", new Vector3((DoorX1 + XE) * 0.5f, cy, southSkinZ + e), Vector3.forward, XE - DoorX1);
        }
    }

    static void BuildBand()
    {
        var g = Group("Sign Band");
        var fascia = Mat("SM_Fascia", "fascia_navy.png", Color.white, 0.25f);
        const float depth = 0.14f;
        float h = CeilY - BandBot, cy = (BandBot + CeilY) * 0.5f;
        void Side(string n, Vector3 wallC, Vector3 nrm, Vector3 along, float len)
        {
            Quad(n, g, wallC + nrm * depth + Vector3.up * (cy - wallC.y), nrm, Vector3.up, len, h, fascia, len / 3.6f, 1f);
            Quad(n + " Lip", g, new Vector3(wallC.x, BandBot, wallC.z) + nrm * (depth * 0.5f), Vector3.down, nrm, len, depth, fascia, len / 3.6f, 0.1f);
        }
        Side("North", new Vector3((XW + XE) * 0.5f, cy, ZN), Vector3.back, Vector3.right, XE - XW);
        Side("South", new Vector3((XW + XE) * 0.5f, cy, ZS), Vector3.forward, Vector3.right, XE - XW);
        Side("East", new Vector3(XE, cy, (ZS + ZN) * 0.5f), Vector3.left, Vector3.forward, ZN - ZS);
        Side("West", new Vector3(XW, cy, (ZS + ZN) * 0.5f), Vector3.right, Vector3.forward, ZN - ZS);
    }

    static void BuildSigns()
    {
        var g = Group("Section Signs");
        const float d = 0.14f + 0.006f, w = 2.3f, h = 0.58f;
        float cy = (BandBot + CeilY) * 0.5f;
        void Sign(string key, Vector3 wallPoint, Vector3 nrm)
        {
            Quad("Sign " + key, g, wallPoint + nrm * d, nrm, Vector3.up, w, h, Mat("SM_Sign_" + key, "sign_" + key + ".png", Color.white, 0.2f), 1f, 1f, false, "Sign_" + w + "x" + h);
        }
        // each sign sits over its section (see BuildFittings)
        Sign("dairy", new Vector3(-238.6f, cy, ZN), Vector3.back);
        Sign("cold_drinks", new Vector3(-233.6f, cy, ZN), Vector3.back);
        Sign("frozen", new Vector3(-231.2f, cy, ZN), Vector3.back);
        Sign("detergents", new Vector3(XE, cy, 103.6f), Vector3.left);
        Sign("fruit_veg", new Vector3(XE, cy, 99.6f), Vector3.left);
        Sign("grocery", new Vector3(-233.2f, cy, southSkinZ), Vector3.forward);
    }

    static void BuildCounter(Scene scene)
    {
        var g = Group("Counter");
        var navy = Mat("SM_CounterNavy", "counter_navy.png", Color.white, 0.3f);
        var top = Mat("SM_CounterTop", "counter_top.png", Color.white, 0.35f);
        var kick = Mat("SM_CounterKick", null, new Color(0.08f, 0.09f, 0.1f), 0.4f);
        var chrome = Mat("SM_Chrome", null, new Color(0.75f, 0.76f, 0.78f), 0.85f);
        var glass = Mat("SM_Glass", null, new Color(0.82f, 0.92f, 0.95f, 0.16f), 0.95f, true);
        if (glass.HasProperty("_Metallic")) glass.SetFloat("_Metallic", 0.1f);

        const float body = CounterTop - 0.05f;
        // front run (register side, customers stand north of it)
        Box("Front Body", g, new Vector3(-239.62f, FloorY, 96.04f), new Vector3(-237.40f, body, 96.70f), navy, true);
        Box("Front Top", g, new Vector3(-239.66f, body, 95.98f), new Vector3(-237.36f, CounterTop, 96.76f), top, false);
        Box("Front Kick", g, new Vector3(-239.60f, FloorY, 96.70f), new Vector3(-237.42f, FloorY + 0.12f, 96.73f), kick, false);
        // the side run: from the south wall (flush against it) to the gap with the flap lifted
        Box("Side Body", g, new Vector3(-237.98f, FloorY, southSkinZ), new Vector3(-237.40f, body, 95.08f), navy, true);
        Box("Side Top", g, new Vector3(-238.02f, body, southSkinZ), new Vector3(-237.36f, CounterTop, 95.12f), top, false);
        Box("Side Kick", g, new Vector3(-237.40f, FloorY, southSkinZ), new Vector3(-237.37f, FloorY + 0.12f, 95.06f), kick, false);
        // the lift-up flap, standing up against the front run (open)
        var flap = Box("Flap (folded back)", g, new Vector3(-238.00f, CounterTop, 96.00f), new Vector3(-237.38f, CounterTop + 0.04f, 96.74f), top, false);
        Box("Flap Hinge", g, new Vector3(-238.00f, CounterTop - 0.02f, 95.94f), new Vector3(-237.38f, CounterTop + 0.02f, 96.00f), chrome, false);
        // glass sneeze-guard on the front run, on four chrome posts
        Box("Guard Glass", g, new Vector3(-239.45f, CounterTop + 0.06f, 96.69f), new Vector3(-237.55f, CounterTop + 0.62f, 96.71f), glass, false)
            .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (float x in new[]{-239.40f, -238.80f, -238.20f, -237.60f})
            Box("Guard Post", g, new Vector3(x - 0.015f, CounterTop, 96.68f), new Vector3(x + 0.015f, CounterTop + 0.64f, 96.72f), chrome, false);
        // a few drops of blood on the counter (the trail itself only shows with the power back)
        var drops = Mat("SM_BloodSmall", "blood_drops_small.png", Color.white, 0.6f, false, true);
        Quad("Blood On Counter", g, new Vector3(-237.72f, CounterTop + 0.002f, 94.86f), Vector3.up, Vector3.forward, 0.35f, 0.35f, drops);
        Quad("Blood On Counter", g, new Vector3(-238.05f, CounterTop + 0.002f, 96.12f), Vector3.up, Vector3.right, 0.28f, 0.28f, drops);

        // hide the old blood-stained desk (the shelves behind it stay) and its collision
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Uncapped_Shelf_small3" && t.parent != null && t.parent.name == "painted desk")
                {
                    foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(true)) if (mr.enabled) { mr.enabled = false; rec.hiddenRenderers.Add(mr); }
                    foreach (var c in t.GetComponentsInChildren<Collider>(true)) if (c.enabled) { c.enabled = false; rec.disabledColliders.Add(c); }
                }
                if (t.name == "COUNTER" && t.gameObject.activeInHierarchy && Vector3.Distance(t.position, new Vector3(-237.4f, 2f, 96.2f) + off) < 3f)
                    foreach (var c in t.GetComponents<Collider>()) if (c.enabled) { c.enabled = false; rec.disabledColliders.Add(c); }
            }
    }

    static void BuildProps()
    {
        var g = Group("Props");
        var white = Mat("SM_PlasticWhite", null, new Color(0.9f, 0.9f, 0.88f), 0.35f);
        var dark = Mat("SM_DarkPlastic", null, new Color(0.08f, 0.08f, 0.09f), 0.6f);
        var chrome = Mat("SM_Chrome", null, new Color(0.75f, 0.76f, 0.78f), 0.85f);
        // split AC units under the sign band
        void AC(string n, Vector3 c, Vector3 nrm, Vector3 along)
        {
            var a = Box(n, g, Vector3.zero, Vector3.zero, white, false);
            a.transform.position = c + nrm * 0.17f + off;
            a.transform.rotation = Quaternion.LookRotation(nrm, Vector3.up);
            a.transform.localScale = new Vector3(1.25f, 0.36f, 0.32f);
            var vent = Box(n + " Vent", g, Vector3.zero, Vector3.zero, dark, false);
            vent.transform.position = c + nrm * 0.33f + Vector3.down * 0.12f + off;
            vent.transform.rotation = a.transform.rotation;
            vent.transform.localScale = new Vector3(1.1f, 0.05f, 0.02f);
        }
        AC("Wall AC", new Vector3(-238.9f, 4.30f, ZN), Vector3.back, Vector3.right);
        AC("Wall AC", new Vector3(-233.3f, 4.30f, ZS), Vector3.forward, Vector3.right);
        // ceiling security cameras
        void Cam(Vector3 c)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(b.GetComponent<Collider>());
            b.name = "CCTV Base"; b.transform.SetParent(g, false); b.transform.position = c + off; b.transform.localScale = new Vector3(0.2f, 0.02f, 0.2f);
            b.GetComponent<MeshRenderer>().sharedMaterial = white;
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(dome.GetComponent<Collider>());
            dome.name = "CCTV Dome"; dome.transform.SetParent(g, false); dome.transform.position = c + Vector3.down * 0.05f + off; dome.transform.localScale = new Vector3(0.16f, 0.12f, 0.16f);
            dome.GetComponent<MeshRenderer>().sharedMaterial = dark;
        }
        // the Game copy has no pickups: show the delivery map on the wall like the Store scene does
        if (southSkinZ != ZS)
        {
            var mapMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DeliveryMap_Mat.mat");
            if (mapMat != null) Quad("Delivery Map (static)", g, new Vector3(-236.7f, 3.55f, southSkinZ + 0.02f), Vector3.forward, Vector3.up, 1.05f, 0.59f, mapMat, 1f, 1f, false, "DeliveryMapStatic");
        }
        Cam(new Vector3(-239.2f, CeilY - 0.02f, 105.6f));
        Cam(new Vector3(-230.3f, CeilY - 0.02f, 95.1f));
        Cam(new Vector3(-238.6f, CeilY - 0.02f, 97.4f));
    }

    static void BuildBloodTrail(Scene scene)
    {
        var holder = Group("Blood Trail (shows with power)");
        var trail = new GameObject("Trail").transform; trail.SetParent(holder, false);
        var small = Mat("SM_BloodSmall", "blood_drops_small.png", Color.white, 0.6f, false, true);
        var med = Mat("SM_BloodMed", "blood_drops_med.png", Color.white, 0.6f, false, true);
        var big = Mat("SM_BloodBig", "blood_drop_big.png", Color.white, 0.6f, false, true);
        // from the counter gap, past the cabinet, along the south wall to the storage door; drops get bigger near the door
        Vector3 a = new Vector3(-237.2f, FloorY, 95.45f), b = new Vector3(-235.6f, FloorY, 95.25f), c = new Vector3(-232.6f, FloorY, 95.1f), d = new Vector3(-230.7f, FloorY, 94.6f);
        var rnd = new System.Random(94);
        int n = 16;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 p = t < 0.33f ? Vector3.Lerp(a, b, t / 0.33f) : t < 0.8f ? Vector3.Lerp(b, c, (t - 0.33f) / 0.47f) : Vector3.Lerp(c, d, (t - 0.8f) / 0.2f);
            p += new Vector3((float)(rnd.NextDouble() - 0.5) * 0.25f, 0.016f + i * 0.0006f, (float)(rnd.NextDouble() - 0.5) * 0.2f);
            float size = Mathf.Lerp(0.22f, 0.62f, t * t);
            var m = t < 0.4f ? small : t < 0.85f ? med : big;
            Quad("Drop", trail, p, Vector3.up, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0) * Vector3.forward, size, size, m, 1f, 1f, false, "Drop_" + size.ToString("F2"));
        }
        // a smear at the foot of the storage door
        Quad("Door Smear", trail, new Vector3(-230.65f, FloorY + 0.03f, 94.42f), Vector3.up, Vector3.right, 0.9f, 0.5f, big, 1f, 1f, false, "Smear");
        var show = holder.gameObject.AddComponent<ShowWhenPowered>();
        Fusebox fb = null;
        foreach (var r in scene.GetRootGameObjects()) { var f = r.GetComponentInChildren<Fusebox>(true); if (f) fb = f; }
        var so = new SerializedObject(show);
        so.FindProperty("fusebox").objectReferenceValue = fb;
        so.FindProperty("target").objectReferenceValue = trail.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildOutsideBoard(Scene scene)
    {
        var g = Group("Outside Sign");
        var board = Mat("SM_BaqalaBoard", "baqala_board.png", Color.white, 0.3f);
        // the black board like the tailor's next door, over both arches
        Quad("BAQALA Board", g, new Vector3(-240.19f, 8.15f, 100.8f), Vector3.left, Vector3.up, 7.2f, 1.8f, board, 1f, 1f, true, "BaqalaBoard");
        var frame = Mat("SM_DarkPlastic", null, new Color(0.08f, 0.08f, 0.09f), 0.6f);
        Box("Board Back", g, new Vector3(-240.17f, 7.25f, 97.2f), new Vector3(-240.06f, 9.05f, 104.4f), frame, false);
        // hide the old DUKAN / دكان letters on the facade
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
                if ((t.name == "DUKAN TEXT" || (t.name == "typeMesh1" && t.parent != null && t.parent.name == "DUKAN")) && t.position.x < -239.5f)
                    foreach (var mr in t.GetComponents<MeshRenderer>()) if (mr.enabled) { mr.enabled = false; rec.hiddenRenderers.Add(mr); }
    }

    static void BuildFloorAndLights()
    {
        var g = Group("Floor And Ceiling Lights");
        float w = XE - XW, d = ZN - ZS;
        Quad("Floor Tiles", g, new Vector3((XW + XE) * 0.5f, FloorY + 0.012f, (ZS + ZN) * 0.5f), Vector3.up, Vector3.forward, w, d,
             Mat("SM_FloorRoom", "floor_room.png", new Color(0.8f, 0.79f, 0.76f), 0.28f), 1f, 1f);
        var dl = Mat("SM_Downlight", "downlight.png", Color.white, 0.3f, false, true);
        var led = Mat("SM_LedPanel", "led_panel.png", Color.white, 0.3f);
        foreach (float x in new[] { -238.6f, -234.25f, -230.4f })
            foreach (float z in new[] { 97.3f, 100.8f, 104.3f })
                Quad("Downlight", g, new Vector3(x, CeilY - 0.006f, z), Vector3.down, Vector3.forward, 0.32f, 0.32f, dl, 1f, 1f, false, "Downlight");
        foreach (var p in new[] { new Vector2(-234.25f, 95.6f), new Vector2(-234.25f, 106.0f), new Vector2(-238.6f, 100.8f) })
            Quad("LED Panel", g, new Vector3(p.x, CeilY - 0.005f, p.y), Vector3.down, Vector3.forward, 0.84f, 0.84f, led, 1f, 1f, false, "LedPanel");
    }

    static Mesh SaveFittingMesh(Mesh m, string name, string assetPath)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(assetPath));
        Mesh existing = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath)) if (o is Mesh mm && mm.name == name) existing = mm;
        if (existing != null)
        {
            existing.Clear();
            existing.indexFormat = m.indexFormat;
            existing.SetVertices(m.vertices); existing.SetNormals(m.normals); existing.SetUVs(0, m.uv); existing.SetTriangles(m.triangles, 0);
            existing.RecalculateBounds();
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null) AssetDatabase.CreateAsset(m, assetPath);
        else AssetDatabase.AddObjectToAsset(m, assetPath);
        return m;
    }

    static void BuildFittings(Scene scene, bool gameCopy)
    {
        var g = Group("Fittings");
        string meshPath = "Assets/Models/StoreMakeover/StoreFittings_" + (gameCopy ? "Game" : "Store") + ".asset";
        StoreFittings.off = off;
        StoreFittings.Mat = (n, t, c, gl, fade, cut) => Mat(n, t, c, gl, fade, cut);
        StoreFittings.SaveMesh = (m, n) => SaveFittingMesh(m, n, meshPath);
        StoreFittings.Init(77);
        StoreFittings.keepClear.Clear();
        var shop = new Bounds(new Vector3((XW + XE) * 0.5f, 3.5f, (ZS + ZN) * 0.5f) + off, new Vector3(XE - XW, 5f, ZN - ZS));
        var pickups = new List<Transform>();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var mb in r.GetComponentsInChildren<MonoBehaviour>(true))
                if ((mb is InspectableItemPickup || mb is ItemPickup) && shop.Contains(mb.transform.position) && !pickups.Contains(mb.transform)) pickups.Add(mb.transform);
        foreach (var p in pickups) StoreFittings.keepClear.Add(p.position - off);

        string[] S(params string[] a) => a;
        // ---- centre: three islands, aisles run from the entrance side to the produce wall
        StoreFittings.Gondola(g, "Island North", -235.7f, -232.0f, 103.3f,
            new[] { S("cereal"), S("biscuits", "baking"), S("noodles", "pasta"), S("tea", "coffee", "spreads"), S("tubes", "cereal") },
            new[] { S("oil", "vinegar"), S("canned", "tomato"), S("tuna", "evap", "canned"), S("sauces", "syrup"), S("milkpowder", "salt", "spreads") },
            S("chips", "chips_big"), S("water_small", "soda_small"));
        StoreFittings.Gondola(g, "Island Middle", -235.7f, -232.0f, 100.6f,
            new[] { S("rice_small", "sugar"), S("sugar", "pasta"), S("juice_carton", "milk_carton"), S("juice_small", "malt", "juice_can"), S("water_small", "soda_small") },
            new[] { S("chips_big"), S("chips"), S("chips"), S("chips", "nuts"), S("tubes", "nuts") },
            S("chips", "chips_big"), S("biscuits", "baking"));
        StoreFittings.Gondola(g, "Island South", -235.7f, -232.0f, 97.9f,
            new[] { S("det_jug", "softener"), S("det_powder", "bleach"), S("dish", "insect"), S("shampoo", "soap", "toothpaste"), S("tissue") },
            new[] { S("soda_big"), S("water_big"), S("juice_carton"), S("soda_can", "juice_can", "energy"), S("biscuits", "tea") },
            S("nuts", "chips"), S("noodles", "pasta"));
        // ---- north wall: dairy chiller | water packs | (vending machine stays) | drinks fridge | chest freezer
        StoreFittings.DairyChiller(g, "Dairy Chiller", new Vector3(-239.8f, 0, ZN), Vector3.right, Vector3.back, 2.4f,
            new[] { S("dairy_jug", "dairy_big"), S("dairy_big", "juice_bottle", "milk_carton"), S("dairy_small", "yogurt"), S("cheese", "cheese_box", "butter", "cheese_flat", "eggs") });
        StoreFittings.FloorStack(g, "Water Packs", new Vector3(-237.32f, 0, ZN - 0.03f), Vector3.right, Vector3.back, S("pack_waha", "pack_mai"), 2, 2, 3);
        StoreFittings.VendingSnack(g, "Snack Vending Machine", new Vector3(-236.64f, 0, ZN), Vector3.right, Vector3.back);
        StoreFittings.GlassFridge(g, "Juice Fridge", new Vector3(-235.52f, 0, ZN), Vector3.right, Vector3.back, 1, "fit_fridge_red", "header_rayan",
            new[] { S("juice_bottle", "water_big"), S("juice_carton", "juice_bottle"), S("juice_small", "juice_can"), S("energy", "malt"), S("juice_can", "water_small") });
        StoreFittings.GlassFridge(g, "Drinks Fridge", new Vector3(-234.62f, 0, ZN), Vector3.right, Vector3.back, 3, "fit_fridge_blue", "header_cola",
            new[] { S("soda_big", "water_big"), S("soda_small", "juice_bottle", "water_small"), S("soda_can"), S("juice_can", "energy", "malt"), S("soda_can", "water_small") });
        StoreFittings.ChestFreezer(g, "Chest Freezer", new Vector3(-232.2f, 0, ZN), Vector3.right, Vector3.back, 1.9f, S("frozen", "icecream", "frozen_box"));
        // ---- east wall: detergents (north end), fruit & vegetables (the fuse box end stays clear)
        StoreFittings.WallShelf(g, "Detergent Shelf", new Vector3(XE, 0, 105.0f), new Vector3(XE, 0, 102.2f), Vector3.left,
            new[] { S("det_jug", "det_bag"), S("det_powder", "softener"), S("bleach", "dish"), S("insect", "shampoo"), S("tissue", "soap") });
        StoreFittings.ProduceRack(g, "Produce Rack", new Vector3(XE, 0, 101.7f), Vector3.back, Vector3.left, 4.2f);
        // ---- south wall: dry goods next to the vending machine (the storage door stays clear)
        StoreFittings.VendingCola(g, "Cola Vending Machine", new Vector3(-234.98f, 0, southSkinZ), Vector3.left, Vector3.forward);
        StoreFittings.WallShelf(g, "Grocery Shelf", new Vector3(-234.88f, 0, southSkinZ), new Vector3(-231.6f, 0, southSkinZ), Vector3.forward,
            new[] { S("rice_small", "sugar"), S("oil"), S("canned", "tomato", "evap"), S("tea", "coffee", "spreads"), S("milkpowder", "cereal") });
        // ---- west wall, on the pillar between the two shop windows: the chocolate fridge, cartons beside it, water-cooler bottles
        StoreFittings.GlassFridge(g, "Chocolate Fridge", new Vector3(-239.94f, 0, 99.84f), Vector3.forward, Vector3.right, 1, "fit_fridge_white", "header_choco",
            new[] { S("candy"), S("choc_egg"), S("candy"), S("choc_box", "candy"), S("candy") });
        StoreFittings.FloorStack(g, "Cartons", new Vector3(-239.93f, 0, 98.92f), Vector3.forward, Vector3.right, S("carton_cola", "carton_laziz", "carton_qaws", "carton_vg", "carton_indomix"), 2, 1, 3);
        StoreFittings.FloorItems(g, "Water Bottles", new[] { new Vector3(-239.76f, 0, 100.88f), new Vector3(-239.45f, 0, 100.86f), new Vector3(-239.74f, 0, 101.18f) }, Vector3.right, S("gallon"));
        // spare stock kept on top of the fridges, like every corner shop
        StoreFittings.FloorStack(g, "Cartons On Fridge", new Vector3(-234.5f, 0, ZN - 0.06f), Vector3.right, Vector3.back, S("carton_cola", "carton_vg", "carton_cola", "carton_qaws"), 4, 1, 1, false, 2.26f);
        StoreFittings.FloorStack(g, "Cartons On Chiller", new Vector3(-239.55f, 0, ZN - 0.06f), Vector3.right, Vector3.back, S("carton_qaws", "carton_indomix", "carton_laziz"), 4, 1, 1, false, 2.0f);
        // ---- the sweets on the customer side of the counter
        StoreFittings.CounterSweets(g, "Counter Sweets", new Vector3(-239.5f, 0, 96.73f), new Vector3(-237.5f, 0, 96.73f), Vector3.forward, new[] { 0.28f, 0.5f, 0.72f });

        // ---- behind the counter: cigarettes, batteries, medicine, phone cards (replaces the old black shelving)
        StoreFittings.WallShelf(g, "Back Shelf South", new Vector3(-239.94f, 0, southSkinZ), new Vector3(-238.04f, 0, southSkinZ), Vector3.forward,
            new[] { S("water_small", "tissue"), S("battery", "pharma", "counter_misc"), S("cig"), S("cig"), S("cig_carton") });
        StoreFittings.WallShelf(g, "Back Shelf West", new Vector3(XW, 0, 96.02f), new Vector3(XW, 0, southSkinZ + 0.5f), Vector3.right,
            new[] { S("soda_can", "energy"), S("counter_misc", "candy"), S("cig", "pharma"), S("cig", "battery"), S("cig_carton", "tissue") });
        BuildLooseProps(scene, gameCopy);

        // nothing may stand inside anything else
        for (int i = 0; i < StoreFittings.fixtureBounds.Count; i++)
            for (int j = i + 1; j < StoreFittings.fixtureBounds.Count; j++)
            {
                var bi = StoreFittings.fixtureBounds[i]; bi.Expand(-0.02f);
                if (bi.Intersects(StoreFittings.fixtureBounds[j])) Debug.LogWarning("[StoreMakeover] OVERLAP: " + StoreFittings.fixtureNames[i] + " / " + StoreFittings.fixtureNames[j]);
            }
        NudgeLooseProps(scene, shop);
        MoveFridgeHums(g);
        Debug.Log("[StoreMakeover] fittings: " + StoreFittings.triCount + " triangles");

        // the fuse drawer: the cashier cabinet now stands behind the counter
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "CashierCabinet" || !shop.Contains(t.position + Vector3.up)) continue;
                Transform body = t.Find("Cabinet_Body");
                Renderer br = body != null ? body.GetComponent<Renderer>() : null;
                Vector3 bc = br != null ? br.bounds.center : t.position;
                rec.moves.Add(new StoreMakeoverRecord.Move { target = t, originalPosition = t.position, hasRotation = true, originalRotation = t.rotation });
                // against the south wall under the delivery map, right beside the counter flap (where the user approved it)
                Vector3 target = new Vector3(-236.67f, 0f, southSkinZ + 0.33f) + off; target.y = bc.y;
                Vector3 nbc = br != null ? br.bounds.center : t.position;
                t.position += target - nbc;
            }

        Physics.SyncTransforms();
        if (!gameCopy)
        {
            // pickups that stood on the old shelves come down onto the new ones; the Training Note goes onto the counter (doc)
            foreach (var p in pickups)
            {
                // the Training Note stays where it always was (on the freezer, north-east corner): the generic rule below just
                // seats it on the new chest freezer's lid. Not on the counter - next to the fuse drawer it made the fuse a 2-second grab.
                float h = p.position.y - off.y - FloorY;
                if (h < 0.25f) continue;
                // inside one of the new fittings: put it on the shelf level it was nearest (or on top of a low freezer)
                Bounds? inside = null;
                foreach (var fb in StoreFittings.fixtureBounds)
                    if (fb.min.y < off.y + FloorY + 0.5f && p.position.x > fb.min.x - 0.12f && p.position.x < fb.max.x + 0.12f && p.position.z > fb.min.z - 0.12f && p.position.z < fb.max.z + 0.12f) inside = fb;
                if (inside.HasValue)
                {
                    float y;
                    if (inside.Value.size.y < 1.2f) y = inside.Value.max.y + 0.085f;
                    else
                    {
                        float lvl = StoreFittings.Levels[0];
                        foreach (float L in StoreFittings.Levels) if (L <= h + 0.05f) lvl = L;
                        y = off.y + FloorY + lvl + 0.02f;
                    }
                    rec.moves.Add(new StoreMakeoverRecord.Move { target = p, originalPosition = p.position });
                    var ib = inside.Value;
                    float px = p.position.x, pz = p.position.z;
                    if (ib.size.y > 1.2f)
                    {
                        if (ib.size.x > ib.size.z) px = Mathf.Clamp(px, ib.min.x + 0.17f, ib.max.x - 0.17f);
                        else pz = Mathf.Clamp(pz, ib.min.z + 0.17f, ib.max.z - 0.17f);
                    }
                    p.position = new Vector3(px, y, pz);
                    foreach (var rb in p.GetComponentsInChildren<Rigidbody>())
                        if (!rb.isKinematic) { rb.isKinematic = true; rec.madeKinematic.Add(rb); }
                    continue;
                }
                if (Physics.Raycast(p.position + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) && p.position.y - hit.point.y > 0.03f)
                {
                    rec.moves.Add(new StoreMakeoverRecord.Move { target = p, originalPosition = p.position });
                    p.position = new Vector3(p.position.x, hit.point.y + 0.03f, p.position.z);
                }
            }
        }
    }

    /// <summary>
    /// The old fridges/freezer are switched off, but their hums are part of the blackout (they wind down when the power dies, come
    /// back with the fuse). Move each hum object onto the nearest new cold fitting so the new fridges hum instead.
    /// </summary>
    static void MoveFridgeHums(Transform fittings)
    {
        var cold = new List<Transform>();
        foreach (Transform t in fittings)
            if (t.name.Contains("Fridge") || t.name.Contains("Freezer") || t.name.Contains("Chiller") || t.name.Contains("Vending")) cold.Add(t);
        if (cold.Count == 0) return;
        foreach (var go in rec.deactivatedObjects)
        {
            if (go == null) continue;
            foreach (var hum in go.GetComponentsInChildren<FridgeHum>(true))
            {
                Transform best = null; float bd = float.MaxValue;
                foreach (var c in cold) { float d = (c.position - hum.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
                rec.reparented.Add(new StoreMakeoverRecord.Reparent { target = hum.transform, originalParent = hum.transform.parent, originalLocalPosition = hum.transform.localPosition });
                var bc = best.GetComponent<BoxCollider>();
                float low = bc != null ? best.position.y - bc.size.y * 0.5f + 0.3f : best.position.y;
                hum.transform.SetParent(best, true);
                hum.transform.position = new Vector3(best.position.x, low, best.position.z);
                Debug.Log("[StoreMakeover] hum from '" + go.name + "' now on '" + best.name + "'");
            }
        }
    }

    /// <summary>Old loose props (carts, boxes, the wet-floor sign...) that now stand inside a new fitting are pushed out of it.</summary>
    static void NudgeLooseProps(Scene scene, Bounds shop)
    {
        var names = new List<string> { "SHOPPIING CART", "SHOPPIING CART (1)", "CardBoard Box", "CardBoard Box (1)", "wet floor board", "broom", "blue crate", "cooler",
            "Cleaning supplies", "red bucketrev", "broken chair" };
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
            {
                bool meshy = t.name.StartsWith("Meshy_AI_") && !t.name.StartsWith("Meshy_AI_Weathered");
                if (!names.Contains(t.name) && !meshy) continue;
                if (t.IsChildOf(root) || !shop.Contains(t.position + Vector3.up)) continue;
                var rs = t.GetComponentsInChildren<Renderer>(false);
                if (rs.Length == 0) continue;
                Vector3 start = t.position;
                for (int pass = 0; pass < 6; pass++)
                {
                    Bounds b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                    bool hit = false;
                    for (int i = 0; i < StoreFittings.fixtureBounds.Count; i++)
                    {
                        var f = StoreFittings.fixtureBounds[i]; f.Expand(0.06f);
                        if (!f.Intersects(b)) continue;
                        // shortest way out in x or z
                        float px = b.center.x > f.center.x ? f.max.x - b.min.x : f.min.x - b.max.x;
                        float pz = b.center.z > f.center.z ? f.max.z - b.min.z : f.min.z - b.max.z;
                        t.position += Mathf.Abs(px) < Mathf.Abs(pz) ? new Vector3(px, 0, 0) : new Vector3(0, 0, pz);
                        hit = true; break;
                    }
                    if (!hit) break;
                }
                if ((t.position - start).sqrMagnitude > 1e-6f)
                {
                    rec.moves.Add(new StoreMakeoverRecord.Move { target = t, originalPosition = start });
                    Debug.Log("[StoreMakeover] moved '" + t.name + "' out of a fitting by " + (t.position - start));
                }
            }
    }

    static void RestyleShelves(Scene scene)
    {
        var shelfWhite = Mat("SM_ShelfWhite", null, new Color(0.88f, 0.88f, 0.86f), 0.35f);
        var area = new Bounds(new Vector3(-234.7f, 3f, 100.4f) + off, new Vector3(10.6f, 5f, 12.4f));
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
            {
                if (!(t.name == "Shelf 1" || t.name == "Shelf 2" || t.name == "Shelf 3")) continue;
                foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!area.Contains(mr.bounds.center)) continue;
                    var ms = mr.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < ms.Length; i++)
                        if (ms[i] != null && ms[i].name == "blinn1" && ms[i].color.maxColorComponent < 0.05f)
                        {
                            rec.materialSwaps.Add(new StoreMakeoverRecord.MaterialSwap { renderer = mr, slot = i, original = ms[i] });
                            ms[i] = shelfWhite; changed = true;
                        }
                    if (changed) mr.sharedMaterials = ms;
                }
            }
    }

    // ------------------------------------------------------------------ storage room (Store scene only)
    const float SX0 = -221.96f, SX1 = -211.54f, SZ0 = 86.36f, SZ1 = 93.92f, SFloor = 1.445f, SCeil = 5.0f;

    /// <summary>
    /// The storage room behind the shop, rebuilt: painted walls, concrete floor, tile ceiling with tube lights, steel doors,
    /// stock racking on three walls, pallets, the packing table with the notice board. The old room kit is hidden.
    /// </summary>
    static void BuildStoreroom(Scene scene)
    {
        var g = Group("Storeroom");
        string[] S(params string[] a) => a;
        var furnitureColliders = new List<string> { "Collider1 (11)", "Collider1 (12)", "Collider1 (13)", "Collider1 (14)", "Collider1 (15)", "Collider1 (16)", "Collider1 (17)", "Collider1 (18)", "Collider1 (19)" };
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null || t.parent.name != "StorageRoom") continue;
                if (t.name == "STORAGE ROOM1" && t.gameObject.activeInHierarchy)
                {
                    foreach (var mr in t.GetComponentsInChildren<Renderer>(false)) if (mr.enabled) { mr.enabled = false; rec.hiddenRenderers.Add(mr); }
                    foreach (var c in t.GetComponentsInChildren<Collider>(false)) if (c.enabled) { c.enabled = false; rec.disabledColliders.Add(c); }
                }
                if (t.name == "StorageRoomColliders")
                    foreach (Transform c in t)
                        if (furnitureColliders.Contains(c.name))
                            foreach (var col in c.GetComponents<Collider>()) if (col.enabled) { col.enabled = false; rec.disabledColliders.Add(col); }
                if (t.name == "CardboardBoxGroup" && t.gameObject.activeSelf) { t.gameObject.SetActive(false); rec.deactivatedObjects.Add(t.gameObject); }
            }
        // The blackout's crash still runs on the old (now hidden) shelf so its timing and sounds stay; it must not leave
        // invisible solid boxes in the new room, and the placeholder "gap" in the wall is gone. What fell is built below.
        foreach (var r in scene.GetRootGameObjects())
        {
            foreach (var sc in r.GetComponentsInChildren<StorageCollapse>(true))
            {
                var so = new SerializedObject(sc);
                foreach (string prop in new[] { "addShelfCollider", "addItemColliders" })
                {
                    var sp = so.FindProperty(prop);
                    if (sp == null || !sp.boolValue) continue;
                    rec.boolChanges.Add(new StoreMakeoverRecord.BoolChange { component = sc, property = prop, original = true });
                    sp.boolValue = false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                var gap = so.FindProperty("gap").objectReferenceValue as GameObject;
                if (gap != null)
                    foreach (var mr in gap.GetComponentsInChildren<Renderer>(true)) if (mr.enabled) { mr.enabled = false; rec.hiddenRenderers.Add(mr); }
            }
        }

        // ---- shell
        float w = SX1 - SX0, d = SZ1 - SZ0, h = SCeil - SFloor, cx = (SX0 + SX1) * 0.5f, cz = (SZ0 + SZ1) * 0.5f;
        BuildStoreroomShell(g);

        // ---- fittings (StoreFittings measures heights from its FloorY: shift it onto this room's floor)
        StoreFittings.off = new Vector3(0f, SFloor - StoreFittings.FloorY, 0f);
        float Y(float aboveFloor) => StoreFittings.FloorY + aboveFloor;
        foreach (float lx in new[] { -219.49f, -216.98f, -214.47f })
            StoreFittings.TubeLight(g, "Tube Light", new Vector3(lx, Y(h - 0.04f), 90.34f), Vector3.forward, 1.3f);
        BuildStoreroomDoors(scene, g);
        float[] L4 = { 0.12f, 0.78f, 1.42f, 2.02f };
        StoreFittings.keepClear.Add(new Vector3(-219.6f, Y(1.42f), SZ0 + 0.35f));          // the slot the cashier keeps filling in the cutscene
        StoreFittings.Racking(g, "Rack South West", new Vector3(-221.75f, 0, SZ0 + 0.03f), new Vector3(-217.55f, 0, SZ0 + 0.03f), Vector3.forward, L4,
            new[] { S("carton_box"), S("water_pack"), S("carton_box"), S("tissue", "det_powder") });
        StoreFittings.Racking(g, "Rack West", new Vector3(SX0 + 0.03f, 0, 92.75f), new Vector3(SX0 + 0.03f, 0, 87.25f), Vector3.right, L4,
            new[] { S("rice", "charcoal", "produce_sack"), S("oil", "sugar"), S("carton_box"), S("carton_box", "tissue") });
        StoreFittings.Racking(g, "Rack North West", new Vector3(-219.2f, 0, SZ1 - 0.03f), new Vector3(-221.75f, 0, SZ1 - 0.03f), Vector3.back, L4,
            new[] { S("gallon"), S("soda_big", "water_big"), S("carton_box"), S("carton_box") });
        float[] LT = { 0.12f, 0.92f, 1.56f, 2.1f };
        StoreFittings.Racking(g, "Rack Tools", new Vector3(-214.75f, 0, SZ1 - 0.03f), new Vector3(-217.15f, 0, SZ1 - 0.03f), Vector3.back, LT,
            new[] { S("carton_box"), null, S("water_pack"), S("carton_box") });
        StoreFittings.ShelfTools(g, "Shelf Tools", new Vector3(-217.05f, 0, SZ1 - 0.63f), Vector3.right, Vector3.back, 0.92f);
        StoreFittings.Racking(g, "Rack North East", new Vector3(-211.75f, 0, SZ1 - 0.03f), new Vector3(-214.55f, 0, SZ1 - 0.03f), Vector3.back, L4,
            new[] { S("water_pack"), S("carton_box"), S("det_powder", "det_bag"), S("tissue") });
        StoreFittings.WorkTable(g, "Packing Table", new Vector3(SX1 - 0.03f, 0, 91.5f), new Vector3(SX1 - 0.03f, 0, 88.7f), Vector3.left, 0.8f, 0.9f);
        StoreFittings.WallPanel(g, "Notice Board", new Vector3(SX1 - 0.05f, Y(1.78f), 90.1f), Vector3.left, 1.3f, 0.98f, "panel_noticeboard", 0.04f, 0.1f);
        StoreFittings.Pallet(g, "Pallet Water", new Vector3(-213.6f, 0, 87.05f), 0f, S("pack_waha", "pack_mai"), 4, 5, 4);
        StoreFittings.Pallet(g, "Pallet Cartons", new Vector3(-212.25f, 0, 87.0f), 0f, S("carton_cola", "carton_vg", "carton_laziz"), 3, 3, 3);
        StoreFittings.Pallet(g, "Pallet Half Empty", new Vector3(-214.4f, 0, 90.7f), 20f, S("carton_qaws", "carton_indomix"), 3, 3, 2, 0.55f);
        // what came down in the crash during the blackout: stock off the north-east racking, all over the floor
        StoreFittings.Spill(g, "Fallen Stock", new[] {
            ("carton_cola", -213.35f, 92.55f, 25f, 'f'), ("carton_laziz", -212.75f, 92.2f, 110f, 'u'), ("carton_vg", -213.9f, 92.05f, 200f, 'f'), ("carton_qaws", -212.4f, 92.75f, 300f, 'u'),
            ("pack_waha", -213.1f, 91.75f, 60f, 'f'), ("pack_mai", -214.2f, 92.75f, 150f, 'u'), ("tidal", -212.95f, 91.4f, 80f, 'f'), ("perzil", -213.6f, 91.55f, 250f, 'f'),
            ("fino_blue", -212.3f, 91.85f, 10f, 'f'), ("fino_pink", -214.0f, 91.35f, 130f, 'f'), ("pet_cola", -213.45f, 91.1f, 40f, 's'), ("water_waha", -212.6f, 91.15f, 290f, 's') });
        StoreFittings.FloorStack(g, "Cartons To Shelve", new Vector3(-218.75f, 0, 87.3f), Vector3.right, Vector3.forward, S("carton_cola", "carton_qaws"), 2, 1, 2);
        BuildStoreroomDecay(g);
        StoreFittings.off = off;

        // ---- what lay on the old work bench moves onto the packing table; the hammer waits on the tool shelf
        float top = SFloor + 0.9f;
        var targets = new Dictionary<string, Vector3> {
            { "Note2Pickup", new Vector3(SX1 - 0.42f, top + 0.02f, 90.3f) }, { "PainKillerPickup (2)", new Vector3(SX1 - 0.45f, top + 0.12f, 89.2f) },
            { "Ammo 2 (1)", new Vector3(SX1 - 0.4f, top + 0.02f, 91.1f) }, { "ItemPickup (Hammer)", new Vector3(-215.15f, SFloor + 0.95f, SZ1 - 0.42f) } };
        var room = new Bounds(new Vector3(cx, 3f, cz), new Vector3(w, 6f, d));
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (!targets.ContainsKey(t.name) || !room.Contains(t.position)) continue;
                if (t.GetComponent<Interactable>() == null && t.GetComponent<InspectableItemPickup>() == null) continue;
                rec.moves.Add(new StoreMakeoverRecord.Move { target = t, originalPosition = t.position });
                t.position = targets[t.name];
                foreach (var rb in t.GetComponentsInChildren<Rigidbody>()) if (!rb.isKinematic) { rb.isKinematic = true; rec.madeKinematic.Add(rb); }
            }
    }

    // the opening broken into the east wall (metres from that wall's north end / the floor) and the ceiling tiles that are gone
    static readonly Rect SrEastHole = new Rect(0.92f, 0.72f, 0.98f, 1.16f);
    static readonly Rect[] SrCeilingHoles = { new Rect(8.4f, 0f, 0.6f, 0.6f), new Rect(0f, 6.0f, 1.2f, 0.6f) };

    static Material SrCeilingMat => Mat("SM_SrCeiling", "sr_ceiling.png", Color.white, 0.03f);

    /// <summary>Walls, floor and ceiling of the storage room: peeling paint over cement, bare concrete, stained ceiling tiles
    /// (textures from the location photos: scratchpad store/decay/make_decay.py). Real coordinates.</summary>
    static void BuildStoreroomShell(Transform g)
    {
        float w = SX1 - SX0, d = SZ1 - SZ0, h = SCeil - SFloor, cy = (SFloor + SCeil) * 0.5f, cx = (SX0 + SX1) * 0.5f, cz = (SZ0 + SZ1) * 0.5f;
        Vector3 keep = StoreFittings.off; StoreFittings.off = off;
        Quad("SR North", g, new Vector3(cx, cy, SZ1 - 0.012f), Vector3.back, Vector3.up, w, h, Mat("SM_SrWallN", "sr_wall_n.png", Color.white, 0.04f));
        Quad("SR South", g, new Vector3(cx, cy, SZ0 + 0.03f), Vector3.forward, Vector3.up, w, h, Mat("SM_SrWallS", "sr_wall_s.png", Color.white, 0.04f));
        Quad("SR West", g, new Vector3(SX0 + 0.03f, cy, cz), Vector3.right, Vector3.up, d, h, Mat("SM_SrWallW", "sr_wall_w.png", Color.white, 0.04f));
        StoreFittings.PlaneWithHoles(g, "SR East", new Vector3(SX1 - 0.03f, cy, cz), Vector3.left, Vector3.up, d, h, Mat("SM_SrWallE", "sr_wall_e.png", Color.white, 0.04f), SrEastHole);
        Quad("SR Floor", g, new Vector3(cx, SFloor + 0.012f, cz), Vector3.up, Vector3.forward, w, d, Mat("SM_SrFloor", "sr_floor.png", Color.white, 0.1f));
        StoreFittings.PlaneWithHoles(g, "SR Ceiling", new Vector3(cx, SCeil, cz), Vector3.down, Vector3.forward, w, d, SrCeilingMat, SrCeilingHoles);
        StoreFittings.off = keep;
    }

    /// <summary>Pipes, the broken wall, blocks, rubble, hanging tile (StoreFittings.off must already be on the room's floor).</summary>
    static void BuildStoreroomDecay(Transform g)
    {
        StoreFittings.StoreroomDecay(g, SX0 + 0.03f, SX1 - 0.03f, SZ0 + 0.03f, SZ1 - 0.012f, SCeil - SFloor, SrEastHole, SZ1, SrCeilingMat);
    }

    /// <summary>Only the storage room's surfaces and decay again, without rebuilding the whole makeover.</summary>
    [MenuItem("Tools/Store Makeover/Rebuild Storeroom Shell + Decay (Store scene)")]
    public static void RebuildStoreroomShell()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[StoreMakeover] stop Play first"); return; }
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!scene.isLoaded) { Debug.LogError("[StoreMakeover] open the Store scene first"); return; }
        root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == RootName) root = r.transform;
        rec = root != null ? root.GetComponent<StoreMakeoverRecord>() : null;
        var g = root != null ? root.Find("Storeroom") : null;
        if (rec == null || g == null) { Debug.LogError("[StoreMakeover] the makeover / its Storeroom group is not in the scene"); return; }
        var gone = new List<GameObject>();
        foreach (Transform c in g) if (c.name.StartsWith("SR ") || c.name == "Decay") gone.Add(c.gameObject);
        foreach (var x in gone) Object.DestroyImmediate(x);
        off = Vector3.zero;
        EnsureMaterials();
        StoreFittings.Mat = (n, t, c, gl, fade, cut) => Mat(n, t, c, gl, fade, cut);
        StoreFittings.SaveMesh = (m, n) => SaveFittingMesh(m, n, "Assets/Models/StoreMakeover/StoreFittings_Store.asset");
        StoreFittings.off = Vector3.zero;
        BuildStoreroomShell(g);
        StoreFittings.off = new Vector3(0f, SFloor - StoreFittings.FloorY, 0f);
        BuildStoreroomDecay(g);
        StoreFittings.off = Vector3.zero;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.allowOcclusionWhenDynamic = false;
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[StoreMakeover] storeroom shell + decay rebuilt");
    }

    /// <summary>The storage room's two steel doors (StoreFittings.off must already be on the room's floor) and the bag on the
    /// back door, which hangs from the door's lever instead of floating beside the frame.</summary>
    static void BuildStoreroomDoors(Scene scene, Transform g)
    {
        float Y(float aboveFloor) => StoreFittings.FloorY + aboveFloor;
        StoreFittings.SteelDoor(g, "Door To Shop", new Vector3(-218.17f, Y(DoorH * 0.5f), SZ1 - 0.02f), Vector3.back, DoorW, DoorH, true, "door_steel_shop");
        Vector3 lever = StoreFittings.SteelDoor(g, "Back Door", new Vector3(-216.25f, Y(DoorH * 0.5f), SZ0 + 0.04f), Vector3.forward, DoorW, DoorH, false, "door_steel_back", "sign_exit_lit");
        BuildShopSideStorageDoor(scene);

        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.name != "DoorInteractable (BackMain)") continue;
                var bagHolder = t.Find("Lock (1)"); var point = t.Find("InteractPoint");
                if (bagHolder != null)
                {
                    var oldTie = bagHolder.Find(BagTieName);
                    if (oldTie != null) Object.DestroyImmediate(oldTie.gameObject);
                    Restore(bagHolder);
                    var mf = bagHolder.GetComponentInChildren<MeshFilter>(false);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Record(bagHolder);
                        // 1. the bag hangs straight down: its pointed neck (the vertex furthest from its middle) goes up
                        Vector3 mid, tip; BagShape(mf, out mid, out tip);
                        Quaternion q = Quaternion.FromToRotation(tip - mid, Vector3.up);
                        bagHolder.SetPositionAndRotation(mid + q * (bagHolder.position - mid), q * bagHolder.rotation);
                        // 2. leaning out a little, the way a full bag rests against a door (its belly is wider than the lever stands off)
                        BagShape(mf, out mid, out tip);
                        Quaternion lean = Quaternion.AngleAxis(-24f, Vector3.right);          // bottom swings to +z, into the room
                        bagHolder.SetPositionAndRotation(tip + lean * (bagHolder.position - tip), lean * bagHolder.rotation);
                        // 3. its neck just under the lever
                        BagShape(mf, out mid, out tip);
                        Vector3 neck = lever + Vector3.down * 0.075f + Vector3.forward * 0.012f;
                        bagHolder.position += neck - tip;
                        StoreFittings.BagTie(bagHolder, BagTieName, neck, lever, Vector3.forward);
                    }
                }
                if (point != null) { Restore(point); Record(point); point.position = new Vector3(lever.x, lever.y + 0.25f, point.position.z); }
            }
    }

    const string BagTieName = "Bag Tie (store makeover)";
    // every steel door of the store is this size (the rooms are tall: a 2.1 m door looked small next to Khalid)
    const float DoorW = 1.29f, DoorH = 2.6f;
    const string ShopDoorName = "Storage Door (shop side)";

    /// <summary>The storage door as seen from the shop: the same steel door as inside the storage room (it was a tall dark
    /// slab), with a fixed panel over it up to the top of the old opening. The old slab is hidden.</summary>
    static void BuildShopSideStorageDoor(Scene scene)
    {
        var old = root.Find(ShopDoorName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject(ShopDoorName).transform;
        holder.SetParent(root, false);
        Vector3 keep = StoreFittings.off;
        StoreFittings.off = Vector3.zero;                    // the shop floor, not the storage room's
        float cx = (DoorX0 + DoorX1) * 0.5f;
        float transom = (BandBot - FloorY) - DoorH - 0.065f * 2f;
        StoreFittings.SteelDoor(holder, ShopDoorName, new Vector3(cx, FloorY + DoorH * 0.5f, ZS + 0.005f), Vector3.forward, DoorW, DoorH, false, "door_steel_shop", null, Mathf.Max(0f, transom));
        StoreFittings.off = keep;
        foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.allowOcclusionWhenDynamic = false;

        // the old slab + its handle stand in the same opening
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
            {
                if (t == null || t.name != "Door3") continue;
                foreach (var mr in t.GetComponentsInChildren<Renderer>(false))
                {
                    Vector3 c = mr.bounds.center;
                    if (!mr.enabled || mr.transform.IsChildOf(root) || Mathf.Abs(c.x - cx) > 1.1f || c.z < ZS - 0.5f || c.z > ZS + 0.5f) continue;
                    mr.enabled = false; rec.hiddenRenderers.Add(mr);
                }
            }
    }

    /// <summary>The bag's middle and its neck (the vertex furthest from the middle), in world space.</summary>
    static void BagShape(MeshFilter mf, out Vector3 mid, out Vector3 tip)
    {
        var v = mf.sharedMesh.vertices; var m = mf.transform.localToWorldMatrix;
        mid = Vector3.zero;
        foreach (var p in v) mid += m.MultiplyPoint3x4(p);
        mid /= Mathf.Max(1, v.Length);
        // its long axis: through the vertex furthest from the middle
        Vector3 far = mid; float best = -1f;
        foreach (var p in v) { Vector3 w = m.MultiplyPoint3x4(p); float d = (w - mid).sqrMagnitude; if (d > best) { best = d; far = w; } }
        Vector3 axis = (far - mid).normalized;
        float tMin = float.MaxValue, tMax = float.MinValue; Vector3 pMin = mid, pMax = mid;
        foreach (var p in v) { Vector3 w = m.MultiplyPoint3x4(p); float t = Vector3.Dot(w - mid, axis); if (t < tMin) { tMin = t; pMin = w; } if (t > tMax) { tMax = t; pMax = w; } }
        // the neck is the thin end: compare how wide the bag is near each end
        float span = tMax - tMin, rLow = 0f, rHigh = 0f; int nLow = 0, nHigh = 0;
        foreach (var p in v)
        {
            Vector3 w = m.MultiplyPoint3x4(p); float t = Vector3.Dot(w - mid, axis);
            float r = (w - mid - axis * t).magnitude;
            if (t < tMin + span * 0.3f) { rLow += r; nLow++; } else if (t > tMax - span * 0.3f) { rHigh += r; nHigh++; }
        }
        rLow /= Mathf.Max(1, nLow); rHigh /= Mathf.Max(1, nHigh);
        tip = rLow < rHigh ? pMin : pMax;
    }

    static void Record(Transform t)
    {
        foreach (var m in rec.moves) if (m.target == t) return;
        rec.moves.Add(new StoreMakeoverRecord.Move { target = t, originalPosition = t.position, hasRotation = true, originalRotation = t.rotation });
    }

    /// <summary>Back to where it stood before the makeover moved it (so building twice does not move it twice).</summary>
    static void Restore(Transform t)
    {
        foreach (var m in rec.moves) if (m.target == t) { t.position = m.originalPosition; if (m.hasRotation) t.rotation = m.originalRotation; }
    }

    const string FuseLampName = "Emergency Lamp (fuse cabinet)";

    /// <summary>The battery emergency lamp on the wall above the cabinet that holds the fuse: when the power fails it is the
    /// one light in the shop and it shines on the cabinet (wired into the cabinet's FuseCabinetHighlight).</summary>
    static void BuildFuseCabinetLamp(Scene scene)
    {
        var old = root.Find(FuseLampName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        FuseCabinetHighlight hl = null; StoreBlackoutSequence blackout = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            foreach (var x in r.GetComponentsInChildren<FuseCabinetHighlight>(true)) hl = x;
            foreach (var x in r.GetComponentsInChildren<StoreBlackoutSequence>(true)) blackout = x;
        }
        if (hl == null) { Debug.LogWarning("[StoreMakeover] no FuseCabinetHighlight in the scene: no emergency lamp"); return; }
        Bounds cb = new Bounds(hl.transform.position, Vector3.zero); bool any = false;
        foreach (var mr in hl.GetComponentsInChildren<MeshRenderer>(false)) { if (!any) { cb = mr.bounds; any = true; } else cb.Encapsulate(mr.bounds); }
        var holder = new GameObject(FuseLampName).transform;
        holder.SetParent(root, false);
        Vector3 keep = StoreFittings.off; StoreFittings.off = Vector3.zero;
        Vector3 wall = new Vector3(cb.center.x, BandBot - 0.2f, ZS + 0.004f);
        Vector3 aim = new Vector3(cb.center.x, cb.max.y - 0.25f, cb.max.z + 0.25f);
        StoreFittings.EmergencyLamp(holder, FuseLampName, wall, Vector3.forward, aim, out Light light, out Renderer lens);
        StoreFittings.off = keep;
        foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.allowOcclusionWhenDynamic = false;
        hl.lamp = light; hl.lampLens = lens; hl.blackout = blackout;
        EditorUtility.SetDirty(hl);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hl);
        Debug.Log("[StoreMakeover] emergency lamp at " + wall + " aimed at " + aim + " (cabinet " + cb.min + " .. " + cb.max + ")");
    }

    [MenuItem("Tools/Store Makeover/Rebuild Fuse Cabinet Lamp (Store scene)")]
    public static void RebuildFuseCabinetLamp()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[StoreMakeover] stop Play first"); return; }
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!scene.isLoaded) { Debug.LogError("[StoreMakeover] open the Store scene first"); return; }
        root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == RootName) root = r.transform;
        rec = root != null ? root.GetComponent<StoreMakeoverRecord>() : null;
        if (rec == null) { Debug.LogError("[StoreMakeover] the makeover is not in the scene"); return; }
        off = Vector3.zero;
        StoreFittings.Mat = (n, t, c, gl, fade, cut) => Mat(n, t, c, gl, fade, cut);
        StoreFittings.SaveMesh = (m, n) => SaveFittingMesh(m, n, "Assets/Models/StoreMakeover/StoreFittings_Store.asset");
        BuildFuseCabinetLamp(scene);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }

    /// <summary>Only the storage room's doors again, without rebuilding the whole makeover.</summary>
    [MenuItem("Tools/Store Makeover/Rebuild Storeroom Doors (Store scene)")]
    public static void RebuildStoreroomDoors()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[StoreMakeover] stop Play first"); return; }
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Store.unity");
        if (!scene.isLoaded) { Debug.LogError("[StoreMakeover] open the Store scene first"); return; }
        root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == RootName) root = r.transform;
        rec = root != null ? root.GetComponent<StoreMakeoverRecord>() : null;
        var g = root != null ? root.Find("Storeroom") : null;
        if (rec == null || g == null) { Debug.LogError("[StoreMakeover] the makeover / its Storeroom group is not in the scene"); return; }
        foreach (var n in new[] { "Door To Shop", "Back Door" }) { var old = g.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
        off = Vector3.zero;
        StoreFittings.Mat = (n, t, c, gl, fade, cut) => Mat(n, t, c, gl, fade, cut);
        StoreFittings.SaveMesh = (m, n) => SaveFittingMesh(m, n, "Assets/Models/StoreMakeover/StoreFittings_Store.asset");
        StoreFittings.off = new Vector3(0f, SFloor - StoreFittings.FloorY, 0f);
        BuildStoreroomDoors(scene, g);
        StoreFittings.off = Vector3.zero;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.allowOcclusionWhenDynamic = false;
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[StoreMakeover] storeroom doors rebuilt");
    }

    static Bounds oldFuseBox; static bool haveFuseBox;

    /// <summary>The loose props that replace the old downloaded models (register, counter devices, baskets, trolley, floor clutter, fuse box).</summary>
    static void BuildLooseProps(Scene scene, bool gameCopy)
    {
        var g = Group("Props (loose)");
        string[] S(params string[] a) => a;
        Vector3 toCashier = Vector3.back;
        // ---- on the counter
        var screen = StoreFittings.CashRegister(g, "Cash Register", new Vector3(-239.1f, CounterTop, 96.36f), toCashier);
        StoreFittings.CounterDevices(g, "Counter Devices", new Vector3(-238.36f, CounterTop, 96.38f), new Vector3(-238.7f, CounterTop, 96.3f),
            new Vector3(-237.62f, CounterTop, 94.82f), new Vector3(-238.12f, CounterTop, 96.55f), toCashier);
        foreach (var r in scene.GetRootGameObjects())
            foreach (var fx in r.GetComponentsInChildren<CashRegisterPowerFX>(true))
            {
                var so = new SerializedObject(fx);
                var sp = so.FindProperty("screen");
                rec.refChanges.Add(new StoreMakeoverRecord.RefChange { component = fx, property = "screen", original = sp.objectReferenceValue });
                sp.objectReferenceValue = screen;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        // ---- by the door
        StoreFittings.DoorMat(g, "Door Mat", new Vector3(-239.6f, 0, 103.06f), 0f, 0.56f, 0.95f);
        StoreFittings.BasketStack(g, "Basket Stack", new Vector3(-239.66f, 0, 101.82f), 90f, 6);
        // ---- the dropped shopping: a basket on its side, what fell out of it, the list beside it
        StoreFittings.BasketLoose(g, "Dropped Basket", new Vector3(-236.68f, 0, 102.12f), 35f, true);
        StoreFittings.Spill(g, "Dropped Shopping", new[] {
            ("rice_small", -236.15f, 101.7f, 70f, 'f'), ("fz_chicken", -237.56f, 101.38f, 200f, 'f'), ("noodles", -237.72f, 101.98f, 310f, 'f'), ("noodles", -237.96f, 101.52f, 20f, 'f'),
            ("canned", -236.62f, 101.22f, 120f, 's'), ("tomato", -237.3f, 100.95f, 35f, 's'), ("laziz_classic", -236.2f, 102.55f, 150f, 'f') });
        // ---- the crisps somebody knocked off the island, the abandoned trolley in the same aisle
        StoreFittings.Spill(g, "Fallen Crisps", new[] {
            ("chips", -233.93f, 98.8f, 15f, 'f'), ("chips", -234.81f, 98.55f, 140f, 'f'), ("chips", -234.44f, 98.84f, 260f, 'f'), ("chips", -235.32f, 98.6f, 75f, 'f'),
            ("chips", -234.18f, 99.22f, 310f, 'f'), ("nuts", -235.06f, 98.98f, 200f, 'f') });
        StoreFittings.Trolley(g, "Trolley", new Vector3(-233.2f, 0, 99.55f), 78f, S("pack_waha", "laziz_classic_big", "rice_small", "juice_carton", "tissue"));
        StoreFittings.BasketLoose(g, "Left Basket", new Vector3(-236.7f, 0, 98.82f), -20f, false, S("dairy_big", "biscuits", "chips", "canned"));
        // ---- somebody was mopping: bucket, sign, cooler, crate, cartons being unpacked
        StoreFittings.MopBucket(g, "Mop Bucket", new Vector3(-234.2f, 0, 96.55f), 30f, S("bleach", "dish", "insect"), new[] { new Vector3(-234.52f, 0, 96.74f), new Vector3(-234.4f, 0, 96.36f) });
        StoreFittings.WetFloorSign(g, "Wet Floor Sign", new Vector3(-231.0f, 0, 98.9f), 75f);
        StoreFittings.Cooler(g, "Cooler", new Vector3(-232.64f, 0, 96.5f), 20f);
        StoreFittings.BottleCrate(g, "Bottle Crate", new Vector3(-235.46f, 0, 95.72f), 8f, S("soda_small", "water_small", "malt"));
        StoreFittings.OpenBox(g, "Open Carton A", new Vector3(-234.3f, 0, 105.05f), 12f, 0.42f, 0.27f, 0.32f, S("juice_can", "soda_can"));
        StoreFittings.OpenBox(g, "Open Carton B", new Vector3(-233.55f, 0, 96.12f), -25f, 0.42f, 0.27f, 0.32f, S("canned", "tomato"));
        StoreFittings.Broom(g, "Broom", new Vector3(-229.78f, 0, southSkinZ + 0.22f), new Vector3(0.6f, 0, -1f));
        // ---- litter by the water packs
        StoreFittings.Spill(g, "Litter", new[] {
            ("water_small", -236.46f, 104.25f, 40f, 's'), ("soda_can", -236.3f, 104.33f, 160f, 's'), ("soda_can", -236.6f, 104.2f, 0f, 'u'), ("juice_can", -236.52f, 104.4f, 250f, 's'),
            ("canned", -236.4f, 104.07f, 0f, 'u'), ("energy", -236.72f, 104.36f, 300f, 's'), ("juice_can", -236.22f, 104.5f, 90f, 's') });
        // ---- the fuse box: a proper steel enclosure over the old blocks
        if (haveFuseBox)
        {
            Vector3 c = oldFuseBox.center - off;
            StoreFittings.FuseBoxShell(g, "Fuse Box Enclosure", new Vector3(XE - 0.07f, c.y, c.z), Vector3.left, oldFuseBox.size.z + 0.03f, oldFuseBox.size.y + 0.03f, 0.14f, BandBot);
        }
    }

    static void HideExtras(Scene scene)
    {
        haveFuseBox = false;
        // the old fittings the new ones replace (and the fuse-hunt cabinets nobody needs any more)
        var names = new List<string> { "Shelf 1", "Shelf 2", "Shelf 3", "Supermarket Chips Shelf1", "Supermarket Chips Shelf1 (1)", "Supermarket Chips Shelf1 (2)",
            "Coke", "fridge blue", "mini fridge (1)", "freezer", "MaintCabinet", "ElectricalBox", "stepledder", "corner trash bins", "FAMILY SIZE TIN AND BASKET",
            // every remaining downloaded prop: replaced by BuildLooseProps / the new fittings
            "vending machine", "vending machine (1)", "painted desk", "SHOPPIING CART", "SHOPPIING CART (1)", "welcome mat", "CardBoard Box", "CardBoard Box (1)", "wet floor board",
            "landline", "lays", "lays (1)", "lays (2)", "lays (3)", "lays (4)", "lays (5)", "ASHTRAY", "card machine", "broom", "blue crate", "cooler", "Cleaning supplies", "red bucketrev",
            "LooseClutter_Scatter", "coins", "coins (1)", "32dc338ec8114de6a8e0a4878d73d51f.fbx" };
        var shopB = new Bounds(new Vector3((XW + XE) * 0.5f, 3.5f, (ZS + ZN) * 0.5f) + off, new Vector3(XE - XW + 0.6f, 5f, ZN - ZS + 0.6f));
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
            {
                if (!t.gameObject.activeSelf) continue;
                bool inside = shopB.Contains(t.position + Vector3.up * 0.5f);
                if (!inside && t.name.StartsWith("32dc338e"))   // the old security TV: its pivot is far away from its mesh
                {
                    var tv = t.GetComponentInChildren<Renderer>();
                    inside = tv != null && shopB.Contains(tv.bounds.center);
                }
                if (!inside) continue;
                bool hide = names.Contains(t.name);
                // the Meshy groceries that sat on the old shelves (the ones on the floor by the basket stay)
                if (t.name.StartsWith("Meshy_AI_") && !t.name.StartsWith("Meshy_AI_Weathered")) hide = true;
                if (hide) { t.gameObject.SetActive(false); rec.deactivatedObjects.Add(t.gameObject); }
            }
        // the old register and fuse box keep their objects (dust effect, interaction, lamps): only the old models are hidden
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
            {
                if (!shopB.Contains(t.position + Vector3.up * 0.5f)) continue;
                if (t.name == "cash register" || t.name.EndsWith("_cash_register_01_0"))   // the Game copy has the register's parts loose
                {
                    foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(false)) if (mr.enabled) { mr.enabled = false; rec.hiddenRenderers.Add(mr); }
                    foreach (var c in t.GetComponentsInChildren<Collider>(false)) if (c.enabled) { c.enabled = false; rec.disabledColliders.Add(c); }
                }
                if ((t.name == "Cube" || t.name == "Cube (1)") && t.parent != null && t.parent.name == "Fusebox")
                {
                    var mr = t.GetComponent<MeshRenderer>();
                    if (mr != null && mr.enabled)
                    {
                        if (!haveFuseBox) { oldFuseBox = mr.bounds; haveFuseBox = true; } else oldFuseBox.Encapsulate(mr.bounds);
                        mr.enabled = false; rec.hiddenRenderers.Add(mr);
                    }
                }
            }
        // the old modelled grocery list next to the basket (the readable one is the GroceryListPickup)
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
                if (t.name.StartsWith("Meshy_AI_Weathered_Grocery_Lis"))
                    foreach (var mr in t.GetComponentsInChildren<MeshRenderer>()) if (mr.enabled) { mr.enabled = false; rec.hiddenRenderers.Add(mr); }
    }
}
