using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Items > Build Healing Items (PS1)
// PS1-style Painkiller (amber pill bottle, red crescent label, loose pills) and Med Kit (weathered white metal box,
// red cross): low-poly meshes + point-filtered low-res textures, pixel icons rendered from the models, swapped into the
// pickup prefabs and the inspection prefab (old renderers are only disabled). In the open scene: the HealingItemsHUD
// pickup banner, the blood texture on the low-health vignette and (Gym only) a test kit of pickups.
// Rerun safe. Tools > Items > Revert Healing Items (PS1) puts the old models / icons / vignette back.
public static class HealingItemsBuilder
{
    const string Dir = "Assets/Models/HealingItems";
    const string PainkillerItemPath = "Assets/Resources/Items/Supplies/Pain Killer.asset";
    const string MedKitItemPath = "Assets/Resources/Items/Supplies/Med Kit.asset";
    const string PainkillerIconPath = "Assets/Sprites/Items/PainkillerIcon.png";
    const string MedKitIconPath = "Assets/Sprites/Items/MedKitIcon.png";
    const string OldPainkillerIconPath = "Assets/Sprites/Items/syringe.png";
    const string OldMedKitIconPath = "Assets/Sprites/Items/health.png";
    const string SandBandPath = Dir + "/SandBand.png";
    const string BloodPath = Dir + "/BloodVignette.png";
    const string OldVignettePath = "Assets/Colors/Code_Generated_Image (1).png";
    const string FontPath = "Assets/Fonts/EBGaramond-Bold.ttf";
    const string InspectionPrefab = "Assets/Prefab/ItemInspectionHandler.prefab";
    const string MedKitPickupPath = "Assets/Prefab/ItemsPickups/MedKitPickup.prefab";
    const string PainkillerPickupPath = "Assets/Prefab/ItemsPickups/PainKillerPickup.prefab";
    static readonly string[] MedKitPickups = { MedKitPickupPath, "Assets/Prefab/ItemsPickups/Bag Loot/MedKitPickupBag.prefab" };
    const string ModelName = "PS1 Model";
    const string PainkillerFbxPath = Dir + "/PainkillerBlender.fbx";
    const string PainkillerAtlasPath = Dir + "/PainkillerAtlas.png";
    const string TestKitName = "Healing Items Test Kit";

    // real-world sizes (m)
    const float KitW = 0.30f, KitH = 0.20f, KitD = 0.10f, KitChamfer = 0.02f;
    // pickups in the world are a bit bigger than life so they read from the third-person camera
    const float PainkillerPickupScale = 2.2f;

    [MenuItem("Tools/Items/Build Healing Items (PS1)")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);

        // the painkiller is modelled + painted in Blender (AnimationTools/healing_items/build_painkiller.py)
        var pkTex = ImportTexture(PainkillerAtlasPath);
        var mkTex = SaveTexture(Dir + "/MedKitTex.png", MedKitTexture(), false);
        var pkMat = SaveMaterial(Dir + "/Painkiller.mat", pkTex, 0.5f);
        var mkMat = SaveMaterial(Dir + "/MedKit.mat", mkTex, 0.25f);

        var bottle = FbxMesh(PainkillerFbxPath, "PainkillerBottle");
        var pill = FbxMesh(PainkillerFbxPath, "Tablet");
        if (bottle == null || pill == null) { Debug.LogError("[HealingItemsBuilder] Run build_painkiller.py in Blender first: " + PainkillerFbxPath); return; }
        var kit = SaveMesh(MedKitMesh(), Dir + "/MedKit.asset");

        var pkModel = PainkillerModel(bottle, pill, pkMat);
        var mkModel = MedKitModel(kit, mkMat);

        // icons
        var pkIcon = SaveIcon(PainkillerIconPath, RenderIcon(pkModel, new Vector3(24f, -28f, 0f)));
        var mkIcon = SaveIcon(MedKitIconPath, RenderIcon(mkModel, new Vector3(16f, -32f, 0f)));
        SetIcon(PainkillerItemPath, pkIcon);
        SetIcon(MedKitItemPath, mkIcon);

        // pickups + inspection
        SwapPickup(PainkillerPickupPath, pkModel, true);
        foreach (var p in MedKitPickups) SwapPickup(p, mkModel, false);
        // the inspect / inventory preview models: the prefab is left original, the swap is done on the scene's own handler
        // (each scene adds items to it, so the Pain Killer there is a scene object the prefab can't reach)
        CleanInspectionPrefab();
        var sceneHandler = Object.FindFirstObjectByType<ItemInspectionHandler>(FindObjectsInactive.Include);
        if (sceneHandler != null) SwapInspection(sceneHandler, pkModel, mkModel);
        else Debug.LogWarning("[HealingItemsBuilder] No ItemInspectionHandler in the open scene - inspect/inventory models not swapped");

        // HUD textures
        SaveTexture(SandBandPath, SandBandTexture(), false);
        var blood = SaveTexture(BloodPath, BloodTexture(), true, true);

        BuildQuickHeal();

        // open scene
        var scene = SceneManager.GetActiveScene();
        BuildHUD(scene, pkIcon, mkIcon);
        ApplyVignette(scene, AssetDatabase.LoadAssetAtPath<Sprite>(BloodPath));
        if (scene.name == "Gym") BuildTestKit(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[HealingItemsBuilder] Built PS1 Painkiller + Med Kit, pickup banner and blood vignette in " + scene.name);
    }

