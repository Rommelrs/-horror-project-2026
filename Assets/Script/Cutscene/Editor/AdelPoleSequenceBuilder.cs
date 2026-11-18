using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Cutscenes > Build Adel Pole Sequence (Game scene). See AdelPoleSequence. Builds 'Between Alley 2-3 Gameplay/
/// Adel Pole Sequence': Adel (copy of 'Bridge Adel') tied to the light pole on the middle street's corner (Environment7/POLES/
/// Pole (4)) with rope made here (tube meshes, the wardrobe rope's PS2 texture), the cut rope for afterwards, his way to Block 3's
/// side gate, the camera at the gate, the two lines. Also digs a small hollow in the sand under the gate's right leaf (the gate
/// leaves only 11 cm - a kid can't get under that); the terrain asset is backed up to AnimationTools/adel_pole first.
/// Safe to run again.
/// </summary>
public static class AdelPoleSequenceBuilder
{
    const string RootName = "Adel Pole Sequence";
    const string MeshDir = "Assets/Models/AdelPole";
    const string RopeMat = "Assets/Models/WardrobeCloth/Materials/WardrobeRope.mat";
    const float RopeR = 0.013f;

    static readonly Vector3 GateHollow = new Vector3(-46.0f, 0f, -41.1f);
    const float HollowDepth = 0.42f, HollowRx = 0.95f, HollowRz = 1.6f;

    [MenuItem("Tools/Cutscenes/Build Adel Pole Sequence (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[AdelPole] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[AdelPole] stop Play mode first"); return; }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform group = all.FirstOrDefault(t => t.name == "Between Alley 2-3 Gameplay");
        Transform runnerT = all.FirstOrDefault(t => t.name == "RunnerEnemy (1)" && t.parent == group);
        Transform pole = all.FirstOrDefault(t => t.name == "Pole (4)" && t.parent != null && t.parent.name == "POLES" && t.parent.parent != null && t.parent.parent.name == "Environment7");
        Transform bridgeAdel = all.FirstOrDefault(t => t.name == "Bridge Adel");
        var block3 = all.Select(t => t.GetComponent<BurnableObstacle>()).FirstOrDefault(b => b != null && b.name == "ClothBlockage");
        if (group == null || runnerT == null || pole == null || bridgeAdel == null)
        { Debug.LogError("[AdelPole] Between Alley 2-3 Gameplay / RunnerEnemy (1) / Pole (4) / Bridge Adel not found"); return; }

        DigGateHollow(scene);

