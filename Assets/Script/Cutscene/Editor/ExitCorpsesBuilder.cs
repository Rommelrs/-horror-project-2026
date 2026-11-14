using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Tw = DeadMonsterPoses.Tweak;

/// <summary>
/// Store redesign, beat 7 (leaving the store): the bodies behind the store are dead Questioners and Runners instead of
/// the old downloaded human corpse props, and Khalid only says "God protect us...".
/// Game scene: City Props/Corpses gets a "Dead Monsters" group (SceneSwapRecord: right-click > Revert shows the old
/// props again); Cutscene_AfterStore's first subtitle is re-pointed, the second one and the old voice line are silenced.
/// </summary>
public static class ExitCorpsesBuilder
{
    const string MeshDir = "Assets/Models/DeadMonsters";
    const string GroupName = "Dead Monsters";
    const string LineEn = "God protect us...";
    const string LineAr = "يا ساتر...";
    const string OldVoiceClip = "Voice line 1";

    struct Variant
    {
        public string name, prefab, clip; public float t, scale; public bool mirror; public Tw[] tweaks;
        public Variant(string n, string p, string c, float t, float s, bool m, params Tw[] tw) { name = n; prefab = p; clip = c; this.t = t; scale = s; mirror = m; tweaks = tw; }
    }

    // tweaks swing a limb around the vertical axis, so it stays lying on the ground. The bodies lie in a ~2.1 m wide
    // alley: the Questioner arms are pulled in from the spread-eagle of the clips so every body fits between the walls.
    static readonly Variant[] Variants =
    {
        new Variant("Q_FaceDown_A", DeadMonsterPoses.QuestionerPrefab, "Questioner_Die", 1f, 1f, false, new Tw("LeftArm", 22f), new Tw("RightArm", -24f)),
        new Variant("Q_FaceDown_B", DeadMonsterPoses.QuestionerPrefab, "Questioner_Die", 1f, 1f, true, new Tw("LeftArm", 48f), new Tw("RightArm", -45f), new Tw("Neck", 20f)),
        new Variant("Q_FaceDown_C", DeadMonsterPoses.QuestionerPrefab, "Questioner_Die", 1f, 1f, false, new Tw("LeftArm", 70f), new Tw("RightArm", -8f), new Tw("LeftUpLeg", 8f)),
        new Variant("Q_OnBack_A", DeadMonsterPoses.QuestionerPrefab, "Questioner_WakeUp", 0f, 1f, false, new Tw("LeftArm", 20f), new Tw("RightArm", -22f)),
        new Variant("Q_OnBack_B", DeadMonsterPoses.QuestionerPrefab, "Questioner_WakeUp", 0f, 1f, true, new Tw("LeftArm", 46f), new Tw("RightArm", -6f), new Tw("Neck", -22f)),
        new Variant("R_OnBack_A", DeadMonsterPoses.RunnerPrefab, "Die", 1f, 1.1f, false),
        new Variant("R_OnBack_B", DeadMonsterPoses.RunnerPrefab, "Die", 1f, 1.1f, true, new Tw("LeftArm", -48f), new Tw("RightUpLeg", 18f)),
        new Variant("R_FaceDown_A", DeadMonsterPoses.RunnerPrefab, "Weakpoint_hurt_falling_down", 1f, 1.1f, false),
        new Variant("R_FaceDown_B", DeadMonsterPoses.RunnerPrefab, "Weakpoint_hurt_falling_down", 1f, 1.1f, true, new Tw("RightArm", 36f), new Tw("LeftUpLeg", -20f)),
    };

    // one per old corpse, in hierarchy order: which variant lies there (index into Variants)
    static readonly int[] Layout = { 0, 5, 3, 7, 1, 8, 4, 6, 2, 5, 0, 7, 3, 6, 4, 8, 1, 2 };

    [MenuItem("Tools/Cutscenes/Preview Dead Monster Poses")]
    public static void PreviewAll()
    {
        foreach (var v in Variants) DeadMonsterPoses.Preview("v_" + v.name, v.prefab, v.clip, v.t, v.tweaks, v.mirror);
    }

