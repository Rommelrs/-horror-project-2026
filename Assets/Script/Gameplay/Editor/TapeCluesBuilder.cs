using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Alley 3 readability (user 2026-11-21): the whispering hole gets torn duct tape round its rim (someone sealed it before and
/// it was ripped open), and a hole on the hanged-man wall is shown already taped shut - it teaches "tape the hole" before the
/// whispering one. Re-runnable: Tools > Prototype > Build Tape Clues (rebuilds 'Alley 3 Gameplay/Tape Clues', saves Game).
/// Textures: AnimationTools/duct_tape/make_duct_tape_textures.py. Nothing of the user's hole / door / papers is touched.
/// </summary>
public static class TapeCluesBuilder
{
    const string Dir = "Assets/Models/DuctTape";
    const string AtlasPath = Dir + "/Textures/DuctTape_Atlas.png";
    const string HoleTexPath = Dir + "/Textures/TapedHole_Back.png";
    const float RowLength = 0.384f;     // one atlas row = 512 px along, 64 px = 48 mm across
    const float TapeWidth = 0.05f;
    const float WallZ = -76.46f;        // front face of WallsHole along Alley 3 (normal +z, toward the lane)
    static readonly Vector3 WhisperHole = new Vector3(-18.62f, 3.13f, WallZ);
    static readonly Vector3 TapedHole = new Vector3(-46.90f, 2.50f, WallZ);   // the spot the user circled
    const float GroundY = 0.85f;

    class Strip
    {
        public List<Vector3> pts = new List<Vector3>();
        public Vector3 up = Vector3.forward;   // surface normal hint (front face points this way)
        public int row, backRow = 4;
        public float width = TapeWidth;
    }

    [MenuItem("Tools/Prototype/Build Tape Clues")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TapeClues] stop Play first"); return; }
        SetupTexture(AtlasPath);
        SetupTexture(HoleTexPath);
        // both walls stand in shade: a little self-light keeps the silver tape reading against the dark bricks
        var tapeMat = Mat(Dir + "/Materials/DuctTape.mat", AtlasPath, 0.35f, 0f, 0.42f);
        var holeMat = Mat(Dir + "/Materials/TapedHole_Back.mat", HoleTexPath, 0.05f, 0f, 0.12f);

        WallHoleInteractable whisper = null;
        foreach (var w in Object.FindObjectsByType<WallHoleInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w.transform.parent != null && w.transform.parent.name == "Alley 3 Gameplay") whisper = w;
        if (whisper == null) { Debug.LogError("[TapeClues] Alley 3 whispering hole not found"); return; }
        var alley3 = whisper.transform.parent;

        var old = alley3.Find("Tape Clues");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Tape Clues").transform;
        root.SetParent(alley3, false);

        // ---- 1. torn tape round the whispering hole ----
        var ripped = new GameObject("Ripped Tape (whispering hole)").transform;
        ripped.SetParent(root, false);
        ripped.position = WhisperHole;
        AddMesh(ripped.gameObject, SaveMesh(BuildMesh(RippedStrips()), Dir + "/Meshes/RippedTape_WhisperingHole.asset"), tapeMat);

        // ---- 2. a hole already taped shut on the hanged-man wall ----
        var taped = new GameObject("Taped Hole (hanged man wall)").transform;
        taped.SetParent(root, false);
        taped.position = TapedHole;
        var back = new GameObject("Hole (under the tape)");
        back.transform.SetParent(taped, false);
        back.transform.localPosition = new Vector3(0f, 0f, 0.003f);
        AddMesh(back, SaveMesh(Quad(1.05f), Dir + "/Meshes/TapedHole_Back.asset"), holeMat);
        var tape = new GameObject("Tape");
        tape.transform.SetParent(taped, false);
        AddMesh(tape, SaveMesh(BuildMesh(TapedStrips()), Dir + "/Meshes/TapedHole_Tape.asset"), tapeMat);

        // the Player next to the new taped hole, facing the wall (user's iterate-from-Play habit)
        var player = Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Place Player");
            player.transform.SetPositionAndRotation(new Vector3(-45.6f, 0.9f, -73.2f), Quaternion.Euler(0f, 200f, 0f));
        }

        var scene = alley3.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[TapeClues] built: ripped tape at the whispering hole, taped hole on the hanged-man wall; Game saved");
    }

    // ------------------------------------------------------------------ layouts (local: x along the wall, y up, z out)

    static List<Strip> RippedStrips()
    {
        var s = new List<Strip>();
        // remnants: stuck flat outside the rim, torn at the edge, the torn end curling into the hole
        void Remnant(float sx, float sy, float ex, float ey, int row, int backRow = 4, float layer = 0f)
        {
            var a = new Vector3(sx, sy, 0f); var e = new Vector3(ex, ey, 0f);
            var d = (e - a).normalized;
            float z = 0.004f + layer;
            var st = new Strip { row = row, backRow = backRow };
            int n = 5;
            for (int i = 0; i <= n; i++) st.pts.Add(Vector3.Lerp(a, e, i / (float)n) + new Vector3(0, 0, z));
            st.pts.Add(e + d * 0.025f + new Vector3(0f, -0.006f, -0.012f));
            st.pts.Add(e + d * 0.045f + new Vector3(0f, -0.02f, -0.05f));
            s.Add(st);
        }
        // the same horizontal rows as a taped hole, with the middle torn out: stubs left on both sides of the rim
        var r = new System.Random(21);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        int k = 0;
        for (float y = -0.27f; y <= 0.31f; y += 0.052f, k++)
        {
            float xl, xr;
            if (!HoleSpan(y, out xl, out xr)) continue;
            float tilt = Rnd(-0.02f, 0.02f);
            int row = (k % 3 == 0) ? 5 : (k % 3 == 1) ? 6 : 3;
            if (k != 3) Remnant(xl - Rnd(0.10f, 0.22f), y - tilt, xl + 0.01f, y, row, 7, k * 0.0004f);
            if (k != 6 && k != 9) Remnant(xr + Rnd(0.10f, 0.22f), y + tilt, xr - 0.01f, y, (row + 1) % 7, 7, k * 0.0004f);
        }
        // the X that went under the rows: its four corners are left
        Remnant(-0.33f, 0.43f, -0.23f, 0.33f, 1, 4, 0.006f);
        Remnant(0.36f, 0.34f, 0.24f, 0.24f, 2, 4, 0.006f);
        Remnant(-0.34f, -0.33f, -0.24f, -0.23f, 0, 4, 0.006f);
        Remnant(0.29f, -0.32f, 0.19f, -0.22f, 3, 4, 0.006f);

        // two rows peeled half off: still stuck on one side, the torn free end hanging down into the opening
        var h1 = new Strip { row = 6, backRow = 7 };
        h1.pts.AddRange(new[] {
            new Vector3(-0.50f, 0.17f, 0.007f), new Vector3(-0.40f, 0.17f, 0.007f), new Vector3(-0.27f, 0.17f, 0.007f),
            new Vector3(-0.21f, 0.13f, -0.02f), new Vector3(-0.18f, 0.03f, -0.05f), new Vector3(-0.17f, -0.09f, -0.06f),
            new Vector3(-0.165f, -0.19f, -0.05f) });
        s.Add(h1);
        var h2 = new Strip { row = 5, backRow = 7 };
        h2.pts.AddRange(new[] {
            new Vector3(0.52f, -0.05f, 0.007f), new Vector3(0.40f, -0.05f, 0.007f), new Vector3(0.29f, -0.05f, 0.007f),
            new Vector3(0.24f, -0.09f, -0.03f), new Vector3(0.22f, -0.17f, -0.07f), new Vector3(0.215f, -0.25f, -0.08f) });
        s.Add(h2);

        // torn-off strips dropped in the sand below the hole (crumpled)
        float gy = GroundY - WhisperHole.y + 0.004f;
        s.Add(Crumpled(new Vector3(-0.25f, gy, 0.32f), 15f, 0.34f, 3, 11));
        s.Add(Crumpled(new Vector3(0.18f, gy, 0.55f), -50f, 0.26f, 6, 12));
        return s;
    }

    // outline of the whispering hole (WallsHole mesh rim, relative to WhisperHole), counter-clockwise from the bottom
    static readonly Vector2[] HoleOutline = {
        new Vector2(-0.07f, -0.34f), new Vector2(-0.34f, -0.20f), new Vector2(-0.28f, 0.08f), new Vector2(-0.23f, 0.38f),
        new Vector2(-0.12f, 0.34f), new Vector2(0.07f, 0.28f), new Vector2(0.35f, 0.19f), new Vector2(0.27f, -0.15f),
        new Vector2(0.13f, -0.23f) };

    static bool HoleSpan(float y, out float xl, out float xr)
    {
        xl = float.MaxValue; xr = float.MinValue;
        for (int i = 0; i < HoleOutline.Length; i++)
        {
            var a = HoleOutline[i]; var b = HoleOutline[(i + 1) % HoleOutline.Length];
            if ((a.y - y) * (b.y - y) > 0f || Mathf.Approximately(a.y, b.y)) continue;
            float x = Mathf.Lerp(a.x, b.x, (y - a.y) / (b.y - a.y));
            xl = Mathf.Min(xl, x); xr = Mathf.Max(xr, x);
        }
        return xr > xl;
    }

    static Strip Crumpled(Vector3 at, float yaw, float length, int row, int seed)
    {
        var r = new System.Random(seed);
        var st = new Strip { row = row, backRow = 7, up = Vector3.up };
        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        int n = 7;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float h = (i % 2 == 1) ? 0.008f + (float)r.NextDouble() * 0.012f : 0.001f;
            var side = Vector3.Cross(Vector3.up, dir) * (float)(r.NextDouble() - 0.5) * 0.02f;
            st.pts.Add(at + dir * (t * length) + side + Vector3.up * h);
        }
        return st;
    }

    static List<Strip> TapedStrips()
    {
        var s = new List<Strip>();
        var r = new System.Random(7);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        // first an X over the hole, then rows of strips across it; two thin gaps let the black show through
        Strip Line(Vector3 a, Vector3 b, int row, float z, bool peelEnd)
        {
            var st = new Strip { row = row };
            int n = 6;
            for (int i = 0; i <= n; i++) st.pts.Add(Vector3.Lerp(a, b, i / (float)n) + new Vector3(0, 0, z));
            if (peelEnd) { var d = (b - a).normalized; st.pts.Add(b + d * 0.05f + new Vector3(0f, -0.01f, z + 0.025f)); }
            return st;
        }
        s.Add(Line(new Vector3(-0.34f, 0.33f, 0), new Vector3(0.33f, -0.35f, 0), 1, 0.004f, false));
        s.Add(Line(new Vector3(-0.33f, -0.33f, 0), new Vector3(0.35f, 0.31f, 0), 3, 0.0048f, false));
        int k = 0;
        for (float y = -0.28f; y <= 0.29f; y += 0.046f, k++)
        {
            if (k == 3 || k == 9) continue;                      // gaps: the dark hole shows between the strips
            float half = Rnd(0.30f, 0.37f), tilt = Rnd(-0.03f, 0.03f), dx = Rnd(-0.04f, 0.04f);
            var a = new Vector3(-half + dx, y - tilt, 0); var b = new Vector3(half + dx, y + tilt, 0);
            bool flip = (k % 2) == 1;
            s.Add(Line(flip ? b : a, flip ? a : b, k % 4 == 3 ? 2 : k % 4, 0.0056f + k * 0.0006f, k == 2 || k == 9 || k == 13));
        }
        // a last pair of strips pressed over the top + bottom edges
        s.Add(Line(new Vector3(-0.26f, 0.31f, 0), new Vector3(0.27f, 0.30f, 0), 1, 0.0145f, false));
        s.Add(Line(new Vector3(0.28f, -0.31f, 0), new Vector3(-0.24f, -0.32f, 0), 0, 0.015f, true));
        return s;
    }

    // ------------------------------------------------------------------ mesh

    static Mesh BuildMesh(List<Strip> strips)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        foreach (var st in strips)
        {
            for (int face = 0; face < 2; face++)
            {
                int row = face == 0 ? st.row : st.backRow;
                float v0 = (row * 64 + 1) / 512f, v1 = ((row + 1) * 64 - 1) / 512f;
                // arc length
                var len = new List<float> { 0f };
                for (int i = 1; i < st.pts.Count; i++) len.Add(len[i - 1] + Vector3.Distance(st.pts[i - 1], st.pts[i]));
                float L = len[len.Count - 1];
                // u: torn ends at both ends; short strips take each end from its own side of the row (seam in the middle)
                bool split = L < RowLength;
                float U(float sArc, bool rightHalf) => !split ? sArc / L : (rightHalf ? 1f - (L - sArc) / RowLength : sArc / RowLength);
                int baseIdx = v.Count;
                var ring = new List<int[]>();
                for (int i = 0; i < st.pts.Count; i++)
                {
                    var t = (i == 0 ? st.pts[1] - st.pts[0] : i == st.pts.Count - 1 ? st.pts[i] - st.pts[i - 1] : st.pts[i + 1] - st.pts[i - 1]).normalized;
                    var side = Vector3.Cross(st.up, t).normalized * (st.width * 0.5f);
                    if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(Vector3.up, t).normalized * (st.width * 0.5f);
                    var off = face == 0 ? Vector3.zero : -Vector3.Cross(t, side).normalized * 0.0004f;
                    var p = st.pts[i] + off;
                    bool atMid = split && i > 0 && len[i - 1] < L * 0.5f && len[i] >= L * 0.5f;
                    // (insert the seam: an extra vertex pair at the middle point carries the right-half UV)
                    if (atMid)
                    {
                        float sm = L * 0.5f;
                        float f = (sm - len[i - 1]) / Mathf.Max(1e-5f, len[i] - len[i - 1]);
                        var pm = Vector3.Lerp(st.pts[i - 1], st.pts[i], f) + off;
                        int a0 = v.Count; v.Add(pm - side); v.Add(pm + side);
                        float ul = U(sm, false); uv.Add(new Vector2(ul, v0)); uv.Add(new Vector2(ul, v1));
                        ring.Add(new[] { a0, a0 + 1 });
                        int b0 = v.Count; v.Add(pm - side); v.Add(pm + side);
                        float ur = U(sm, true); uv.Add(new Vector2(ur, v0)); uv.Add(new Vector2(ur, v1));
                        ring.Add(new[] { -1, -1 });                    // break: no quad between the seam pair
                        ring.Add(new[] { b0, b0 + 1 });
                    }
                    int idx = v.Count; v.Add(p - side); v.Add(p + side);
                    float u = U(len[i], split && len[i] >= L * 0.5f);
                    uv.Add(new Vector2(u, v0)); uv.Add(new Vector2(u, v1));
                    ring.Add(new[] { idx, idx + 1 });
                }
                for (int i = 1; i < ring.Count; i++)
                {
                    var a = ring[i - 1]; var b = ring[i];
                    if (a[0] < 0 || b[0] < 0) continue;
                    if (face == 0) { tri.Add(a[0]); tri.Add(b[0]); tri.Add(a[1]); tri.Add(a[1]); tri.Add(b[0]); tri.Add(b[1]); }
                    else { tri.Add(a[0]); tri.Add(a[1]); tri.Add(b[0]); tri.Add(a[1]); tri.Add(b[1]); tri.Add(b[0]); }
                }
            }
        }
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Mesh Quad(float size)
    {
        float h = size * 0.5f;
        var m = new Mesh();
        m.vertices = new[] { new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(-h, h, 0), new Vector3(h, h, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 1, 2, 1, 3, 2 };     // faces +z (out of the wall)
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Mesh SaveMesh(Mesh m, string path)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear(); EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m);
            existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing); return existing;
        }
        m.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void AddMesh(GameObject go, Mesh mesh, Material mat)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void SetupTexture(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { AssetDatabase.ImportAsset(path); ti = (TextureImporter)AssetImporter.GetAtPath(path); }
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipMapsPreserveCoverage = true;
        ti.alphaTestReferenceValue = 0.5f;
        ti.SaveAndReimport();
    }

    static Material Mat(string path, string texPath, float smooth, float metal, float selfLight)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        m.SetFloat("_Mode", 1f);                      // Cutout
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.EnableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", m.mainTexture);
        m.SetColor("_EmissionColor", Color.white * selfLight);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }
}