    [MenuItem("Tools/Items/Revert Healing Items (PS1)")]
    public static void Revert()
    {
        SetIcon(PainkillerItemPath, AssetDatabase.LoadAssetAtPath<Sprite>(OldPainkillerIconPath));
        SetIcon(MedKitItemPath, AssetDatabase.LoadAssetAtPath<Sprite>(OldMedKitIconPath));

        RestorePrefab(PainkillerPickupPath);
        foreach (var p in MedKitPickups) RestorePrefab(p);
        RemoveQuickHeal();
        CleanInspectionPrefab();
        var sceneHandler = Object.FindFirstObjectByType<ItemInspectionHandler>(FindObjectsInactive.Include);
        if (sceneHandler != null) RestoreInspection(sceneHandler);

        var scene = SceneManager.GetActiveScene();
        foreach (var hud in Object.FindObjectsByType<HealingItemsHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(hud.gameObject);
        var kit = GameObject.Find(TestKitName);
        if (kit != null) Object.DestroyImmediate(kit);
        var old = AssetDatabase.LoadAssetAtPath<Sprite>(OldVignettePath);
        foreach (var img in VignetteImages())
        {
            Undo.RecordObject(img, "Revert vignette");
            img.sprite = old;
            img.color = new Color(0.83f, 0.149f, 0.114f, 0f);
        }
        foreach (var e in Object.FindObjectsByType<LowHealthEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(e);
            so.FindProperty("effectSmoothness").floatValue = 5f;
            so.FindProperty("strength").floatValue = 1f;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[HealingItemsBuilder] Reverted healing items in " + scene.name + " (the generated assets in " + Dir + " are left in place)");
    }

    // ------------------------------------------------------------------ meshes

    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        // flat quad, wound so it faces 'outward'
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            bool flip = Vector3.Dot(normal, outward) < 0f;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            Vector3 nn = outward.normalized;
            for (int k = 0; k < 4; k++) n.Add(nn);
            if (!flip) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            for (int k = 0; k < 3; k++) n.Add(outward.normalized);
            if (Vector3.Dot(normal, outward) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
        }

        // smooth lathe band between two profile points, u wrapping across the rect
        public void Band(int sides, float r0, float y0, float r1, float y1, Rect rect)
        {
            float slope = Mathf.Atan2(r0 - r1, y1 - y0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p00 = d0 * r0 + Vector3.up * y0, p10 = d1 * r0 + Vector3.up * y0;
                Vector3 p11 = d1 * r1 + Vector3.up * y1, p01 = d0 * r1 + Vector3.up * y1;
                float u0 = rect.xMin + rect.width * i / sides, u1 = rect.xMin + rect.width * (i + 1) / sides;
                int s = v.Count;
                v.Add(p00); v.Add(p10); v.Add(p11); v.Add(p01);
                uv.Add(new Vector2(u0, rect.yMin)); uv.Add(new Vector2(u1, rect.yMin)); uv.Add(new Vector2(u1, rect.yMax)); uv.Add(new Vector2(u0, rect.yMax));
                Vector3 n0 = (d0 * Mathf.Cos(slope) + Vector3.up * Mathf.Sin(slope)).normalized;
                Vector3 n1 = (d1 * Mathf.Cos(slope) + Vector3.up * Mathf.Sin(slope)).normalized;
                n.Add(n0); n.Add(n1); n.Add(n1); n.Add(n0);
                Vector3 face = Vector3.Cross(p10 - p00, p11 - p00);
                if (Vector3.Dot(face, (d0 + d1)) >= 0f) { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
                else { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
            }
        }

        // flat disc (fan), up = +1 faces up, -1 faces down
        public void Disc(int sides, float r, float y, float up, Rect rect)
        {
            Vector3 c = Vector3.up * y;
            Vector2 uc = rect.center;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                Vector2 q0 = uc + new Vector2(Mathf.Cos(a0) * rect.width, Mathf.Sin(a0) * rect.height) * 0.5f;
                Vector2 q1 = uc + new Vector2(Mathf.Cos(a1) * rect.width, Mathf.Sin(a1) * rect.height) * 0.5f;
                Tri(c, p0, p1, uc, q0, q1, Vector3.up * up);
            }
        }

        public void Box(Vector3 center, Vector3 size, Rect rect)
        {
            Vector3 h = size * 0.5f;
            Vector2 a = new Vector2(rect.xMin, rect.yMin), b = new Vector2(rect.xMax, rect.yMin), c = new Vector2(rect.xMax, rect.yMax), d = new Vector2(rect.xMin, rect.yMax);
            for (int axis = 0; axis < 3; axis++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Vector3 nrm = Vector3.zero; nrm[axis] = sgn;
                    Vector3 u = Vector3.zero, w = Vector3.zero;
                    u[(axis + 1) % 3] = 1f; w[(axis + 2) % 3] = 1f;
                    Vector3 fc = center + Vector3.Scale(nrm, h);
                    Vector3 hu = Vector3.Scale(u, h), hw = Vector3.Scale(w, h);
                    Quad(fc - hu - hw, fc + hu - hw, fc + hu + hw, fc - hu + hw, a, b, c, d, nrm);
                }
        }

        public global::UnityEngine.Mesh ToMesh(string name)
        {
            var m = new global::UnityEngine.Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    static Rect Px(float x, float y, float w, float h, float size) => new Rect(x / size, y / size, w / size, h / size);

    // 128x128 atlas: front/back x0-95 y0-63, sides y64-79, handle x96-111 y0-15, latch x112-127 y0-15
    static global::UnityEngine.Mesh MedKitMesh()
    {
        var mb = new MB();
        float w = KitW * 0.5f, c = KitChamfer, h = KitH, dz = KitD * 0.5f;
        Rect front = Px(0, 0, 96, 64, 128), side = Px(0, 64, 128, 16, 128), handle = Px(97, 1, 14, 14, 128), latch = Px(113, 1, 14, 14, 128);

        var P = new[]
        {
            new Vector2(-w + c, 0f), new Vector2(w - c, 0f), new Vector2(w, c), new Vector2(w, h - c),
            new Vector2(w - c, h), new Vector2(-w + c, h), new Vector2(-w, h - c), new Vector2(-w, c)
        };
        Vector2 center = new Vector2(0f, h * 0.5f);
        System.Func<Vector2, Vector2> fuv = p => new Vector2(front.xMin + front.width * (p.x + w) / (2f * w), front.yMin + front.height * p.y / h);
        System.Func<Vector2, Vector2> buv = p => new Vector2(front.xMin + front.width * (w - p.x) / (2f * w), front.yMin + front.height * p.y / h);

        float perimeter = 0f;
        for (int i = 0; i < P.Length; i++) perimeter += Vector2.Distance(P[i], P[(i + 1) % P.Length]);

        float run = 0f;
        for (int i = 0; i < P.Length; i++)
        {
            Vector2 a = P[i], b = P[(i + 1) % P.Length];
            // front (-z) and back (+z) fans
            mb.Tri(new Vector3(center.x, center.y, -dz), new Vector3(a.x, a.y, -dz), new Vector3(b.x, b.y, -dz), fuv(center), fuv(a), fuv(b), Vector3.back);
            mb.Tri(new Vector3(center.x, center.y, dz), new Vector3(a.x, a.y, dz), new Vector3(b.x, b.y, dz), buv(center), buv(a), buv(b), Vector3.forward);

            // side strip
            Vector2 e = b - a;
            Vector3 outward = new Vector3(e.y, -e.x, 0f).normalized;
            if (Vector3.Dot(outward, new Vector3(a.x + b.x, a.y + b.y - h, 0f)) < 0f) outward = -outward;
            float u0 = side.xMin + side.width * run / perimeter;
            run += e.magnitude;
            float u1 = side.xMin + side.width * run / perimeter;
            mb.Quad(new Vector3(a.x, a.y, -dz), new Vector3(b.x, b.y, -dz), new Vector3(b.x, b.y, dz), new Vector3(a.x, a.y, dz),
                new Vector2(u0, side.yMin), new Vector2(u1, side.yMin), new Vector2(u1, side.yMax), new Vector2(u0, side.yMax), outward);
        }

        // handle: two posts and a grip
        mb.Box(new Vector3(-0.06f, h + 0.011f, 0f), new Vector3(0.014f, 0.022f, 0.016f), handle);
        mb.Box(new Vector3(0.06f, h + 0.011f, 0f), new Vector3(0.014f, 0.022f, 0.016f), handle);
        mb.Box(new Vector3(0f, h + 0.027f, 0f), new Vector3(0.15f, 0.013f, 0.02f), handle);
        // latches on the front
        mb.Box(new Vector3(-0.1f, h - 0.03f, -dz - 0.004f), new Vector3(0.022f, 0.032f, 0.008f), latch);
        mb.Box(new Vector3(0.1f, h - 0.03f, -dz - 0.004f), new Vector3(0.022f, 0.032f, 0.008f), latch);
        return mb.ToMesh("MedKit");
    }

    // ------------------------------------------------------------------ textures

    // GLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead)
    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    static float ValueNoise(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float x, float y, int seed)
    {
        float sum = 0f, amp = 0.5f, f = 1f;
        for (int o = 0; o < 4; o++) { sum += amp * ValueNoise(x * f, y * f, seed + o * 31); f *= 2f; amp *= 0.5f; }
        return sum;
    }

    static Color Jitter(Color c, float amount, int x, int y, int seed)
    {
        float j = (Hash(x, y, seed) - 0.5f) * 2f * amount;
        return new Color(Mathf.Clamp01(c.r + j), Mathf.Clamp01(c.g + j), Mathf.Clamp01(c.b + j), c.a);
    }

    static Texture2D MedKitTexture()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color enamel = new Color(0.86f, 0.84f, 0.77f), grime = new Color(0.42f, 0.38f, 0.3f), rust = new Color(0.46f, 0.25f, 0.11f), rustDark = new Color(0.28f, 0.14f, 0.06f);
        Color red = new Color(0.66f, 0.08f, 0.07f), redDark = new Color(0.45f, 0.05f, 0.05f);
        var rng = new System.Random(5);

        // rust spots (in the front + side areas)
        var spots = new List<Vector3>();
        for (int i = 0; i < 26; i++)
        {
            bool onSide = i >= 16;
            float x = onSide ? rng.Next(0, 128) : (rng.NextDouble() < 0.5 ? rng.Next(1, 14) : rng.Next(82, 95));
            float y = onSide ? 64 + rng.Next(0, 16) : rng.Next(1, 63);
            if (!onSide && rng.NextDouble() < 0.4) { x = rng.Next(2, 94); y = rng.NextDouble() < 0.5 ? rng.Next(1, 7) : rng.Next(56, 63); }
            spots.Add(new Vector3(x, y, 0.8f + (float)rng.NextDouble() * 2.2f));
        }

        // scratches
        var scratches = new List<Vector4>();
        for (int i = 0; i < 14; i++)
            scratches.Add(new Vector4(rng.Next(4, 92), rng.Next(4, 60), (float)(rng.NextDouble() * Mathf.PI), 3 + rng.Next(0, 8)));

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color col;
                if (y < 64 && x < 96)
                {
                    // front / back face
                    float edge = Mathf.Min(Mathf.Min(x, 95 - x), Mathf.Min(y, 63 - y));
                    float dirt = Fbm(x / 9f, y / 9f, 11) * 0.55f + (1f - Mathf.Clamp01(edge / 10f)) * 0.35f + (1f - y / 63f) * 0.12f;
                    col = Color.Lerp(enamel, grime, Mathf.Clamp01(dirt - 0.3f) * 0.6f);
                    if (edge < 1.5f) col = Color.Lerp(col, grime, 0.45f);

                    // red cross, chipped
                    float cx = Mathf.Abs(x - 47.5f), cy = Mathf.Abs(y - 31.5f);
                    bool cross = (cx <= 6f && cy <= 18f) || (cy <= 6f && cx <= 18f);
                    if (cross)
                    {
                        bool border = (cx > 5f && cy <= 18f && cy > 6f) || (cy > 5f && cx <= 18f && cx > 6f) || cx > 17f || cy > 17f;
                        bool chipped = Hash(x, y, 21) < (border ? 0.22f : 0.04f);
                        if (!chipped) col = Color.Lerp(red, redDark, Fbm(x / 5f, y / 5f, 13) * 0.7f + (border ? 0.2f : 0f));
                    }

                    foreach (var s in scratches)
                    {
                        Vector2 dir = new Vector2(Mathf.Cos(s.z), Mathf.Sin(s.z));
                        Vector2 rel = new Vector2(x - s.x, y - s.y);
                        float along = Vector2.Dot(rel, dir);
                        float off = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
                        if (along >= 0f && along <= s.w && off < 0.55f) col = Color.Lerp(col, Hash((int)s.x, (int)s.y, 3) > 0.5f ? Color.white : grime, 0.45f);
                    }
                    col = Jitter(col, 0.03f, x, y, 9);
                }
                else if (y >= 64 && y < 80)
                {
                    // side strip: grime, lid seam, dents
                    float dirt = Fbm(x / 8f, y / 6f, 17) * 0.6f + (y < 66 || y > 77 ? 0.25f : 0f);
                    col = Color.Lerp(enamel, grime, Mathf.Clamp01(dirt - 0.28f) * 0.65f);
                    if (y == 75) col = Color.Lerp(col, grime * 0.6f, 0.8f);
                    if (y == 76) col = Color.Lerp(col, Color.white, 0.25f);
                    col = Jitter(col, 0.03f, x, y, 10);
                }
                else if (y < 16 && x >= 96 && x < 112)
                    col = Jitter(new Color(0.12f, 0.11f, 0.1f), 0.03f, x, y, 12);             // handle (black plastic)
                else if (y < 16 && x >= 112)
                    col = Jitter(Color.Lerp(new Color(0.62f, 0.62f, 0.6f), rust, Hash(x, y, 5) < 0.25f ? 0.6f : 0f), 0.05f, x, y, 14); // latch
                else
                    col = enamel;

                // rust over everything painted
                if (y < 80)
                    foreach (var s in spots)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(s.x, s.y)) + (Hash(x, y, 31) - 0.5f) * 1.2f;
                        if (d < s.z) { col = Color.Lerp(rust, rustDark, 1f - d / s.z); break; }
                        if (d < s.z + 1.2f) col = Color.Lerp(col, rust, 0.35f);
                    }

                col.a = 1f;
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        return tex;
    }

    // the highlighted row of the pickup banner (drawn 1:1 in the banner's render texture pixels)
    static Texture2D SandBandTexture()
    {
        const int W = 258, H = 36;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        Color dark = new Color(0.26f, 0.19f, 0.1f), mid = new Color(0.47f, 0.36f, 0.2f), light = new Color(0.66f, 0.53f, 0.32f), edge = new Color(0.78f, 0.64f, 0.38f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1), v = y / (float)(H - 1);
                // streaky sand: stretched noise + grain
                float streak = Fbm(x / 22f, y / 2.2f, 41);
                float grain = Hash(x, y, 43);
                float glow = Mathf.Sin(Mathf.Clamp01((u - 0.05f) / 0.9f) * Mathf.PI) * 0.6f + 0.4f;
                Color col = Color.Lerp(dark, mid, streak * 1.3f * glow);
                if (grain > 0.82f) col = Color.Lerp(col, light, 0.6f);
                if (grain < 0.12f) col = Color.Lerp(col, dark, 0.6f);
                col = Color.Lerp(col, Color.Lerp(mid, light, 0.5f), Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2.2f) * 0.18f * glow);

                // ragged bright lines on the top and bottom edge
                int ragTop = Hash(x / 3, 1, 45) > 0.6f ? 1 : 0;
                int ragBottom = Hash(x / 3, 2, 45) > 0.6f ? 1 : 0;
                float a = 1f;
                if (y >= H - 1 - ragTop || y <= ragBottom) col = Color.Lerp(col, edge, 0.75f);
                if (y == H - 1 && ragTop == 0 && Hash(x, 3, 47) > 0.5f) a = 0f;
                if (y == 0 && ragBottom == 0 && Hash(x, 4, 47) > 0.5f) a = 0f;

                // left edge torn, right end crumbles away
                float left = Mathf.Clamp01((x - Hash(0, y / 2, 49) * 8f) / 6f);
                float right = 1f - Smooth(0.9f, 1f, u + (Hash(x / 2, y, 51) - 0.5f) * 0.08f);
                a *= Mathf.Clamp01(left) * Mathf.Clamp01(right);
                if (a < 0.5f && Hash(x, y, 53) < a * 1.4f) a = 0.85f;   // dusty specks instead of a smooth fade
                else if (a < 0.5f) a = 0f;
                else a = 0.95f;

                col.a = a;
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        return tex;
    }