        var old = group.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName);
        root.transform.SetParent(group, false);
        var seq = root.AddComponent<AdelPoleSequence>();

        // ---- the pole: its shaft's centre and radius (measured by rays at hip height)
        var shaft = pole.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Pole");
        Vector3 pc = shaft.bounds.center;
        float groundY = GroundY(new Vector3(pc.x + 0.6f, 5f, pc.z + 0.6f));
        float radius = 0.08f;
        var hitR = new List<float>();
        foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            Vector3 o = new Vector3(pc.x, groundY + 1.0f, pc.z) - dir * 1.5f;
            if (Physics.Raycast(o, dir, out RaycastHit h, 1.5f, ~0, QueryTriggerInteraction.Ignore) && h.collider.transform.IsChildOf(pole))
                hitR.Add(1.5f - h.distance);
        }
        if (hitR.Count > 0) radius = Mathf.Clamp(hitR.Average(), 0.05f, 0.2f);
        var poleCenter = new GameObject("Pole Centre").transform;
        poleCenter.SetParent(root.transform, false);
        poleCenter.position = new Vector3(pc.x, groundY, pc.z);

        // where Khalid stands to cut him loose: on Adel's right, by the street (Adel faces the street, north)
        Vector3 north = Vector3.forward, east = Vector3.right;
        Vector3 free = new Vector3(pc.x, groundY, pc.z) + east * 0.78f + north * 0.32f;
        Vector3 adelPos = new Vector3(pc.x, groundY, pc.z) + north * (radius + 0.16f);
        adelPos.y = GroundY(adelPos + Vector3.up * 3f);

        // ---- Adel
        var adelGo = Object.Instantiate(bridgeAdel.gameObject, root.transform);
        adelGo.name = "Pole Adel";
        adelGo.transform.SetPositionAndRotation(adelPos, Quaternion.LookRotation(north));
        adelGo.SetActive(false);
        var adelAnim = adelGo.GetComponentInChildren<Animator>(true);

        // ---- rope
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Models", "AdelPole");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RopeMat);
        var tied = RopeObject("Rope (tied)", root.transform, TiedRope(new Vector3(pc.x, adelPos.y, pc.z), radius, adelPos, north), "RopeTied", mat);
        var cut = RopeObject("Rope (cut, on the ground)", root.transform, CutRope(new Vector3(pc.x, groundY, pc.z), radius, north, east), "RopeCut", mat);
        tied.SetActive(false); cut.SetActive(false);

        // ---- his way to the gate (west lane of the side street: the parked cars are in the east one), then under it
        var path = new List<Vector3>
        {
            new Vector3(-60.6f, 0f, -15.2f), new Vector3(-58.0f, 0f, -18.5f), new Vector3(-57.8f, 0f, -33.0f),
            new Vector3(-53.0f, 0f, -36.0f), new Vector3(-46.5f, 0f, -37.8f), new Vector3(-46.0f, 0f, -39.45f)
        };
        for (int i = 0; i < path.Count; i++) path[i] = new Vector3(path[i].x, GroundY(path[i] + Vector3.up * 6f), path[i].z);
        Vector3 crawlTo = new Vector3(-46.0f, 0f, -41.35f); crawlTo.y = GroundY(crawlTo + Vector3.up * 6f);
        var after = new List<Vector3> { new Vector3(-46.3f, 0f, -46.0f), new Vector3(-46.8f, 0f, -52.0f) };
        for (int i = 0; i < after.Count; i++) after[i] = new Vector3(after[i].x, GroundY(after[i] + Vector3.up * 6f), after[i].z);
        // check the way is clear at knee height (logs what's in the way)
        Vector3 prev = adelPos;
        foreach (var p in path)
        {
            if (Physics.Linecast(prev + Vector3.up * 0.5f, p + Vector3.up * 0.5f, out RaycastHit h, ~0, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(pole))
                Debug.LogWarning("[AdelPole] Adel's way " + prev + " -> " + p + " is blocked by " + h.collider.name + " at " + h.point);
            prev = p;
        }

        var gateShot = new GameObject("Gate Shot (from inside Block 3)").transform;
        gateShot.SetParent(root.transform, false);
        Vector3 gs = new Vector3(-45.1f, 0f, -43.5f); gs.y = GroundY(gs + Vector3.up * 6f) + 0.42f;
        gateShot.position = gs;
        gateShot.rotation = Quaternion.LookRotation(new Vector3(-46.0f, crawlTo.y + 0.35f, -40.2f) - gs);

        // ---- the Runner's far spots (the street's two ends, on its NavMesh)
        var far = new List<Vector3>();
        foreach (var s in new[] { new Vector3(-34.5f, 1.5f, -10.0f), new Vector3(-76.0f, 1.5f, -9.6f) })
            far.Add(NavMesh.SamplePosition(s, out NavMeshHit nh, 3f, NavMesh.AllAreas) ? nh.position : s);

        // ---- lines
        Line("All of you are trying to murder me!!", "All of you are trying to murder me!!", "كلكم تبون تقتلوني!!");
        Line("Adel wait!", "Adel wait!", "عادل! اصبر!");

        // ---- wire it
        var so = new SerializedObject(seq);
        so.FindProperty("runner").objectReferenceValue = runnerT.GetComponent<Enemy>();
        var fs = so.FindProperty("runnerFarSpots"); fs.arraySize = far.Count;
        for (int i = 0; i < far.Count; i++) fs.GetArrayElementAtIndex(i).vector3Value = far[i];
        so.FindProperty("adel").objectReferenceValue = adelAnim;
        so.FindProperty("pole").objectReferenceValue = poleCenter;
        so.FindProperty("poleRadius").floatValue = radius;
        so.FindProperty("ropeTied").objectReferenceValue = tied;
        so.FindProperty("ropeCut").objectReferenceValue = cut;
        so.FindProperty("knifeBlade").objectReferenceValue = Resources.LoadAll<Item>("Items").FirstOrDefault(it => it.name == "Knife Blade");
        so.FindProperty("khalidFreeSpot").vector3Value = free;
        var ap = so.FindProperty("adelPath"); ap.arraySize = path.Count;
        for (int i = 0; i < path.Count; i++) ap.GetArrayElementAtIndex(i).vector3Value = path[i];
        so.FindProperty("crawlTo").vector3Value = crawlTo;
        var ag = so.FindProperty("afterGate"); ag.arraySize = after.Count;
        for (int i = 0; i < after.Count; i++) ag.GetArrayElementAtIndex(i).vector3Value = after[i];
        so.FindProperty("gateShot").objectReferenceValue = gateShot;
        if (block3 != null)
        {
            var b3 = new SerializedObject(block3);
            so.FindProperty("ropeCutClip").objectReferenceValue = b3.FindProperty("burnSound").objectReferenceValue;
            var src = block3.GetComponent<AudioSource>();
            if (src != null) so.FindProperty("sfxGroup").objectReferenceValue = src.outputAudioMixerGroup;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[AdelPole] built: pole " + poleCenter.position + " r " + radius.ToString("F3") + ", Adel " + adelPos + ", free spot " + free
                  + ", far spots " + string.Join(" ", far) + ", crawl to " + crawlTo);
    }

    // ------------------------------------------------------------------ the hollow under the gate

    static void DigGateHollow(Scene scene)
    {
        var terrain = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true))
                           .FirstOrDefault(t => t.gameObject.activeInHierarchy && t.terrainData != null
                                                && new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size).Contains(new Vector3(GateHollow.x, t.transform.position.y + 1f, GateHollow.z)));
        if (terrain == null) { Debug.LogWarning("[AdelPole] no terrain under the gate"); return; }
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position, size = data.size;
        int res = data.heightmapResolution;
        float now = terrain.SampleHeight(GateHollow) + tp.y;
        if (now < 0.85f - HollowDepth * 0.6f) { Debug.Log("[AdelPole] the hollow under the gate is already dug (" + now.ToString("F2") + ")"); return; }

        // back the terrain up once (outside Assets so it isn't imported)
        string asset = AssetDatabase.GetAssetPath(data);
        string backupDir = System.IO.Path.GetFullPath("AnimationTools/adel_pole");
        System.IO.Directory.CreateDirectory(backupDir);
        string backup = System.IO.Path.Combine(backupDir, System.IO.Path.GetFileName(asset) + ".before_gate_hollow");
        if (!System.IO.File.Exists(backup)) System.IO.File.Copy(System.IO.Path.GetFullPath(asset), backup);

        float cx = (GateHollow.x - tp.x) / size.x * (res - 1), cz = (GateHollow.z - tp.z) / size.z * (res - 1);
        int x0 = Mathf.FloorToInt(cx - 4), z0 = Mathf.FloorToInt(cz - 6), w = 9, h = 13;
        var heights = data.GetHeights(x0, z0, w, h);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float wx = tp.x + (x0 + i) / (float)(res - 1) * size.x, wz = tp.z + (z0 + j) / (float)(res - 1) * size.z;
                float u = (wx - GateHollow.x) / HollowRx, v = (wz - GateHollow.z) / HollowRz;
                float r2 = u * u + v * v;
                if (r2 >= 1f) continue;
                float k = (1f - r2) * (1f - r2);
                heights[j, i] -= HollowDepth * k / size.y;
            }
        Undo.RegisterCompleteObjectUndo(data, "Dig the hollow under Block 3's side gate");
        data.SetHeights(x0, z0, heights);
        EditorUtility.SetDirty(data);
        Debug.Log("[AdelPole] dug the hollow under the gate: " + now.ToString("F2") + " -> " + (terrain.SampleHeight(GateHollow) + tp.y).ToString("F2") + " (backup " + backup + ")");
    }

    // ------------------------------------------------------------------ rope

    /// <summary>Three loops round the pole and his middle/arms, a knot behind the pole and a loose end.</summary>
    static List<List<Vector3>> TiedRope(Vector3 poleBase, float poleR, Vector3 adelPos, Vector3 facing)
    {
        var loops = new List<List<Vector3>>();
        float[] hs = { 0.62f, 0.80f, 0.97f };
        float[] bodyR = { 0.155f, 0.19f, 0.19f };
        for (int k = 0; k < hs.Length; k++)
        {
            var a = new Vector3(poleBase.x, adelPos.y + hs[k], poleBase.z);
            var b = new Vector3(adelPos.x, adelPos.y + hs[k], adelPos.z) + facing * 0.01f;
            var loop = new List<Vector3>();
            int n = 40;
            for (int i = 0; i <= n; i++)
            {
                float ang = i / (float)n * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                // support point of the hull of the two circles (pole + his body), a bit of slack and a tilt
                float sa = Vector3.Dot(a, d) + poleR + 0.012f, sb = Vector3.Dot(b, d) + bodyR[k];
                Vector3 p = sa > sb ? a + d * (poleR + 0.012f) : b + d * bodyR[k];
                p.y += 0.035f * Mathf.Sin(ang + k * 1.7f) + 0.01f * Mathf.Sin(ang * 3f + k);
                loop.Add(p);
            }
            loops.Add(loop);
        }
        // knot behind the pole and a loose end hanging from it
        Vector3 back = new Vector3(poleBase.x, adelPos.y + 0.8f, poleBase.z) - facing * (poleR + 0.03f);
        var knot = new List<Vector3>();
        for (int i = 0; i <= 14; i++)
        {
            float ang = i / 14f * Mathf.PI * 2f * 1.5f;
            knot.Add(back + new Vector3(Mathf.Cos(ang) * 0.025f, -0.15f + i * 0.02f, Mathf.Sin(ang) * 0.025f) - facing * 0.01f);
        }
        loops.Add(knot);
        var end = new List<Vector3>();
        for (int i = 0; i <= 10; i++) end.Add(back - facing * 0.02f + Vector3.down * (0.03f * i) + Vector3.right * 0.015f * Mathf.Sin(i * 0.7f));
        loops.Add(end);
        return loops;
    }

    /// <summary>The rope after the cut: slack loops lying round the foot of the pole.</summary>
    static List<List<Vector3>> CutRope(Vector3 poleBase, float poleR, Vector3 north, Vector3 east)
    {
        var loops = new List<List<Vector3>>();
        var rnd = new System.Random(4);
        for (int k = 0; k < 3; k++)
        {
            var c = poleBase + north * (0.28f + 0.06f * k) + east * (0.08f * (k - 1));
            float rx = 0.3f + 0.05f * k, rz = 0.22f + 0.04f * k;
            var loop = new List<Vector3>();
            int n = 30;
            float startA = (float)rnd.NextDouble() * 6f, span = Mathf.PI * 2f * (0.8f + 0.15f * k);
            for (int i = 0; i <= n; i++)
            {
                float ang = startA + i / (float)n * span;
                float wob = 1f + 0.12f * Mathf.Sin(ang * 3f + k);
                var p = c + east * Mathf.Cos(ang) * rx * wob + north * Mathf.Sin(ang) * rz * wob;
                p.y = GroundY(new Vector3(p.x, poleBase.y + 0.4f - 3f, p.z)) + RopeR + 0.004f + 0.01f * k;     // from just above the ground: not the pole / lamp
                loop.Add(p);
            }
            loops.Add(loop);
        }
        return loops;
    }

    static GameObject RopeObject(string name, Transform parent, List<List<Vector3>> strands, string meshName, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var m = new UnityEngine.Mesh { name = meshName };
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);       // the strands are in world space
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        foreach (var s in strands) Tube(s, RopeR, verts, uvs, tris);
        for (int i = 0; i < verts.Count; i++) verts[i] = go.transform.InverseTransformPoint(verts[i]);
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        string path = MeshDir + "/" + meshName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(m, existing); m = existing; }
        else AssetDatabase.CreateAsset(m, path);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static void Tube(List<Vector3> pts, float radius, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        const int sides = 6;
        int baseIndex = verts.Count;
        Vector3 nrm = Vector3.up;
        float len = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 t = (pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
            if (i == 0) { nrm = Vector3.ProjectOnPlane(Mathf.Abs(t.y) > 0.9f ? Vector3.right : Vector3.up, t).normalized; }
            else { len += (pts[i] - pts[i - 1]).magnitude; nrm = Vector3.ProjectOnPlane(nrm, t).normalized; }
            Vector3 bi = Vector3.Cross(t, nrm);
            for (int k = 0; k <= sides; k++)
            {
                float a = k / (float)sides * Mathf.PI * 2f;
                verts.Add(pts[i] + (nrm * Mathf.Cos(a) + bi * Mathf.Sin(a)) * radius);
                uvs.Add(new Vector2(k / (float)sides, len / 0.15f));
            }
        }
        for (int i = 0; i < pts.Count - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = baseIndex + i * (sides + 1) + k, b = a + sides + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
    }

    // ------------------------------------------------------------------ helpers

    static float GroundY(Vector3 from)
    {
        var hits = Physics.RaycastAll(new Vector3(from.x, from.y + 3f, from.z), Vector3.down, 15f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
        {
            var r = h.collider.GetComponent<Renderer>();
            if (r != null && !r.enabled && !(h.collider is TerrainCollider)) continue;
            if (h.collider.GetComponentInParent<Player>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.NegativeInfinity ? best : from.y;
    }

    static long Line(string key, string en, string ar)
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
        return entry.Id;
    }
}