    static Scene Game()
    {
        var s = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        return s;
    }

    static T FindIn<T>(Scene scene, string name, string parentName = null) where T : Component
    {
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<T>(true))
                if (t.name == name && (parentName == null || (t.transform.parent != null && t.transform.parent.name == parentName))) return t;
        return null;
    }

    static Mesh SaveMesh(Variant v, out Material[] mats)
    {
        var tmp = EditorSceneManager.NewPreviewScene();
        try
        {
            var posed = DeadMonsterPoses.Pose(tmp, v.prefab, v.clip, v.t, v.tweaks);
            var baked = DeadMonsterPoses.Bake(posed, v.scale, v.mirror);
            Object.DestroyImmediate(posed);
            mats = baked.materials;
            baked.mesh.name = "Dead_" + v.name;
            string path = MeshDir + "/Dead_" + v.name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(baked.mesh, old); Object.DestroyImmediate(baked.mesh); EditorUtility.SetDirty(old); return old; }
            AssetDatabase.CreateAsset(baked.mesh, path);
            return baked.mesh;
        }
        finally { EditorSceneManager.ClosePreviewScene(tmp); }
    }

    static Material BloodMat(string name, string texPath)
    {
        string path = "Assets/Materials/DeadMonsters/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StoreMakeover/SM_BloodBig.mat");
            m = new Material(src);
            AssetDatabase.CreateAsset(m, path);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (imp != null && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp))
        { imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        m.color = Color.white;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh PoolQuad()
    {
        string path = MeshDir + "/BloodPoolQuad.asset";
        var q = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (q != null) return q;
        q = new Mesh { name = "BloodPoolQuad" };
        q.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
        q.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        q.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        q.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        AssetDatabase.CreateAsset(q, path);
        return q;
    }

    // the line of bodies starts just south of where the back door puts Khalid (z 100.6) and runs down the alley
    const float LineNorth = 99.3f, LineSouth = 69.5f;
    const int LineCount = 14;
    // what closes the alley behind him (north of the door)
    const float BlockadeZ = 104.5f;
    const string RubblePrefab = "Assets/Prefab/AlleyRubble.prefab";

    /// <summary>The alley at z: ground height and the two walls (Game scene colliders only: the Store scene may be loaded on top of it).</summary>
    static void Alley(Scene scene, Transform corpses, float alleyX, float z, float oldFloor, out float ground, out float wallL, out float wallR)
    {
        ground = oldFloor;
        foreach (var hit in Physics.RaycastAll(new Vector3(alleyX, oldFloor + 3f, z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.gameObject.scene == scene && hit.collider is TerrainCollider && Mathf.Abs(hit.point.y - oldFloor) < 0.8f) ground = hit.point.y;
        // the paving that is actually seen lies a few centimetres above the terrain (it has no collider): lie on that
        float paving = PavingY(scene, alleyX, z);
        if (paving > ground && paving < ground + 0.25f) ground = paving;
        wallL = alleyX - 1.5f; wallR = alleyX + 1.5f;
        // probed at three depths: a wall that steps in half way along a body counts
        for (int k = -1; k <= 1; k++)
        {
            Vector3 eye = new Vector3(alleyX, ground + 0.45f, z + k * 0.8f);
            foreach (var hit in Physics.RaycastAll(eye, Vector3.left, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.gameObject.scene == scene && !hit.collider.transform.IsChildOf(corpses)) wallL = Mathf.Max(wallL, hit.point.x);
            foreach (var hit in Physics.RaycastAll(eye, Vector3.right, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.gameObject.scene == scene && !hit.collider.transform.IsChildOf(corpses)) wallR = Mathf.Min(wallR, hit.point.x);
        }
    }

    static Vector3[] pavingTris;

    /// <summary>Height of the alley's paving mesh ("Pavers" of the park back alley re-skin) at x/z, or -inf when there is none there.</summary>
    static float PavingY(Scene scene, float x, float z)
    {
        if (pavingTris == null)
        {
            var list = new List<Vector3>();
            foreach (var r in scene.GetRootGameObjects())
                foreach (var mf in r.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (mf.name != "Pavers" || mf.sharedMesh == null) continue;
                    var v = mf.sharedMesh.vertices; var t = mf.sharedMesh.triangles; var m = mf.transform.localToWorldMatrix;
                    for (int i = 0; i < t.Length; i++) list.Add(m.MultiplyPoint3x4(v[t[i]]));
                }
            pavingTris = list.ToArray();
        }
        float best = float.NegativeInfinity;
        for (int i = 0; i + 2 < pavingTris.Length; i += 3)
        {
            Vector3 a = pavingTris[i], b = pavingTris[i + 1], c = pavingTris[i + 2];
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-9f) continue;
            float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
            float w = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
            if (u < -0.001f || w < -0.001f || u + w > 1.001f) continue;
            float y = u * a.y + w * b.y + (1f - u - w) * c.y;
            if (y > best) best = y;
        }
        return best;
    }

    /// <summary>A direction in which the body fits between the walls: any of them (the line), or the one lying most across the alley (the heap).</summary>
    static void Fit(Mesh mesh, float room, System.Random rnd, bool across, out float yaw, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        var verts = mesh.vertices;
        const int N = 120;
        float[] mnX = new float[N], mxX = new float[N], mnZ = new float[N], mxZ = new float[N];
        var fits = new List<int>(); int narrowest = 0, widestFit = -1;
        for (int k = 0; k < N; k++)
        {
            float a = k * 3f * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int n = 0; n < verts.Length; n += 5)
            {
                float x = verts[n].x * c + verts[n].z * sn, z = -verts[n].x * sn + verts[n].z * c;
                if (x < x0) x0 = x; if (x > x1) x1 = x; if (z < z0) z0 = z; if (z > z1) z1 = z;
            }
            mnX[k] = x0; mxX[k] = x1; mnZ[k] = z0; mxZ[k] = z1;
            if (x1 - x0 < mxX[narrowest] - mnX[narrowest]) narrowest = k;
            if (x1 - x0 <= room) { fits.Add(k); if (widestFit < 0 || x1 - x0 > mxX[widestFit] - mnX[widestFit]) widestFit = k; }
        }
        int pick = fits.Count == 0 ? narrowest : across ? widestFit : fits[rnd.Next(fits.Count)];
        if (across && fits.Count > 0 && rnd.Next(2) == 0) pick = (pick + N / 2) % N;        // head to either wall (same width turned round)
        yaw = pick * 3f; minX = mnX[pick]; maxX = mxX[pick]; minZ = mnZ[pick]; maxZ = mxZ[pick];
    }

    static GameObject Body(Transform group, int layer, Variant v, int number, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot)
    {
        bool questioner = v.prefab == DeadMonsterPoses.QuestionerPrefab;
        var go = new GameObject((questioner ? "Dead Questioner " : "Dead Runner ") + number) { layer = layer };
        go.transform.SetParent(group, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        return go;
    }

    static void Pool(Transform parent, Mesh quad, Material mat, Vector3 localPos, float size, System.Random rnd)
    {
        var pool = new GameObject("Blood") { layer = parent.gameObject.layer };
        pool.transform.SetParent(parent, false);
        pool.transform.localPosition = localPos;
        pool.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
        pool.transform.localScale = new Vector3(size, 1f, size * (0.85f + (float)rnd.NextDouble() * 0.3f));
        pool.AddComponent<MeshFilter>().sharedMesh = quad;
        var pr = pool.AddComponent<MeshRenderer>(); pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    [MenuItem("Tools/Cutscenes/Build Exit Corpses (Game scene)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[ExitCorpses] stop Play first"); return; }
        var scene = Game();
        if (!scene.isLoaded) { Debug.LogError("[ExitCorpses] open the Game scene first (additively is fine)"); return; }
        var corpses = FindIn<Transform>(scene, "Corpses", "City Props");
        var cutscene = FindIn<PlayableDirector>(scene, "Cutscene_AfterStore");
        if (corpses == null || cutscene == null) { Debug.LogError("[ExitCorpses] Corpses / Cutscene_AfterStore not found"); return; }

        System.IO.Directory.CreateDirectory(MeshDir);
        System.IO.Directory.CreateDirectory("Assets/Materials/DeadMonsters");
        AssetDatabase.Refresh();

        // ---- meshes
        var meshes = new Mesh[Variants.Length]; var mats = new Material[Variants.Length][];
        for (int i = 0; i < Variants.Length; i++) meshes[i] = SaveMesh(Variants[i], out mats[i]);
        var poolMats = new[] { BloodMat("DM_BloodPoolA", "Assets/Textures/DeadMonsters/blood_pool_a.png"), BloodMat("DM_BloodPoolB", "Assets/Textures/DeadMonsters/blood_pool_b.png") };
        var quad = PoolQuad();
        AssetDatabase.SaveAssets();

        // ---- the old props
        var old = new List<Transform>();
        Transform group = null;
        foreach (Transform c in corpses) { if (c.name == GroupName) group = c; else old.Add(c); }
        if (group != null) Object.DestroyImmediate(group.gameObject);
        var groupGo = new GameObject(GroupName);
        groupGo.transform.SetParent(corpses, false);
        group = groupGo.transform;

        pavingTris = null;
        var rnd = new System.Random(417);
        var hidden = new List<GameObject>();
        bool wasActive = corpses.gameObject.activeSelf;

        // the old props: hidden; they tell where the alley is
        float oldFloor = float.MaxValue, alleyX = 0f;
        foreach (var o in old)
        {
            Bounds b = new Bounds(o.position, Vector3.zero); bool first = true;
            foreach (var r in o.GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            oldFloor = Mathf.Min(oldFloor, b.min.y); alleyX += b.center.x;
            o.gameObject.SetActive(false);
            hidden.Add(o.gameObject);
        }
        alleyX /= Mathf.Max(1, old.Count);
        int layer = old.Count > 0 ? old[0].gameObject.layer : 0;
        var report = new System.Text.StringBuilder();

        // ---- the line: every body flat on the ground, one after the other down the alley (nothing lifted, nothing through a
        // neighbour). Close together near the back door, thinning out further on.
        float zCursor = LineNorth;
        for (int n = 0; n < LineCount; n++)
        {
            int vi = Layout[n % Layout.Length];
            Alley(scene, corpses, alleyX, zCursor - 1f, oldFloor, out float ground, out float wallL, out float wallR);
            Fit(meshes[vi], wallR - wallL - 0.1f, rnd, false, out float yaw, out float minX, out float maxX, out float minZ, out float maxZ);
            float z = zCursor - maxZ;                                   // its north end at the cursor
            if (z + minZ < LineSouth) break;
            float slack = Mathf.Max(0f, wallR - wallL - 0.1f - (maxX - minX));
            float x = wallL + 0.05f + (float)rnd.NextDouble() * slack - minX;
            var go = Body(group, layer, Variants[vi], n + 1, meshes[vi], mats[vi], new Vector3(x, ground + 0.004f, z), Quaternion.Euler(0f, yaw, 0f));
            // a low bump to walk over, like the old props had: it never blocks the alley
            var mb = meshes[vi].bounds;
            var bump = go.AddComponent<SphereCollider>();
            bump.radius = 0.9f; bump.center = new Vector3(mb.center.x, -0.78f, mb.center.z);   // 12 cm high, 30 degree edge
            if (rnd.NextDouble() < 0.75) Pool(go.transform, quad, poolMats[rnd.Next(2)], new Vector3(mb.center.x + (float)(rnd.NextDouble() - 0.5) * 0.3f, 0.012f, mb.center.z + (float)(rnd.NextDouble() - 0.5) * 0.5f), 1.0f + (float)rnd.NextDouble() * 0.55f, rnd);
            report.AppendLine(go.name + " " + Variants[vi].name + " z=" + z.ToString("F2") + " (" + (z + minZ).ToString("F2") + ".." + (z + maxZ).ToString("F2") + ") ground " + ground.ToString("F3") + " walls " + wallL.ToString("F2") + ".." + wallR.ToString("F2") + " width=" + (maxX - minX).ToString("F2") + " yaw=" + yaw);
            float gap = n < 6 ? 0.06f + (float)rnd.NextDouble() * 0.4f : 0.3f + (float)rnd.NextDouble() * 1.5f;
            zCursor = z + minZ - gap;
        }

        // ---- behind Khalid the alley is closed by a collapsed wall (the same rubble pile as the park-toilet alley, turned to
        // lie across this one). No bodies here: a heap of them looked wrong.
        {
            Alley(scene, corpses, alleyX, BlockadeZ, oldFloor, out float ground, out float wallL, out float wallR);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RubblePrefab);
            if (prefab == null) Debug.LogError("[ExitCorpses] missing " + RubblePrefab);
            else
            {
                var rubble = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                rubble.name = "Alley Blockade (rubble)";
                rubble.transform.SetParent(group, true);
                // the pile was made for a 1.97 m alley running along x: turned a quarter, mirrored (so it is not the same view
                // as the other one) and stretched to reach both walls here
                float across = (wallR - wallL) / 1.97f;
                rubble.transform.SetPositionAndRotation(new Vector3((wallL + wallR) * 0.5f, ground - 0.02f, BlockadeZ), Quaternion.Euler(0f, -90f, 0f));
                rubble.transform.localScale = new Vector3(1.08f, 1.1f, across * 1.03f);
                foreach (var t in rubble.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                report.AppendLine("blockade at z " + BlockadeZ + " ground " + ground.ToString("F3") + " walls " + wallL.ToString("F2") + ".." + wallR.ToString("F2"));
            }
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Screenshots/StoreRemake/dead_report.txt"), report.ToString());

        var rec = groupGo.AddComponent<SceneSwapRecord>();
        rec.note = "Dead Questioners + Runners (store redesign, beat 7) replace the old human corpse props. Built by Tools > Cutscenes > Build Exit Corpses.";
        rec.hiddenOriginals = hidden.ToArray();
        corpses.gameObject.SetActive(wasActive);

        // ---- the line
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        var entry = col.SharedData.GetEntry(LineEn) ?? col.SharedData.AddKey(LineEn);
        foreach (var table in col.StringTables)
        {
            string text = table.LocaleIdentifier.Code.StartsWith("ar") ? ArabicSupport.ArabicFixer.Fix(LineAr, false, false) : LineEn;
            var e = table.GetEntry(entry.Id);
            if (e == null) table.AddEntry(entry.Id, text); else e.Value = text;
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(col.SharedData);

        var sub1 = cutscene.transform.Find("Subtitle1").GetComponent<SubtitleTrigger>();
        var so = new SerializedObject(sub1);
        var refProp = so.FindProperty("subtitleRef");
        var line = new LocalizedString();
        line.TableReference = col.SharedData.TableCollectionNameGuid;
        line.TableEntryReference = entry.Id;
        refProp.boxedValue = line;
        so.FindProperty("lifeTime").floatValue = 3.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // the second line ("What is that in front of me?!") is gone: its signal no longer does anything
        var receiver = cutscene.GetComponent<SignalReceiver>();
        var timeline = cutscene.playableAsset as TimelineAsset;
        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var m in track.GetMarkers())
            {
                var se = m as SignalEmitter;
                if (se == null || se.asset == null || se.asset.name != "TriggerSubtitle1") continue;
                bool registered = false;
                foreach (var sg in receiver.GetRegisteredSignals()) if (sg == se.asset) registered = true;
                if (registered) { receiver.Remove(se.asset); EditorUtility.SetDirty(receiver); }
            }
            // the old spoken line does not match any more (lines are subtitles only now): silent, clip kept
            if (track is AudioTrack)
                foreach (var clip in track.GetClips())
                    if (clip.displayName == OldVoiceClip && clip.asset != null)
                    {
                        var cso = new SerializedObject(clip.asset);
                        var vol = cso.FindProperty("m_ClipProperties.volume");
                        if (vol != null) { vol.floatValue = 0f; cso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(clip.asset); }
                        else Debug.LogWarning("[ExitCorpses] could not silence '" + OldVoiceClip + "' (no clip volume property)");
                    }
        }
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[ExitCorpses] " + (group.childCount - 2) + " bodies placed, line -> '" + LineEn + "'");
    }
}