    // modern blood vignette: an intense dark-red band that hugs the screen edges (stronger in the corners) and leaves the
    // middle clear, with soft organic mottling - high resolution, bilinear, no pixel art. LowHealthEffect scales the
    // overlay up at light damage (band pushed off screen) and back to 1 near death (band fully in).
    static Texture2D BloodTexture()
    {
        const int W = 1024, H = 576;
        const float Aspect = W / (float)H;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color[W * H];
        Color bright = new Color(0.66f, 0.02f, 0.02f), deep = new Color(0.14f, 0f, 0.005f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float nx = x / (W - 1f) * 2f - 1f, ny = y / (H - 1f) * 2f - 1f;
                // distance to the nearest edge in half-screen-height units, soft min so the corners round off
                float ex = (1f - Mathf.Abs(nx)) * Aspect, ey = 1f - Mathf.Abs(ny);
                const float K = 0.07f;
                float e = -K * Mathf.Log(Mathf.Exp(-ex / K) + Mathf.Exp(-ey / K));
                float broad = Fbm(x / 160f, y / 160f, 61);
                float fine = Fbm(x / 40f, y / 40f, 67);
                float edge = e + (broad - 0.5f) * 0.16f + (fine - 0.5f) * 0.035f;
                float body = Mathf.Pow(1f - Smooth(0.0f, 0.42f, edge), 1.6f);
                Color col = Color.Lerp(bright, deep, Mathf.Clamp01(body * 1.05f + (fine - 0.5f) * 0.2f));
                col.a = Mathf.Clamp01(body * 1.15f);
                px[y * W + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ assets

    static Texture2D SaveTexture(string path, Texture2D tex, bool sprite, bool smooth = false)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite) imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // an existing PNG (painted in Blender) with the same point-filtered settings
    internal static Texture2D ImportTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Default;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    internal static global::UnityEngine.Mesh FbxMesh(string path, string meshName)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return null;
        if (!imp.bakeAxisConversion || imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation)
        {
            imp.bakeAxisConversion = true;   // Y-up vertices, so the mesh can be used on its own
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.SaveAndReimport();
        }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is global::UnityEngine.Mesh m && m.name == meshName) return m;
        return null;
    }

    static Material SaveMaterial(string path, Texture2D tex, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("Standard");
        mat.mainTexture = tex;
        mat.color = Color.white;
        mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static global::UnityEngine.Mesh SaveMesh(global::UnityEngine.Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<global::UnityEngine.Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static GameObject NewPart(string name, Transform parent, global::UnityEngine.Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return go;
    }

    static GameObject PainkillerModel(global::UnityEngine.Mesh bottle, global::UnityEngine.Mesh pill, Material mat)
    {
        var root = new GameObject("PainkillerModel");
        var b = NewPart("Bottle", root.transform, bottle, mat);
        b.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);   // crescent + name toward the icon camera (front, -z)
        var p1 = NewPart("Pill 1", root.transform, pill, mat);
        p1.transform.localPosition = new Vector3(0.036f, 0f, -0.03f);
        var p2 = NewPart("Pill 2", root.transform, pill, mat);
        p2.transform.localPosition = new Vector3(0.054f, 0f, -0.012f);
        p2.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        var p3 = NewPart("Pill 3", root.transform, pill, mat);
        p3.transform.localPosition = new Vector3(0.02f, 0.0035f, -0.042f);
        p3.transform.localRotation = Quaternion.Euler(22f, 0f, 8f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Dir + "/PainkillerModel.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject MedKitModel(global::UnityEngine.Mesh kit, Material mat)
    {
        var root = new GameObject("MedKitModel");
        NewPart("MedKit", root.transform, kit, mat);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Dir + "/MedKitModel.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void SetIcon(string itemPath, Sprite icon)
    {
        var item = AssetDatabase.LoadAssetAtPath<Item>(itemPath);
        if (item == null || icon == null) return;
        item.itemIcon = icon;
        EditorUtility.SetDirty(item);
    }

    // ------------------------------------------------------------------ icons

    // renders the model alone in a preview scene at 4x and boxes it down to 32x32 pixel art with a dark outline
    internal static Texture2D RenderIcon(GameObject model, Vector3 viewEuler)
    {
        const int Size = 32, Big = Size * 8;
        var scene = EditorSceneManager.NewPreviewScene();
        var go = (GameObject)Object.Instantiate(model);
        SceneManager.MoveGameObjectToScene(go, scene);

        var bounds = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);

        var camGO = new GameObject("IconCam");
        SceneManager.MoveGameObjectToScene(camGO, scene);
        var cam = camGO.AddComponent<Camera>();
        cam.scene = scene;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.fieldOfView = 18f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        Quaternion rot = Quaternion.Euler(viewEuler);
        float dist = bounds.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
        cam.transform.rotation = rot;
        cam.transform.position = bounds.center - rot * Vector3.forward * dist;
        cam.nearClipPlane = dist * 0.1f;
        cam.farClipPlane = dist * 4f;

        var key = new GameObject("Key").AddComponent<Light>();
        SceneManager.MoveGameObjectToScene(key.gameObject, scene);
        key.type = LightType.Directional; key.intensity = 1.05f; key.color = new Color(1f, 0.95f, 0.86f);
        key.transform.rotation = Quaternion.Euler(38f, -45f, 0f);
        var fill = new GameObject("Fill").AddComponent<Light>();
        SceneManager.MoveGameObjectToScene(fill.gameObject, scene);
        fill.type = LightType.Directional; fill.intensity = 0.45f; fill.color = new Color(0.75f, 0.8f, 1f);
        fill.transform.rotation = Quaternion.Euler(20f, 140f, 0f);

        // the open scene's fog / ambient still apply to the preview render (the Game scene's dense sandstorm fog turned the
        // icons black): neutral settings while it renders, restored after
        bool fog = RenderSettings.fog;
        var ambientMode = RenderSettings.ambientMode;
        Color ambient = RenderSettings.ambientLight;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.36f, 0.38f);

        var rt = RenderTexture.GetTemporary(Big, Big, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderSettings.fog = fog;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambient;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var big = new Texture2D(Big, Big, TextureFormat.RGBA32, false);
        big.ReadPixels(new Rect(0, 0, Big, Big), 0, 0);
        big.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        EditorSceneManager.ClosePreviewScene(scene);

        // crop square around what was drawn (so the item fills the icon), box it down, hard alpha
        var px = big.GetPixels();
        int minX = Big, minY = Big, maxX = -1, maxY = -1;
        for (int y = 0; y < Big; y++)
            for (int x = 0; x < Big; x++)
                if (px[y * Big + x].a > 0.1f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        if (maxX < 0) { minX = minY = 0; maxX = maxY = Big - 1; }
        float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * (Size / (Size - 2.5f));   // room for the outline
        float ox = (minX + maxX + 1) * 0.5f - side * 0.5f, oy = (minY + maxY + 1) * 0.5f - side * 0.5f;
        float cell = side / Size;

        var small = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var outPx = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color sum = Color.clear; float aSum = 0f; int count = 0;
                int x0 = Mathf.FloorToInt(ox + x * cell), x1 = Mathf.CeilToInt(ox + (x + 1) * cell);
                int y0 = Mathf.FloorToInt(oy + y * cell), y1 = Mathf.CeilToInt(oy + (y + 1) * cell);
                for (int j = y0; j < y1; j++)
                    for (int i = x0; i < x1; i++)
                    {
                        count++;
                        if (i < 0 || j < 0 || i >= Big || j >= Big) continue;
                        Color c = px[j * Big + i];
                        sum += new Color(c.r, c.g, c.b, 0f) * c.a;
                        aSum += c.a;
                    }
                float coverage = count > 0 ? aSum / count : 0f;
                Color o = aSum > 0f ? sum / aSum : Color.clear;
                o.a = coverage >= 0.4f ? 1f : 0f;
                outPx[y * Size + x] = o;
            }
        Object.DestroyImmediate(big);

        // 1px dark outline
        var final = (Color[])outPx.Clone();
        Color outline = new Color(0.07f, 0.05f, 0.04f, 1f);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (outPx[y * Size + x].a > 0f) continue;
                bool near = false;
                for (int k = 0; k < 4 && !near; k++)
                {
                    int xx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), yy = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (xx >= 0 && yy >= 0 && xx < Size && yy < Size && outPx[yy * Size + xx].a > 0f) near = true;
                }
                if (near) final[y * Size + x] = outline;
            }
        small.SetPixels(final);
        small.Apply();
        return small;
    }

    internal static Sprite SaveIcon(string path, Texture2D tex)
    {
        SaveTexture(path, tex, true);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ------------------------------------------------------------------ prefabs

    static Bounds OldBounds(Transform root, out bool any)
    {
        var b = new Bounds(); any = false;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (IsOurs(r.transform, root)) continue;
            bool was = r.enabled;   // already disabled on a rerun
            r.enabled = true;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            r.enabled = was;
        }
        return b;
    }

    static bool IsOurs(Transform t, Transform root)
    {
        for (; t != null && t != root.parent; t = t.parent)
            if (t.name == ModelName) return true;
        return false;
    }

    static void DisableOld(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            if (!IsOurs(r.transform, root)) r.enabled = false;
    }

    static void RemoveOurs(Transform root)
    {
        var ours = new List<GameObject>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == ModelName) ours.Add(t.gameObject);
        foreach (var g in ours) if (g != null) Object.DestroyImmediate(g);
    }

    // bounds of the model prefab at scale 1, relative to its pivot
    static Bounds ModelBounds(GameObject model)
    {
        var tmp = (GameObject)Object.Instantiate(model);
        tmp.transform.position = Vector3.zero;
        tmp.transform.rotation = Quaternion.identity;
        var b = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var r in tmp.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        Object.DestroyImmediate(tmp);
        return b;
    }

    static GameObject AddModel(Transform parent, GameObject model, float worldScale, Quaternion worldRot, Vector3 worldPivot, int layer)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        inst.name = ModelName;
        inst.transform.rotation = worldRot;
        float parentScale = parent.lossyScale.x;
        inst.transform.localScale = Vector3.one * (worldScale / Mathf.Max(1e-6f, parentScale));
        inst.transform.position = worldPivot;
        foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        return inst;
    }

    static void SwapPickup(string path, GameObject model, bool painkiller)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        // picked straight up: no inspection screen, no "I got a ..." subtitle
        var inspectable = root.GetComponent<InspectableItemPickup>();
        if (inspectable != null) inspectable.instantPickup = true;
        RemoveOurs(root.transform);
        var old = OldBounds(root.transform, out bool any);
        DisableOld(root.transform);

        var mb = ModelBounds(model);
        float scale = painkiller ? PainkillerPickupScale : (any ? Mathf.Max(old.size.x, old.size.z) / Mathf.Max(mb.size.x, mb.size.z) : 1.3f);
        Vector3 spot = any ? new Vector3(old.center.x, old.min.y, old.center.z) : root.transform.position;
        // the med kit lies on its back, cross up, like it was dropped; the bottle stands
        Quaternion rot = painkiller ? root.transform.rotation : root.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
        var inst = AddModel(root.transform, model, scale, rot, spot, root.layer);
        SitOn(inst, spot);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // moves the instance so the bottom-centre of its renderers lands on 'spot'
    static void SitOn(GameObject inst, Vector3 spot)
    {
        var b = new Bounds(); bool first = true;
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        if (first) return;
        inst.transform.position += spot - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    const string OldSuffix = " (old)";

    // The handler finds the object to show by name (itemName) in its inspectionItemObjects. The new model gets a plain
    // holder object that takes over the name; the original is renamed "<name> (old)", hidden and swapped out of the array.
    static Transform FindInspectionObject(ItemInspectionHandler handler, string name, bool wantOld)
    {
        string wanted = wantOld ? name + OldSuffix : name;
        var arr = new SerializedObject(handler).FindProperty("inspectionItemObjects");
        for (int k = 0; k < arr.arraySize; k++)
            if (arr.GetArrayElementAtIndex(k).objectReferenceValue is GameObject go && go.name == wanted) return go.transform;
        foreach (var t in handler.GetComponentsInChildren<Transform>(true))
            if (t.name == wanted) return t;
        return null;
    }

    // Both the handler (pickup / pause inspection) and the inventory panel (InventoryDescriptionUI) keep their own
    // inspectionItemObjects list and match the shown one by name, so both lists are updated.
    static void SetInspectionArray(ItemInspectionHandler handler, Object from, Object to)
    {
        var owners = new List<Component> { handler };
        owners.AddRange(Object.FindObjectsByType<InventoryDescriptionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        foreach (var owner in owners)
        {
            var so = new SerializedObject(owner);
            var arr = so.FindProperty("inspectionItemObjects");
            if (arr == null) continue;
            for (int k = 0; k < arr.arraySize; k++)
                if (arr.GetArrayElementAtIndex(k).objectReferenceValue == from) arr.GetArrayElementAtIndex(k).objectReferenceValue = to;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }
    }

    static void SwapInspection(ItemInspectionHandler handler, GameObject pkModel, GameObject mkModel)
    {
        foreach (var pair in new[] { ("Pain Killer", pkModel), ("Med Kit", mkModel) })
        {
            string name = pair.Item1;
            var oldT = FindInspectionObject(handler, name, true);
            var holderT = FindInspectionObject(handler, name, false);
            var original = oldT != null ? oldT : holderT;
            if (original == null) { Debug.LogWarning("[HealingItemsBuilder] No inspection object named " + name); continue; }

            RemoveOurs(original);
            var old = OldBounds(original, out bool any);
            DisableOld(original);

            if (oldT == null)
            {
                holderT = new GameObject(name).transform;
                holderT.gameObject.layer = original.gameObject.layer;
                holderT.SetParent(original.parent, false);
                holderT.SetSiblingIndex(original.GetSiblingIndex() + 1);
                original.name = name + OldSuffix;
                original.gameObject.SetActive(false);
            }
            else if (holderT != null && holderT != oldT)
                RemoveOurs(holderT);
            SetInspectionArray(handler, original.gameObject, holderT.gameObject);

            var mb = ModelBounds(pair.Item2);
            float scale = any ? old.size.magnitude / mb.size.magnitude : 1f;
            if (name == "Pain Killer") scale *= 1.3f;   // small object, fill the preview like the Med Kit does
            Transform itemRoot = holderT.parent != null ? holderT.parent : holderT;
            // a slight tilt toward the inspection camera
            Quaternion rot = itemRoot.rotation * Quaternion.Euler(-12f, 0f, 0f);
            Vector3 center = any ? old.center : holderT.position;
            Vector3 pivot = center - rot * (mb.center * scale);
            AddModel(holderT, pair.Item2, scale, rot, pivot, holderT.gameObject.layer);
            holderT.gameObject.SetActive(false);   // the handler switches the shown item on by name, like the originals
        }
        EditorUtility.SetDirty(handler);
    }

    static void RestoreInspection(ItemInspectionHandler handler)
    {
        foreach (var name in new[] { "Med Kit", "Pain Killer" })
        {
            var oldT = FindInspectionObject(handler, name, true);
            if (oldT == null) continue;   // never swapped
            var holderT = FindInspectionObject(handler, name, false);
            RestoreModel(oldT);
            if (holderT != null && holderT != oldT)
            {
                SetInspectionArray(handler, holderT.gameObject, oldT.gameObject);
                Object.DestroyImmediate(holderT.gameObject);
            }
            oldT.name = name;
            oldT.gameObject.SetActive(false);
        }
        EditorUtility.SetDirty(handler);
    }

    // earlier builds edited the shared prefab; put it back the way it was
    static void CleanInspectionPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(InspectionPrefab);
        var handler = root.GetComponent<ItemInspectionHandler>();
        if (handler != null) RestoreInspection(handler);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == ModelName) { RemoveOurs(root.transform); break; }
        PrefabUtility.SaveAsPrefabAsset(root, InspectionPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void RestorePrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        var inspectable = root.GetComponent<InspectableItemPickup>();
        if (inspectable != null) inspectable.instantPickup = false;
        RestoreModel(root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void RestoreModel(Transform root)
    {
        RemoveOurs(root);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    // ------------------------------------------------------------------ scene

    static void BuildHUD(Scene scene, Sprite pkIcon, Sprite mkIcon)
    {
        foreach (var old in Object.FindObjectsByType<HealingItemsHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);

        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.name == "GameCanvas" && c.gameObject.scene == scene) { canvas = c; break; }
        if (canvas == null) { Debug.LogWarning("[HealingItemsBuilder] No GameCanvas in " + scene.name + " - banner not added"); return; }

        var go = new GameObject("HealingItemsHUD", typeof(RectTransform), typeof(RawImage));
        Undo.RegisterCreatedObjectUndo(go, "Healing Items HUD");
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);
        // above the low-health vignette so it's still readable when it shows as a reminder
        var vignette = canvas.transform.Find("RedVignetteOverlay");
        go.transform.SetSiblingIndex(vignette != null ? vignette.GetSiblingIndex() + 1 : canvas.transform.childCount - 1);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        img.enabled = false;

        var hud = go.AddComponent<HealingItemsHUD>();
        var so = new SerializedObject(hud);
        var entries = so.FindProperty("entries");
        entries.arraySize = 2;
        SetEntry(entries.GetArrayElementAtIndex(0), AssetDatabase.LoadAssetAtPath<Item>(PainkillerItemPath), "Painkillers", pkIcon);
        SetEntry(entries.GetArrayElementAtIndex(1), AssetDatabase.LoadAssetAtPath<Item>(MedKitItemPath), "Medic Kit", mkIcon);
        so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        so.FindProperty("sandBand").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(SandBandPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";

    // tap G = Painkiller, hold G = Med Kit (PlayerQuickHeal on the Player prefab, so every scene gets it)
    static void BuildQuickHeal()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        var qh = root.GetComponent<PlayerQuickHeal>();
        if (qh == null) qh = root.AddComponent<PlayerQuickHeal>();

        InputActionReference heal = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/HorrorInput.inputactions"))
            if (o is InputActionReference r && r.name == "Player/Heal") { heal = r; break; }
        AudioClip clip = null;
        foreach (var guid in AssetDatabase.FindAssets("ButtonUsing t:AudioClip"))
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));

        var so = new SerializedObject(qh);
        so.FindProperty("healInput").objectReferenceValue = heal;
        so.FindProperty("painkiller").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>(PainkillerItemPath);
        so.FindProperty("medKit").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Item>(MedKitItemPath);
        so.FindProperty("useClip").objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (heal == null) Debug.LogWarning("[HealingItemsBuilder] Player/Heal input reference not found - G will do nothing");

        // the prefab was saved while the heartbeat still defaulted on - keep it off everywhere (user: no pulsing)
        var lowHealth = root.GetComponent<LowHealthEffect>();
        if (lowHealth != null)
        {
            var lso = new SerializedObject(lowHealth);
            lso.FindProperty("heartbeat").boolValue = false;
            lso.ApplyModifiedPropertiesWithoutUndo();
        }

        // use animations: "UseItem" layer + painkiller bottle in the right hand
        var pkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PainkillerClipPath);
        var mkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MedKitClipPath);
        var animator = root.GetComponentInChildren<Animator>(true);
        if (pkClip != null && mkClip != null && animator != null) EnsureUseItemLayer(pkClip, mkClip, UseItemMask(animator.transform));
        else Debug.LogWarning("[HealingItemsBuilder] Use animations missing - run AnimationTools/healing_items/use_anims_build.py + KhalidClipImporter first");
        var prop = BuildPainkillerProp(root.transform);
        var syringe = BuildSyringeProp(root.transform);
        so.Update();
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("painkillerProp").objectReferenceValue = prop;
        so.FindProperty("medKitProp").objectReferenceValue = syringe;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    const string ControllerPath = "Assets/MainCharacter/MainCharacterAnim.controller";
    const string PainkillerClipPath = "Assets/MainCharacter/Khalid_UsePainkiller.anim";
    const string MedKitClipPath = "Assets/MainCharacter/Khalid_UseMedKit.anim";
    const string UseLayerName = "UseItem";
    const string PropName = "PainkillerProp";
    // the bottle in hand.r space (metres; hand.r: +Y fingers, +X thumb side, -Z palm side): cap toward the thumb
    static readonly Vector3 PropPos = new Vector3(-0.045f, 0.085f, -0.035f);
    static readonly Vector3 PropEuler = new Vector3(0f, 0f, -90f);

    const string UseMaskPath = "Assets/Animations/UseItemUpperBody.mask";

    // Khalid is a Generic (Auto-Rig Pro) rig, so a humanoid body-part mask (like the Shooting layer's "UpperBody", which has
    // no transform entries) masks nothing - the heal clip then overrode his legs too and he slid instead of walking.
    // This transform mask keeps spine / neck / head / shoulders / arms / hands / fingers and leaves the root, hips and legs
    // to the movement layer, so he walks through the animation.
    static AvatarMask UseItemMask(Transform animatorRoot)
    {
        // paths relative to the animator root, like the clip curves ("char_grp/rig/..."): AddTransformPath would prefix the
        // root's own name and then nothing matches, masking the whole layer out
        var all = animatorRoot.GetComponentsInChildren<Transform>(true);
        var mask = new AvatarMask { name = "UseItemUpperBody", transformCount = all.Length };
        for (int i = 0; i < all.Length; i++)
        {
            string path = AnimationUtility.CalculateTransformPath(all[i], animatorRoot);
            string leaf = all[i].name.ToLowerInvariant();
            bool upper = false;
            if (all[i] != animatorRoot)
                foreach (var key in new[] { "spine", "neck", "head", "shoulder", "arm", "hand", "index", "thumb", "middle", "ring", "pinky" })
                    if (leaf.Contains(key)) { upper = true; break; }
            mask.SetTransformPath(i, path);
            mask.SetTransformActive(i, upper);
        }
        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(UseMaskPath);
        if (existing == null) { AssetDatabase.CreateAsset(mask, UseMaskPath); return mask; }
        EditorUtility.CopySerialized(mask, existing);
        Object.DestroyImmediate(mask);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // Upper-body override layer (transform mask above, so he can still walk). Its weight stays 0 and PlayerQuickHeal
    // blends it in only while an item is used, so the empty default state never touches the layers below.
    static void EnsureUseItemLayer(AnimationClip pkClip, AnimationClip mkClip, AvatarMask mask)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
        var layers = new List<UnityEditor.Animations.AnimatorControllerLayer>(ctrl.layers);
        int insertAt = layers.Count;
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (layers[i].name == UseLayerName)
            {
                if (layers[i].stateMachine != null) Object.DestroyImmediate(layers[i].stateMachine, true);
                layers.RemoveAt(i);
            }
        }
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].name == "Shooting") insertAt = i + 1;

        var sm = new UnityEditor.Animations.AnimatorStateMachine { name = UseLayerName, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ctrl);
        var empty = sm.AddState("Empty");
        sm.defaultState = empty;
        var pk = sm.AddState("UsePainkiller");
        pk.motion = pkClip;
        pk.speed = pkClip.length / 1.85f;      // ~1.85 s, matches PlayerQuickHeal.painkillerDuration
        var mk = sm.AddState("UseMedKit");
        mk.motion = mkClip;
        mk.speed = mkClip.length / 2.5f;       // ~2.5 s, matches PlayerQuickHeal.medKitDuration

        layers.Insert(insertAt, new UnityEditor.Animations.AnimatorControllerLayer
        {
            name = UseLayerName,
            stateMachine = sm,
            avatarMask = mask,
            defaultWeight = 0f,
            blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Override
        });
        // the reload on the Shooting layer had the same do-nothing humanoid mask, so it froze his legs - with this one he
        // can walk while reloading (PlayerMovement lets him, slowly)
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].name == "Shooting")
            {
                layers[i].avatarMask = mask;
                SetReloadClip(layers[i].stateMachine, AssetDatabase.LoadAssetAtPath<AnimationClip>(NewReloadClipPath), 2.2f);
            }
        ctrl.layers = layers.ToArray();
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    const string NewReloadClipPath = "Assets/MainCharacter/Khalid_Reload_Pistol.anim";
    const string OldReloadClipPath = "Assets/MainCharacter/Reload.anim";

    // The pistol reload (user: the old one looked bad): Mixamo "Gun Reload While Walking" retargeted by
    // AnimationTools/healing_items/use_anims_build.py -- reload. The state keeps its ReloadTimeMultiplier speed parameter;
    // its base speed makes one clip last 'seconds' (PlayerWeaponSystem.reloadDuration, 2.2 s at full stability).
    static void SetReloadClip(UnityEditor.Animations.AnimatorStateMachine sm, AnimationClip clip, float seconds)
    {
        if (sm == null || clip == null) return;
        foreach (var cs in sm.states)
            if (cs.state.name == "Reload")
            {
                // the old clip's events drive the reload (PlayerEventHandler: "Reload" adds the ammo, "ReloadAnimationEnd"
                // finishes it) - carry them over at the same relative times
                var current = cs.state.motion as AnimationClip;
                var source = current != null && current != clip && AnimationUtility.GetAnimationEvents(current).Length > 0
                    ? current : AssetDatabase.LoadAssetAtPath<AnimationClip>(OldReloadClipPath);
                if (source != null && source != clip)
                {
                    var events = AnimationUtility.GetAnimationEvents(source);
                    foreach (var e in events)
                    {
                        e.time = Mathf.Min(e.time / source.length, 0.98f) * clip.length;
                        // the ammo goes in when the magazine does - and an event on frame 0 gets skipped when the state
                        // is entered mid-transition (reloading while walking kept the old ammo)
                        if (e.functionName == "Reload") e.time = 0.5f * clip.length;
                    }
                    AnimationUtility.SetAnimationEvents(clip, events);
                    EditorUtility.SetDirty(clip);
                }
                cs.state.motion = clip;
                cs.state.speed = clip.length / seconds;
                // Reload -> Idle waited for exit time 0.91. The clip now ends right when the Reload bool goes false
                // (normalized 1.0), past that point, so the layer stayed stuck in Reload: the next reload couldn't
                // restart it (no animation, no ammo event). Leave as soon as the bool drops instead.
                foreach (var tr in cs.state.transitions)
                    if (tr.destinationState != null && tr.destinationState.name == "Idle")
                    {
                        tr.hasExitTime = false;
                        EditorUtility.SetDirty(tr);
                    }
                EditorUtility.SetDirty(cs.state);
            }
    }

    const string SyringePropName = "MedKitProp";
    const string SyringeFbxPath = "Assets/Models/Syringe/syringe.fbx";
    // the syringe in hand.r space (metres): upright out of the thumb side, the fist on its lower end, needle down out of
    // the pinky side into the shoulder (his palm is pressed on the shoulder, so the barrel has to sit above the fist)
    static readonly Vector3 SyringePos = new Vector3(0.1f, 0.085f, -0.035f);
    static readonly Vector3 SyringeEuler = new Vector3(0f, 0f, -90f);
    const float SyringeScale = 0.9f;

    static Material SyringeMaterial(string name, Color color, float smoothness)
    {
        string path = Dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = Shader.Find("Standard");
        mat.color = color;
        mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // the med kit "injection": the old pickup's syringe model (it never had materials) in his right hand
    static GameObject BuildSyringeProp(Transform playerRoot)
    {
        Transform hand = null;
        foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
            if (t.name == "hand.r") { hand = t; break; }
        if (hand == null) return null;
        var old = hand.Find(SyringePropName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var prop = new GameObject(SyringePropName);
        prop.layer = hand.gameObject.layer;
        prop.transform.SetParent(hand, false);
        float s = 1f / Mathf.Max(1e-6f, hand.lossyScale.x);
        prop.transform.localPosition = SyringePos * s;
        prop.transform.localRotation = Quaternion.Euler(SyringeEuler);
        prop.transform.localScale = Vector3.one * s * SyringeScale;

        var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SyringeFbxPath), prop.transform, false);
        model.name = "Syringe";
        var barrel = SyringeMaterial("SyringeBarrel", new Color(0.86f, 0.9f, 0.92f), 0.75f);
        var serum = SyringeMaterial("SyringeSerum", new Color(0.72f, 0.08f, 0.06f), 0.6f);
        var plunger = SyringeMaterial("SyringePlunger", new Color(0.3f, 0.3f, 0.33f), 0.3f);
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = r.name == "liquid" ? serum : r.name == "pump" ? plunger : barrel;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (var t in prop.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = prop.layer;
        prop.SetActive(false);
        return prop;
    }

    static GameObject BuildPainkillerProp(Transform playerRoot)
    {
        Transform hand = null;
        foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
            if (t.name == "hand.r") { hand = t; break; }
        if (hand == null) { Debug.LogWarning("[HealingItemsBuilder] No hand.r bone - painkiller prop not added"); return null; }
        var old = hand.Find(PropName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var prop = new GameObject(PropName);
        prop.layer = hand.gameObject.layer;
        prop.transform.SetParent(hand, false);
        float s = 1f / Mathf.Max(1e-6f, hand.lossyScale.x);   // hand.r lossyScale is 100: bone-local units are 1/100 m
        prop.transform.localPosition = PropPos * s;
        prop.transform.localRotation = Quaternion.Euler(PropEuler);
        prop.transform.localScale = Vector3.one * s;
        var bottle = NewPart("Bottle", prop.transform, FbxMesh(PainkillerFbxPath, "PainkillerBottle"), AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Painkiller.mat"));
        bottle.layer = prop.layer;
        bottle.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        prop.SetActive(false);
        return prop;
    }

    static void RemoveQuickHeal()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        var qh = root.GetComponent<PlayerQuickHeal>();
        if (qh != null) Object.DestroyImmediate(qh);
        foreach (var name in new[] { PropName, SyringePropName })
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) { Object.DestroyImmediate(t.gameObject); break; }
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
        var layers = new List<UnityEditor.Animations.AnimatorControllerLayer>(ctrl.layers);
        layers.RemoveAll(l => l.name == UseLayerName);
        var original = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Animations/UpperBody.mask");
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].name == "Shooting")
            {
                var l = layers[i]; l.avatarMask = original; layers[i] = l;
                var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(OldReloadClipPath);
                if (old != null) SetReloadClip(l.stateMachine, old, old.length / 1.3f);   // the old state ran at speed 1.3
            }
        ctrl.layers = layers.ToArray();
        EditorUtility.SetDirty(ctrl);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void SetEntry(SerializedProperty p, Item item, string label, Sprite icon)
    {
        p.FindPropertyRelative("item").objectReferenceValue = item;
        p.FindPropertyRelative("label").stringValue = label;
        p.FindPropertyRelative("icon").objectReferenceValue = icon;
    }

    static List<Image> VignetteImages()
    {
        var list = new List<Image>();
        foreach (var e in Object.FindObjectsByType<LowHealthEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var arr = new SerializedObject(e).FindProperty("vignetteOverlays");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue is Image img) list.Add(img);
        }
        return list;
    }

    static void ApplyVignette(Scene scene, Sprite blood)
    {
        foreach (var img in VignetteImages())
        {
            Undo.RecordObject(img, "Blood vignette");
            img.sprite = blood;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
        }
        // fade in over about a second instead of the old 5 s, so the warning lands while it matters
        foreach (var e in Object.FindObjectsByType<LowHealthEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(e);
            so.FindProperty("effectSmoothness").floatValue = 1.2f;
            so.FindProperty("strength").floatValue = 1.6f;
            so.FindProperty("heartbeat").boolValue = false;   // user: no pulsing
            so.ApplyModifiedProperties();
        }
    }

    static void BuildTestKit(Scene scene)
    {
        var existing = GameObject.Find(TestKitName);
        if (existing != null) Object.DestroyImmediate(existing);
        var player = Object.FindFirstObjectByType<Player>();
        if (player == null) return;

        var kit = new GameObject(TestKitName);
        Undo.RegisterCreatedObjectUndo(kit, "Healing test kit");
        Transform p = player.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        kit.transform.position = p.position + fwd * 3f;

        var pk = AssetDatabase.LoadAssetAtPath<GameObject>(PainkillerPickupPath);
        var mk = AssetDatabase.LoadAssetAtPath<GameObject>(MedKitPickupPath);
        Place(pk, kit.transform, p.position + fwd * 3f - right * 1.2f);
        Place(pk, kit.transform, p.position + fwd * 3f - right * 0.5f);
        Place(mk, kit.transform, p.position + fwd * 3f + right * 0.6f);
        Place(mk, kit.transform, p.position + fwd * 3f + right * 1.5f);
    }

    static void Place(GameObject prefab, Transform parent, Vector3 spot)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        // the pickup prefabs keep odd root offsets: move so the visible model lands on the ground at 'spot'
        var b = new Bounds(); bool any = false;
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            if (r.enabled) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        float ground = spot.y;
        if (Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
        Vector3 target = new Vector3(spot.x, ground, spot.z);
        Vector3 from = any ? new Vector3(b.center.x, b.min.y, b.center.z) : inst.transform.position;
        inst.transform.position += target - from;
        // each copy needs its own save ID
        foreach (var id in inst.GetComponentsInChildren<UniqueID>())
        {
            var so = new SerializedObject(id);
            so.FindProperty("id").stringValue = System.Guid.NewGuid().ToString();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
