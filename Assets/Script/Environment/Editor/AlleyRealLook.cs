using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Alley 1 re-skinned after the user's photos of the real alleys (2026-10-25): sandy ground, weathered tan / cream
// stucco walls with pilasters and plaster peeling off at the foot, bushes spilling over the wall tops. Looks only - no
// object is moved and nothing new has a collider, so gameplay boundaries, enemies and triggers are untouched.
//   Apply: PS2-style texture import (256 px, point filter), materials, the wall materials swapped on Alley 1's wall
//   meshes in five house styles (world-mapped AlleyWorldWall shader), a sand terrain layer painted over the Alley 1 rectangle (the original
//   weights of that patch are backed up first), bushes on the wall tops (AlleyBushes.fbx, FoliageCutout shader).
//   Revert: the old materials back, the bushes gone, the terrain patch restored and the sand layer removed.
//   Plain pilasters as geometry (they were painted into the wall texture) on the alley side: over every joint / end of
//   the wall pieces, instead of the old gate pillar props, and on the texture's 3.2 m panel seams in between.
//   Wall taps after the user's photo (pipe out of the wall, red-wheel valve, hose tap, black hose along the wall foot)
//   on the alley side of the house walls, one or two per house; the old Tripo pipe props are hidden.
// Textures: AnimationTools/alley_real/make_alley_textures.py (+ make_pipe_texture.py), bushes: build_bushes.py,
// taps: build_wall_taps.py (both Blender, same folder).
public static class AlleyRealLook
{
    const string Dir = "Assets/Textures/AlleyReal";
    const string BushFbx = "Assets/Models/AlleyBushes/AlleyBushes.fbx";
    const string Backup = "AnimationTools/alley_real/backup/terrain_patch.bin";
    const string WallsPath = "Alley 1 Props/New Walls";
    const string BushGroup = "Alley Bushes (real look)";
    const string TapFbx = "Assets/Models/AlleyTaps/AlleyTaps.fbx";
    const string TapGroup = "Alley Taps (real look)";
    const string PillarGroup = "Alley Wall Pillars (real look)";
    const string PillarMeshDir = "Assets/Models/AlleyPillars";
    // the old Tripo pipe props in Alley 1, hidden by Apply (the wall taps replace them) and shown again by Revert
    static readonly string[] OldPipes = { "Pipe", "Differentkindofpipes", "WaterHose" };
    const string OldWall2 = "Assets/Prefab/Props/Laundry Sheets/Material 1.mat";      // on Wall_Mesh 2
    const string OldWall1 = "Assets/RoadAsset/Textures/Rommel/Material 2.mat";         // on Wall_mesh 1
    const string OldPillar = "Assets/RoadAsset/Textures/Rommel/Material.015.mat";
    // the patch of ground painted: Alley 1's walls with a margin
    static readonly Rect Area = Rect.MinMaxRect(-216f, -54f, -143f, 14f);
    const float Edge = 2f;      // metres the sand fades in over at the patch border
    const float GroundY = 1.45f;

    [MenuItem("Tools/Environment/Alley 1 Real Look/Apply")]
    public static void Apply()
    {
        var env = GameObject.Find("------------Environment----------");
        var walls = env != null ? env.transform.Find(WallsPath) : null;
        if (walls == null) { Debug.LogError("[AlleyRealLook] " + WallsPath + " not found - open the Game scene."); return; }
        ImportSettings();
        var styles = Enumerable.Range(1, Styles).Select(i => WallMat("AlleyWall_V" + i)).ToArray();
        int swapped = AssignStyles(walls, styles);
        var wallRs = walls.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in Pillars())   // a pillar takes its nearest wall's style
        {
            var nearest = wallRs.OrderBy(w => (w.bounds.ClosestPoint(r.bounds.center) - r.bounds.center).sqrMagnitude).First();
            Undo.RecordObject(r, "Alley real look"); r.sharedMaterial = nearest.sharedMaterial; swapped++;
        }

        PaintSand();
        PlaceBushes(walls);
        PlacePillars(walls);
        PlaceTaps(walls);
        EditorSceneManager.MarkSceneDirty(walls.gameObject.scene);
        Debug.Log("[AlleyRealLook] applied: " + swapped + " wall renderers re-skinned, sand painted, bushes and taps placed");
    }

    [MenuItem("Tools/Environment/Alley 1 Real Look/Revert")]
    public static void Revert()
    {
        var env = GameObject.Find("------------Environment----------");
        var walls = env != null ? env.transform.Find(WallsPath) : null;
        if (walls == null) { Debug.LogError("[AlleyRealLook] " + WallsPath + " not found - open the Game scene."); return; }
        var m1 = AssetDatabase.LoadAssetAtPath<Material>(OldWall1); var m2 = AssetDatabase.LoadAssetAtPath<Material>(OldWall2);
        foreach (var mf in walls.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<MeshRenderer>(); if (r == null || mf.sharedMesh == null) continue;
            Undo.RecordObject(r, "Alley real look revert");
            r.sharedMaterial = mf.sharedMesh.name.StartsWith("Wall_mesh 1") ? m1 : m2;
        }
        var pm = AssetDatabase.LoadAssetAtPath<Material>(OldPillar);
        foreach (var r in Pillars()) { Undo.RecordObject(r, "Alley real look revert"); r.sharedMaterial = pm; }
        foreach (var r in GatePillarProps().SelectMany(g => g)) { Undo.RecordObject(r, "Alley real look revert"); r.enabled = true; }
        var group = walls.parent.Find(BushGroup);
        if (group != null) Undo.DestroyObjectImmediate(group.gameObject);
        var taps = walls.parent.Find(TapGroup);
        if (taps != null) Undo.DestroyObjectImmediate(taps.gameObject);
        var pillars = walls.parent.Find(PillarGroup);
        if (pillars != null) Undo.DestroyObjectImmediate(pillars.gameObject);
        ShowOldPipes(env.transform, true);
        RestoreTerrain();
        EditorSceneManager.MarkSceneDirty(walls.gameObject.scene);
        Debug.Log("[AlleyRealLook] reverted");
    }

    // ── wall styles: one per house block ──
    // (regions reaching the edge of the area are streets, not yards)
    // Five styles (AlleyWall_V1..V5: peach crazed + stained, ochre with rotting pilaster feet, cream, tan, off-white).
    // A house block = the yard its walls enclose. The ground plan is rasterised (0.5 m cells blocked by any collider at
    // wall height - walls, gates, doors, houses) and flood-filled: the alleys come out as one big region, every yard as
    // its own. Each wall piece belongs to the yard on its inner side (sampled just off both faces along it), so all the
    // walls round one yard share a style - even across doorways and the small gaps between pieces, which defeated
    // grouping by touching. Neighbouring blocks get different styles where possible.
    const int Styles = 5;
    const float Cell = 0.5f;
    public static string LastReport = "";
    static readonly System.Collections.Generic.Dictionary<MeshRenderer, int> pieceBlock = new System.Collections.Generic.Dictionary<MeshRenderer, int>();
    static int[,] regionLabel;                                       // the flood-filled ground plan (-2 = solid)
    static System.Collections.Generic.HashSet<int> alleyRegions;     // its regions that are alleys / streets

    static int regionAt(Vector3 p)
    {
        int i = Mathf.FloorToInt((p.x - Area.xMin) / Cell), j = Mathf.FloorToInt((p.z - Area.yMin) / Cell);
        return regionLabel == null || i < 0 || j < 0 || i >= regionLabel.GetLength(0) || j >= regionLabel.GetLength(1) ? -1 : regionLabel[i, j];
    }

    // out in the alley where the player walks (not inside a yard)
    static bool InAlley(Vector3 p) { int r = regionAt(p); return r >= 0 && alleyRegions.Contains(r); }

    static int AssignStyles(Transform walls, Material[] styles)
    {
        int W = Mathf.CeilToInt(Area.width / Cell), H = Mathf.CeilToInt(Area.height / Cell);
        var label = new int[W, H];
        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                label[i, j] = Physics.CheckBox(new Vector3(Area.xMin + (i + 0.5f) * Cell, GroundY + 1.2f, Area.yMin + (j + 0.5f) * Cell),
                    new Vector3(Cell * 0.8f, 0.9f, Cell * 0.8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) ? -2 : -1;   // fat cells: seal gaps < ~0.6 m between pieces
        var sizes = new System.Collections.Generic.List<int>();
        var outside = new System.Collections.Generic.HashSet<int>();     // open to the edge of the area: alleys / streets, not yards
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
            {
                if (label[i, j] != -1) continue;
                int id = sizes.Count, size = 0;
                var q = new System.Collections.Generic.Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(i, j)); label[i, j] = id;
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); size++;
                    if (c.x == 0 || c.y == 0 || c.x == W - 1 || c.y == H - 1) outside.Add(id);
                    foreach (var d in dirs)
                    {
                        var o = c + d;
                        if (o.x < 0 || o.y < 0 || o.x >= W || o.y >= H || label[o.x, o.y] != -1) continue;
                        label[o.x, o.y] = id; q.Enqueue(o);
                    }
                }
                sizes.Add(size);
            }
        outside.Add(Enumerable.Range(0, sizes.Count).OrderByDescending(k => sizes[k]).First());
        regionLabel = label; alleyRegions = outside;

        // each piece votes for the non-alley region just off its faces (the bigger one if both sides are yards)
        var rs = walls.GetComponentsInChildren<MeshRenderer>(true);
        var block = new int[rs.Length];
        for (int k = 0; k < rs.Length; k++)
        {
            var b = rs[k].bounds; bool alongX = b.size.x >= b.size.z;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, side = alongX ? Vector3.forward : Vector3.right;
            float half = (alongX ? b.size.z : b.size.x) * 0.5f + 0.75f, len = alongX ? b.size.x : b.size.z;
            var votes = new System.Collections.Generic.Dictionary<int, int>();
            foreach (float f in new[] { -0.35f, 0f, 0.35f })
                foreach (float sgn in new[] { 1f, -1f })
                {
                    int r = regionAt(b.center + along * f * len + side * sgn * half);
                    if (r < 0 || outside.Contains(r)) continue;
                    votes.TryGetValue(r, out int v); votes[r] = v + 1;
                }
            block[k] = votes.Count == 0 ? -1 : votes.OrderByDescending(v => v.Value).ThenByDescending(v => sizes[v.Key]).First().Key;
        }
        // pieces that found no yard (free-standing bits) join their nearest piece's block
        for (int k = 0; k < rs.Length; k++)
            if (block[k] < 0)
            {
                var c = rs[k].bounds.center; int best = -1; float bd = float.MaxValue;
                for (int o = 0; o < rs.Length; o++)
                    if (block[o] >= 0) { float d = (rs[o].bounds.ClosestPoint(c) - c).sqrMagnitude; if (d < bd) { bd = d; best = o; } }
                block[k] = best >= 0 ? block[best] : 0;
            }

        // two yards that leak into each other (a gap the cells did not seal) come out as one block: split a block where
        // its walls fall apart with an alley-wide gap between them, across x or across z
        int nextId = block.Max() + 1;
        foreach (var id in block.Distinct().ToList())
            foreach (bool alongX in new[] { false, true })
            {
                var pieces = Enumerable.Range(0, rs.Length).Where(k => block[k] == id)
                    .OrderBy(k => alongX ? rs[k].bounds.min.x : rs[k].bounds.min.z).ToList();
                float reach = float.MinValue; bool split = false;
                foreach (var k in pieces)
                {
                    var bb = rs[k].bounds; float lo = alongX ? bb.min.x : bb.min.z, hi = alongX ? bb.max.x : bb.max.z;
                    if (reach > float.MinValue && lo - reach > 2.5f) split = true;
                    if (split) block[k] = nextId;
                    reach = Mathf.Max(reach, hi);
                }
                if (split) { nextId++; break; }
            }

        // one style per block, different from the blocks near it where possible
        var ids = block.Distinct().ToList();
        var bounds = ids.ToDictionary(id => id, id =>
        {
            var pieces = Enumerable.Range(0, rs.Length).Where(k => block[k] == id).ToArray();
            var bb = rs[pieces[0]].bounds; foreach (var k in pieces) bb.Encapsulate(rs[k].bounds); return bb;
        });
        var rnd = new System.Random(9);
        var style = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var id in ids.OrderByDescending(id => bounds[id].size.x * bounds[id].size.z))
        {
            var near = bounds[id]; near.Expand(24f);   // (Expand grows the size: 12 m each way)
            var taken = style.Where(s => near.Intersects(bounds[s.Key])).Select(s => s.Value).ToHashSet();
            // not used by a neighbour, and of those the least used so far (so every style shows up)
            var free = Enumerable.Range(0, styles.Length).Where(st => !taken.Contains(st)).ToList();
            if (free.Count == 0) free = Enumerable.Range(0, styles.Length).ToList();
            int least = free.Min(st => style.Values.Count(v => v == st));
            free = free.Where(st => style.Values.Count(v => v == st) == least).ToList();
            style[id] = free[rnd.Next(free.Count)];
        }
        pieceBlock.Clear();
        for (int k = 0; k < rs.Length; k++)
        {
            Undo.RecordObject(rs[k], "Alley real look");
            rs[k].sharedMaterial = styles[style[block[k]]];
            pieceBlock[rs[k]] = block[k];
        }
        LastReport = string.Join("\n", ids.Select(id => "block " + id + " (" + block.Count(b => b == id) + " pieces, x " + bounds[id].min.x.ToString("F0") + ".." + bounds[id].max.x.ToString("F0") + " z " + bounds[id].min.z.ToString("F0") + ".." + bounds[id].max.z.ToString("F0") + ") -> V" + (style[id] + 1)));
        Debug.Log("[AlleyRealLook] " + ids.Count + " house blocks styled\n" + LastReport);
        return rs.Length;
    }

    // ── textures / materials ──

    static void ImportSettings()
    {
        foreach (var name in Enumerable.Range(1, Styles).Select(i => "AlleyWall_V" + i).Concat(new[] { "AlleySand", "AlleyLeaves", "AlleyBougainvillea", "AlleyPipes" }))
        {
            var path = Dir + "/" + name + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { Debug.LogError("[AlleyRealLook] missing " + path + " (run make_alley_textures.py)"); continue; }
            bool leaves = name.Contains("Leaves") || name.Contains("Bougain");
            ti.maxTextureSize = name.StartsWith("AlleyWall") ? 512 : 256;   // walls: two panels wide, same 256 px per 3.2 m
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = leaves ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.alphaIsTransparency = leaves;
            ti.alphaSource = leaves ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.mipmapEnabled = true;
            ti.mipMapsPreserveCoverage = leaves;
            ti.alphaTestReferenceValue = 0.5f;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    static Material WallMat(string name)
    {
        var m = LoadOrCreate(name, "Custom/AlleyWorldWall");
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + name + ".png");
        m.SetFloat("_BaseY", GroundY);
        m.SetFloat("_PanelWidth", 6.4f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material BushMat(string name, string tex, Color tint)
    {
        var m = LoadOrCreate(name, "Custom/FoliageCutout");
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + ".png");
        m.color = tint;
        m.SetFloat("_Cutoff", 0.5f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrCreate(string name, string shader)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        else m.shader = Shader.Find(shader);
        return m;
    }

    static MeshRenderer[] Pillars()
    {
        var env = GameObject.Find("------------Environment----------");
        var props = env.transform.Find("Scattered Props");
        if (props == null) return new MeshRenderer[0];
        return props.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.transform.parent != null && r.transform.parent.name.StartsWith("Pillar_3") && Area.Contains(new Vector2(r.bounds.center.x, r.bounds.center.z)))
            .ToArray();
    }

    // ── terrain ──

    static Terrain AlleyTerrain()
    {
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            var p = t.transform.position; var s = t.terrainData.size;
            if (Area.center.x > p.x && Area.center.x < p.x + s.x && Area.center.y > p.z && Area.center.y < p.z + s.z && t.SampleHeight(new Vector3(Area.center.x, 0, Area.center.y)) + p.y < GroundY + 0.5f)
                return t;
        }
        return null;
    }

    static void PatchRect(Terrain t, out int x0, out int y0, out int w, out int h)
    {
        var td = t.terrainData; int res = td.alphamapResolution; var p = t.transform.position;
        x0 = Mathf.Clamp(Mathf.FloorToInt((Area.xMin - p.x) / td.size.x * (res - 1)), 0, res - 1);
        y0 = Mathf.Clamp(Mathf.FloorToInt((Area.yMin - p.z) / td.size.z * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((Area.xMax - p.x) / td.size.x * (res - 1)), 0, res - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt((Area.yMax - p.z) / td.size.z * (res - 1)), 0, res - 1);
        w = x1 - x0 + 1; h = y1 - y0 + 1;
    }

    static void PaintSand()
    {
        var t = AlleyTerrain(); if (t == null) { Debug.LogError("[AlleyRealLook] no terrain under Alley 1"); return; }
        var td = t.terrainData;
        string layerPath = Dir + "/AlleySand.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/AlleySand.png");
        layer.tileSize = new Vector2(4f, 4f);
        layer.smoothness = 0f; layer.metallic = 0f;
        EditorUtility.SetDirty(layer);

        PatchRect(t, out int x0, out int y0, out int w, out int h);
        var layers = td.terrainLayers.ToList();
        int idx = layers.IndexOf(layer);
        if (idx < 0)
        {
            // back up the patch as it is now (before the layer exists), for Revert
            var orig = td.GetAlphamaps(x0, y0, w, h);
            Directory.CreateDirectory(Path.GetDirectoryName(Backup));
            using (var bw = new BinaryWriter(File.Create(Backup)))
            {
                bw.Write(x0); bw.Write(y0); bw.Write(w); bw.Write(h); bw.Write(td.alphamapLayers);
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int l = 0; l < td.alphamapLayers; l++) bw.Write(orig[y, x, l]);
            }
            Undo.RegisterCompleteObjectUndo(td, "Alley sand");
            layers.Add(layer); td.terrainLayers = layers.ToArray(); idx = layers.Count - 1;
        }
        var maps = td.GetAlphamaps(x0, y0, w, h);
        int res = td.alphamapResolution; var p = t.transform.position;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float wx = p.x + (x0 + x) / (float)(res - 1) * td.size.x, wz = p.z + (y0 + y) / (float)(res - 1) * td.size.z;
                float d = Mathf.Min(wx - Area.xMin, Area.xMax - wx, wz - Area.yMin, Area.yMax - wz);
                float s = Mathf.Clamp01(d / Edge);
                for (int l = 0; l < td.alphamapLayers; l++) maps[y, x, l] *= 1f - s;
                maps[y, x, idx] += s;
            }
        td.SetAlphamaps(x0, y0, maps);
        EditorUtility.SetDirty(td);
    }

    static void RestoreTerrain()
    {
        var t = AlleyTerrain(); if (t == null || !File.Exists(Backup)) return;
        var td = t.terrainData;
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Dir + "/AlleySand.terrainlayer");
        using (var br = new BinaryReader(File.OpenRead(Backup)))
        {
            int x0 = br.ReadInt32(), y0 = br.ReadInt32(), w = br.ReadInt32(), h = br.ReadInt32(), n = br.ReadInt32();
            var orig = new float[h, w, n];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int l = 0; l < n; l++) orig[y, x, l] = br.ReadSingle();
            Undo.RegisterCompleteObjectUndo(td, "Alley sand revert");
            if (layer != null) td.terrainLayers = td.terrainLayers.Where(l => l != layer).ToArray();
            if (td.alphamapLayers == n) td.SetAlphamaps(x0, y0, orig);
            else Debug.LogError("[AlleyRealLook] terrain layer count changed since the backup - patch not restored");
        }
        EditorUtility.SetDirty(td);
    }

    // ── bushes ──

    static void PlaceBushes(Transform walls)
    {
        var old = walls.parent.Find(BushGroup);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject(BushGroup).transform;
        group.SetParent(walls.parent, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Alley bushes");

        var green = BushMat("AlleyBush_Green", "AlleyLeaves", Color.white);
        var bougain = BushMat("AlleyBush_Bougainvillea", "AlleyBougainvillea", Color.white);
        var meshes = AssetDatabase.LoadAllAssetsAtPath(BushFbx).OfType<Mesh>().OrderBy(m => m.name).ToArray();
        if (meshes.Length == 0) { Debug.LogError("[AlleyRealLook] no meshes in " + BushFbx + " (run build_bushes.py in Blender)"); return; }

        var rnd = new System.Random(5);
        int placed = 0;
        foreach (var r in walls.GetComponentsInChildren<MeshRenderer>(true).OrderBy(x => x.name))
        {
            var b = r.bounds;
            bool alongX = b.size.x > b.size.z;
            float length = alongX ? b.size.x : b.size.z;
            if (length < 2.5f || rnd.NextDouble() > 0.38) continue;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 side = alongX ? Vector3.forward : Vector3.right;
            // the alley side: walkable ground (NavMesh) just off that face of the wall
            float thick = (alongX ? b.size.z : b.size.x) * 0.5f;
            Vector3 mid = new Vector3(b.center.x, GroundY, b.center.z);
            bool plus = Walkable(mid + side * (thick + 1.2f)), minus = Walkable(mid - side * (thick + 1.2f));
            if (!plus && !minus) continue;
            Vector3 out_ = plus && (!minus || rnd.NextDouble() < 0.5) ? side : -side;
            float offset = (float)(rnd.NextDouble() - 0.5) * Mathf.Max(0f, length - 2f);
            Vector3 pos = new Vector3(b.center.x, b.max.y, b.center.z) + along * offset + out_ * thick;

            var go = new GameObject("WallBush " + placed);
            go.transform.SetParent(group, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(out_, Vector3.up);
            float s = 1.05f + (float)rnd.NextDouble() * 0.35f;
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<MeshFilter>().sharedMesh = meshes[rnd.Next(meshes.Length)];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rnd.NextDouble() < 0.35 ? bougain : green;
            placed++;
        }
        Debug.Log("[AlleyRealLook] " + placed + " bushes on the walls");
    }

    // ── pilasters ──
    // A boundary wall here is panels between plain pilasters (the photos), and the pilasters stand where panels meet.
    // Three kinds, all the same simple box (the wall's plaster via its "_Pillar" material, a dark seam, shaded sides):
    //  - joints and ends: wherever an alley-side face ends - the next piece steps out or in, is higher or lower, a
    //    corner, or the wall just stops - one pillar covers the joint, deep and tall enough to hide the step (the
    //    original pieces overlap with small offsets, and their bare ends read as odd slabs);
    //  - gate pillars: the old pillar props (Pillar_3, Pillar_2, Pillars_5, pillar-sized wall pieces outside New Walls)
    //    are drawn as plain pillars too (renderers off, colliders kept);
    //  - seams: in between, on each 3.2 m seam of the texture (along = 3.2 k - shift, shift = frac(perp * 0.731) * 6.4,
    //    where the rot at the pillar feet is centred) where the wall runs on unbroken and level for 60 cm each side and
    //    no other pillar is within 1.2 m.
    // One mesh per style, no colliders.

    const float PillarW = 0.36f, PillarOut = 0.12f, PillarCap = 0.05f, JointW = 0.44f;
    static readonly System.Collections.Generic.List<Bounds> pillarBounds = new System.Collections.Generic.List<Bounds>();
    public static int LastPillarCount = -1;
    public static string LastPillarReport = "";

    class PillarPart
    {
        public readonly System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>(), n = new System.Collections.Generic.List<Vector3>();
        public readonly System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>();
        public readonly System.Collections.Generic.List<int> t = new System.Collections.Generic.List<int>();
    }

    static void PlacePillars(Transform walls)
    {
        pillarBounds.Clear();
        var old = walls.parent.Find(PillarGroup);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var wallCols = new System.Collections.Generic.HashSet<Collider>(walls.GetComponentsInChildren<Collider>(true));
        var wallRs = walls.GetComponentsInChildren<MeshRenderer>(true).OrderBy(x => x.name).ToArray();
        var parts = new System.Collections.Generic.Dictionary<Material, PillarPart>();
        var placed = new System.Collections.Generic.List<Vector3>();     // pillar centres, y = 0
        int gates = 0, joints = 0, seams = 0;

        // a box: `at` anywhere on its centre line, back / front measured along out_
        void Add(Material wallMat, float shift, Vector3 at, Vector3 along, Vector3 out_, float width, float back, float front, float bottom, float top)
        {
            var mat = PillarMat(wallMat);
            if (!parts.TryGetValue(mat, out var part)) parts[mat] = part = new PillarPart();
            var c = at - out_ * Vector3.Dot(at, out_) + out_ * (back + front) / 2f;
            c.y = (bottom + top) / 2f;
            PillarBox(part, c, new Vector3(width / 2f, (top - bottom) / 2f, (front - back) / 2f), along, out_, shift);
            var size = Abs(along * width + out_ * (front - back));
            pillarBounds.Add(new Bounds(c, new Vector3(size.x, top - bottom, size.z)));
            placed.Add(new Vector3(c.x, 0f, c.z));
        }
        bool Near(Vector3 p, float d) => placed.Any(q => (q - new Vector3(p.x, 0f, p.z)).sqrMagnitude < d * d);
        // the wall top just inside a face point
        bool TopAt(Vector3 f, Vector3 out_, out float y)
        {
            y = 0f;
            if (!Physics.Raycast(f - out_ * 0.04f + Vector3.up * 8f, Vector3.down, out RaycastHit h, 10f, ~0, QueryTriggerInteraction.Ignore) || !wallCols.Contains(h.collider)) return false;
            y = h.point.y; return true;
        }
        // something tall that is not wall (gate pillar, door, gate, pole) standing by a face point
        bool Crowded(Vector3 f, Vector3 along, Vector3 out_, float reach) =>
            Physics.OverlapBox(f + out_ * 0.4f + Vector3.up * 1.5f, Abs(along * reach + out_ * 0.6f + Vector3.up * 1.2f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Any(col => !wallCols.Contains(col) && !(col is TerrainCollider) && col.bounds.size.y > 2.0f);
        Material WallMatAt(Vector3 f, Vector3 out_)
        {
            if (!Physics.Raycast(f + out_ * 0.05f + Vector3.up * 0.5f, -out_, out RaycastHit h, 0.2f, ~0, QueryTriggerInteraction.Ignore)) return null;
            var r = h.collider.GetComponent<MeshRenderer>();
            return r != null ? r.sharedMaterial : null;
        }

        // 1. gate pillars: the old props drawn as plain pillars, a little taller than the wall beside them
        foreach (var grp in GatePillarProps())
        {
            var col = grp.Key.GetComponentInChildren<Collider>();
            var b = col != null ? col.bounds : grp.First().bounds;
            var nearest = wallRs.OrderBy(w => (w.bounds.ClosestPoint(b.center) - b.center).sqrMagnitude).First();
            bool alongX = b.size.x >= b.size.z;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, out_ = alongX ? Vector3.forward : Vector3.right;
            float width = alongX ? b.size.x : b.size.z, depth = alongX ? b.size.z : b.size.x;
            if (!Ground(b.center + out_ * (depth / 2f + 0.3f), out float gy)) gy = GroundY;
            float mid = Vector3.Dot(b.center, out_);
            Add(nearest.sharedMaterial, 0f, b.center, along, out_, Mathf.Max(width, PillarW), mid - depth / 2f, mid + depth / 2f, gy - 0.1f, nearest.bounds.max.y + 0.15f);
            foreach (var r in grp) { Undo.RecordObject(r, "Alley pillars"); r.enabled = false; }
            gates++;
        }

        // 2. joints and wall ends on the alley side
        foreach (var r in wallRs)
        {
            var b = r.bounds; bool alongX = b.size.x >= b.size.z;
            float yaw = r.transform.eulerAngles.y % 90f;
            if (yaw > 1f && yaw < 89f) continue;      // axis-aligned pieces only (one stray one is turned 66 degrees)
            float thick = (alongX ? b.size.z : b.size.x) * 0.5f;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, side = alongX ? Vector3.forward : Vector3.right;
            Vector3 mid = new Vector3(b.center.x, GroundY, b.center.z);
            foreach (var out_ in new[] { side, -side })
            {
                float stand = Vector3.Dot(b.center + out_ * thick, out_);      // how far out this face stands
                foreach (var (end, dir) in new[] { (Vector3.Dot(b.min, along), -1f), (Vector3.Dot(b.max, along), 1f) })
                {
                    Vector3 at = mid + along * (end - Vector3.Dot(mid, along));
                    // this face, just inside its end (if another piece covers the end, that piece's end counts instead)
                    if (!FaceAt(at - along * dir * 0.15f, out_, wallCols, out Vector3 fIn) || Mathf.Abs(Vector3.Dot(fIn, out_) - stand) > 0.01f) continue;
                    if (!InAlley(fIn + out_ * 0.6f) && !InAlley(fIn + out_ * 1.2f)) continue;
                    if (!TopAt(fIn, out_, out float topIn)) continue;
                    // and just past it: another face (maybe further out or back, higher or lower) or nothing / a corner
                    float topOut = topIn;
                    bool beyond = FaceAt(at + along * dir * 0.15f, out_, wallCols, out Vector3 fOut) && TopAt(fOut, out_, out topOut);
                    if (!beyond) topOut = topIn;
                    float standOut = beyond ? Vector3.Dot(fOut, out_) : stand;
                    if (beyond && Mathf.Abs(standOut - stand) < 0.01f && Mathf.Abs(topOut - topIn) < 0.05f) continue;   // the wall just runs on
                    if (Near(at, 0.9f) || Crowded(fIn, along, out_, 0.5f)) continue;
                    var wm = WallMatAt(fIn, out_); if (wm == null) continue;
                    if (!Ground(fIn + out_ * 0.1f, out float gy)) gy = GroundY;
                    // across a joint; flush with the end of a wall that just stops
                    Vector3 c = beyond ? at : at - along * dir * JointW / 2f;
                    Add(wm, Mathf.Repeat(Vector3.Dot(fIn, side) * 0.731f, 1f), c, along, out_, JointW,
                        Mathf.Min(stand, standOut) - 0.02f, Mathf.Max(stand, standOut) + PillarOut, gy - 0.1f, Mathf.Max(topIn, topOut) + PillarCap);
                    joints++;
                }
            }
        }

        // 3. seams along the unbroken stretches
        foreach (var r in wallRs)
        {
            var b = r.bounds; bool alongX = b.size.x >= b.size.z;
            float yaw = r.transform.eulerAngles.y % 90f;
            if (yaw > 1f && yaw < 89f) continue;
            float thick = (alongX ? b.size.z : b.size.x) * 0.5f;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, side = alongX ? Vector3.forward : Vector3.right;
            Vector3 mid = new Vector3(b.center.x, GroundY, b.center.z);
            foreach (var out_ in new[] { side, -side })
            {
                float plane = Vector3.Dot(b.center + out_ * thick, side);            // the face's perp coordinate
                float shift = Mathf.Repeat(plane * 0.731f, 1f);
                float a0 = Vector3.Dot(b.min, along), a1 = Vector3.Dot(b.max, along);
                for (float a = Mathf.Ceil((a0 + shift * 6.4f) / 3.2f) * 3.2f - shift * 6.4f; a <= a1; a += 3.2f)
                {
                    Vector3 basePos = mid + along * (a - Vector3.Dot(mid, along));
                    bool ok = true; Vector3 face = default; float topMin = float.MaxValue, topMax = float.MinValue;
                    foreach (float o in new[] { -0.6f, -PillarW / 2f, 0f, PillarW / 2f, 0.6f })
                    {
                        if (!FaceAt(basePos + along * o, out_, wallCols, out Vector3 f) || Mathf.Abs(Vector3.Dot(f, side) - plane) > 0.01f || !TopAt(f, out_, out float ty)) { ok = false; break; }
                        if (o == 0f) face = f;
                        topMin = Mathf.Min(topMin, ty); topMax = Mathf.Max(topMax, ty);
                    }
                    if (!ok || topMax - topMin > 0.08f || Near(face, 1.2f) || Crowded(face, along, out_, 1.0f)) continue;
                    if (!InAlley(face + out_ * 0.6f) && !InAlley(face + out_ * 1.2f)) continue;    // the alley side, not a yard
                    var wm = WallMatAt(face, out_); if (wm == null) continue;
                    if (!Ground(face + out_ * 0.1f, out float gy)) gy = GroundY;
                    float stand = Vector3.Dot(face, out_);
                    Add(wm, shift, face, along, out_, PillarW, stand - 0.02f, stand + PillarOut, gy - 0.1f, topMin + PillarCap);
                    seams++;
                }
            }
        }

        var group = new GameObject(PillarGroup).transform;
        group.SetParent(walls.parent, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Alley pillars");
        Directory.CreateDirectory(PillarMeshDir);
        foreach (var kv in parts)
        {
            // the boxes were built in world space; Alley 1 Props is not at the origin
            for (int i = 0; i < kv.Value.v.Count; i++) { kv.Value.v[i] = group.InverseTransformPoint(kv.Value.v[i]); kv.Value.n[i] = group.InverseTransformDirection(kv.Value.n[i]); }
            string path = PillarMeshDir + "/" + kv.Key.name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = kv.Key.name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(kv.Value.v); mesh.SetNormals(kv.Value.n); mesh.SetUVs(0, kv.Value.uv); mesh.SetTriangles(kv.Value.t, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var go = new GameObject(kv.Key.name);
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
        }
        AssetDatabase.SaveAssets();
        LastPillarCount = placed.Count;
        LastPillarReport = gates + " gate pillars, " + joints + " at joints / ends, " + seams + " on seams";
        Debug.Log("[AlleyRealLook] wall pillars: " + LastPillarReport);
    }

    // the old pillar props in Alley 1, each with its renderers: Pillar_3 (with its side panels), Pillar_2, Pillars_5,
    // and wall-mesh pieces cut to pillar size that sit outside New Walls (Scattered Props/Wall Mesh_2 (13..19) still in
    // the old white material, Alley 1 Props/Wall Mesh_3 (1) in the old pillar one)
    static System.Collections.Generic.IEnumerable<IGrouping<Transform, MeshRenderer>> GatePillarProps()
    {
        var env = GameObject.Find("------------Environment----------");
        if (env == null) return Enumerable.Empty<IGrouping<Transform, MeshRenderer>>();
        var props = env.transform.Find("Scattered Props");
        var alleyProps = env.transform.Find("Alley 1 Props");
        var newWalls = env.transform.Find(WallsPath);
        bool IsPillar(Transform t) => t != null && (t.name.StartsWith("Pillar_") || t.name.StartsWith("Pillars_"));
        bool PillarSizedWall(MeshRenderer r) => r.name.StartsWith("Wall Mesh") && !r.transform.IsChildOf(newWalls)
            && r.bounds.size.y > 2f && Mathf.Max(r.bounds.size.x, r.bounds.size.z) < 1f;
        var rs = new[] { props, alleyProps }.Where(t => t != null).SelectMany(t => t.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r => Area.Contains(new Vector2(r.bounds.center.x, r.bounds.center.z)) && (IsPillar(r.transform) || IsPillar(r.transform.parent) || PillarSizedWall(r)));
        return rs.GroupBy(r => IsPillar(r.transform) || !IsPillar(r.transform.parent) ? r.transform : r.transform.parent).ToList();
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // the wall style's twin for pillars: same texture, shift read from the mesh UVs
    static Material PillarMat(Material wall)
    {
        var m = LoadOrCreate(wall.name + "_Pillar", "Custom/AlleyWorldWall");
        m.CopyPropertiesFromMaterial(wall);
        m.SetFloat("_ShiftFromUV", 1f);
        m.color = wall.color * 1.05f;   // a slightly fresher coat than the panels, as on the photos
        EditorUtility.SetDirty(m);
        return m;
    }

    // world-space box (no bottom - it is under the sand), all its vertices carrying the wall face's shift in uv.x
    static void PillarBox(PillarPart p, Vector3 c, Vector3 half, Vector3 along, Vector3 out_, float shift)
    {
        Vector3 x = along * half.x, y = Vector3.up * half.y, z = out_ * half.z;
        // uv.y: the shader's seam / shade term - 0..1 across the front, low on the sides, 0.5 (none) on top and back
        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, float s0, float s1)
        {
            int i = p.v.Count;
            p.v.Add(p0); p.v.Add(p1); p.v.Add(p2); p.v.Add(p3);
            for (int k = 0; k < 4; k++) { p.n.Add(normal); p.uv.Add(new Vector2(shift, k == 0 || k == 3 ? s0 : s1)); }
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0f) p.t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else p.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        Quad(c - x - y + z, c + x - y + z, c + x + y + z, c - x + y + z, out_, 0f, 1f);          // front
        Quad(c + x - y + z, c + x - y - z, c + x + y - z, c + x + y + z, along, 0.04f, 0.04f);   // sides
        Quad(c - x - y - z, c - x - y + z, c - x + y + z, c - x + y - z, -along, 0.04f, 0.04f);
        Quad(c - x + y + z, c + x + y + z, c + x + y - z, c - x + y - z, Vector3.up, 0.5f, 0.5f);    // top
        Quad(c + x - y - z, c - x - y - z, c - x + y - z, c + x + y - z, -out_, 0.5f, 0.5f);         // back (shows over a lower wall)
    }


    // ── wall taps ──
    // Where a real one would be: on the alley side of a house wall (the house's supply comes out through it), facing the
    // next house across the alley (not the outer edge of the quarter, not inside a yard), at the foot
    // of an unbroken stretch of wall with level sand in front and nothing standing there. Tap A's hose runs along the wall
    // foot (only over that same wall face - never across a gap, doorway or pillar) and dives into the sand; tap B rises
    // out of the sand and has no hose. One tap per house block, two on the big ones, at least 8 m apart.

    public static int LastTapCount = -1;
    struct TapSpot { public Vector3 pos, out_, dir; public float hose; public int block; }

    static void PlaceTaps(Transform walls)
    {
        var old = walls.parent.Find(TapGroup);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var env = walls.parent.parent;
        ShowOldPipes(env, false);
        var meshes = AssetDatabase.LoadAllAssetsAtPath(TapFbx).OfType<Mesh>().ToDictionary(m => m.name);
        if (!meshes.ContainsKey("WallTap_A")) { Debug.LogError("[AlleyRealLook] no meshes in " + TapFbx + " (run build_wall_taps.py in Blender)"); return; }
        var mat = LoadOrCreate("AlleyPipes", "Legacy Shaders/Diffuse");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/AlleyPipes.png");
        EditorUtility.SetDirty(mat);

        var wallCols = new System.Collections.Generic.HashSet<Collider>(walls.GetComponentsInChildren<Collider>(true));
        var bushes = walls.parent.Find(BushGroup);
        // props without colliders that a tap must not stand in (papers, plants...): renderers in the area, not walls / bushes
        var props = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && Area.Contains(new Vector2(r.bounds.center.x, r.bounds.center.z))
                        && r.bounds.size.magnitude < 12f && !r.transform.IsChildOf(walls) && (bushes == null || !r.transform.IsChildOf(bushes)))
            .Select(r => r.bounds).Concat(pillarBounds).ToArray();

        var rnd = new System.Random(14);
        var spots = new System.Collections.Generic.List<TapSpot>();
        foreach (var r in walls.GetComponentsInChildren<MeshRenderer>(true).OrderBy(x => x.name))
        {
            var b = r.bounds; bool alongX = b.size.x >= b.size.z;
            float len = alongX ? b.size.x : b.size.z, thick = (alongX ? b.size.z : b.size.x) * 0.5f;
            if (len < 3f || b.max.y < GroundY + 1.5f) continue;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, side = alongX ? Vector3.forward : Vector3.right;
            foreach (var out_ in new[] { side, -side })
            {
                Vector3 mid = new Vector3(b.center.x, GroundY, b.center.z);
                if (!Walkable(mid + out_ * (thick + 1.2f))) continue;
                for (float s = -len / 2 + 0.7f; s <= len / 2 - 0.7f; s += 0.5f)
                {
                    Vector3 basePos = mid + along * s;
                    if (!FaceAt(basePos, out_, wallCols, out Vector3 face) || !Ground(face + out_ * 0.1f, out float gy)) continue;
                    face.y = gy;
                    if (!InAlley(face + out_ * 0.9f) || !InAlley(face + out_ * 1.6f) || !FacesWall(face, out_, wallCols) || !Clear(face, out_, along, 0.35f, props)) continue;
                    // the hose: as long as the same face stays flat and clear, in whichever direction is longer
                    float want = 2f + (float)rnd.NextDouble() * 3f, bestLen = 0f; Vector3 bestDir = along;
                    foreach (var dir in new[] { along, -along })
                    {
                        float l = 0f;
                        while (l < want)
                        {
                            Vector3 p = face + dir * (l + 0.5f);
                            if (!FaceAt(new Vector3(p.x, GroundY, p.z) - out_ * 0.01f, out_, wallCols, out Vector3 f2) || Mathf.Abs(Vector3.Dot(f2 - face, out_)) > 0.02f
                                || !Ground(f2 + out_ * 0.1f, out float g2) || Mathf.Abs(g2 - gy) > 0.05f || !Clear(f2, out_, along, 0.3f, props)) break;
                            l += 0.25f;
                        }
                        if (l > bestLen) { bestLen = l; bestDir = dir; }
                    }
                    spots.Add(new TapSpot { pos = face, out_ = out_, dir = bestDir, hose = bestLen >= 1.5f ? bestLen : 0f, block = pieceBlock.TryGetValue(r, out int bk) ? bk : -1 });
                }
            }
        }

        var group = new GameObject(TapGroup).transform;
        group.SetParent(walls.parent, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Alley taps");
        var chosen = new System.Collections.Generic.List<TapSpot>();
        foreach (var blk in spots.Select(t => t.block).Distinct().OrderBy(x => x))
        {
            int pieces = pieceBlock.Count(kv => kv.Value == blk);
            for (int n = 0; n < (pieces > 20 ? 2 : 1); n++)
            {
                var ok = spots.Where(t => t.block == blk && chosen.All(c => (c.pos - t.pos).sqrMagnitude > 64f)).ToList();
                if (ok.Count == 0) break;
                // prefer spots with room for the hose (it is what makes it read as the photo)
                var withHose = ok.Where(t => t.hose > 0f).ToList();
                var pool = withHose.Count > 0 && rnd.NextDouble() < 0.75 ? withHose : ok;
                chosen.Add(pool[rnd.Next(pool.Count)]);
            }
        }
        int i = 0;
        foreach (var t in chosen)
        {
            var root = new GameObject("WallTap " + i++).transform;
            root.SetParent(group, false);
            root.position = t.pos;
            root.rotation = Quaternion.LookRotation(t.out_, Vector3.up);
            // the meshes run their hose along local +X; mirror when it has to go the other way
            if (Vector3.Dot(root.right, t.dir) < 0f) root.localScale = new Vector3(-1f, 1f, 1f);
            bool hose = t.hose > 0f;
            Part(root, hose ? "WallTap_A" : "WallTap_B", Vector3.zero, Vector3.one, meshes, mat);
            if (!hose) continue;
            float run = t.hose - 0.4f - 0.36f;
            Part(root, "WallHose_Run", new Vector3(0.4f, 0f, 0f), new Vector3(run, 1f, 1f), meshes, mat);
            Part(root, "WallHose_End", new Vector3(0.4f + run, 0f, 0f), Vector3.one, meshes, mat);
            for (float x = 0.75f; x < 0.4f + run - 0.2f; x += 0.9f)
                Part(root, "WallHose_Clip", new Vector3(x, 0f, 0f), Vector3.one, meshes, mat);
        }
        LastTapCount = chosen.Count;
        Debug.Log("[AlleyRealLook] " + chosen.Count + " wall taps (" + chosen.Count(t => t.hose > 0f) + " with a hose) from " + spots.Count + " possible spots");
    }

    static void Part(Transform root, string mesh, Vector3 localPos, Vector3 localScale, System.Collections.Generic.Dictionary<string, Mesh> meshes, Material mat)
    {
        var go = new GameObject(mesh);
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.AddComponent<MeshFilter>().sharedMesh = meshes[mesh];
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // the visible wall face straight in from the alley at knee height (the nearest of overlapping pieces); fails if a
    // pillar or anything else is in the way, or the wall is missing there
    static bool FaceAt(Vector3 groundPoint, Vector3 out_, System.Collections.Generic.HashSet<Collider> wallCols, out Vector3 face)
    {
        face = default;
        Vector3 from = groundPoint + Vector3.up * 0.5f + out_ * 2f;
        if (!Physics.Raycast(from, -out_, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) || !wallCols.Contains(hit.collider)) return false;
        if (Vector3.Dot(hit.normal, out_) < 0.98f) return false;
        // and the wall still there just above the foot (no hole, no gap under it)
        if (!Physics.Raycast(from - Vector3.up * 0.42f, -out_, out RaycastHit low, 3f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(low.distance - hit.distance) > 0.02f) return false;
        face = new Vector3(hit.point.x, groundPoint.y, hit.point.z);
        return true;
    }

    // an alley between houses (another house wall across it), not the outer edge of the quarter
    static bool FacesWall(Vector3 face, Vector3 out_, System.Collections.Generic.HashSet<Collider> wallCols)
    {
        return Physics.Raycast(face + Vector3.up * 1.0f + out_ * 0.1f, out_, out RaycastHit hit, 9f, ~0, QueryTriggerInteraction.Ignore) && wallCols.Contains(hit.collider);
    }

    // sand under it: the terrain, not a step or a slab
    static bool Ground(Vector3 p, out float y)
    {
        y = GroundY;
        if (!Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore)) return false;
        y = hit.point.y;
        return hit.collider is TerrainCollider && Mathf.Abs(y - GroundY) < 0.3f;
    }

    // nothing standing in front of that bit of wall (colliders or loose props), up to 1.1 m high and `depth` out
    static bool Clear(Vector3 face, Vector3 out_, Vector3 along, float halfWidth, Bounds[] props)
    {
        var c = face + out_ * (0.04f + 0.3f) + Vector3.up * 0.6f;
        var half = along * halfWidth + out_ * 0.3f + Vector3.up * 0.5f;
        half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
        foreach (var col in Physics.OverlapBox(c, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (!(col is TerrainCollider)) return false;
        var zone = new Bounds(c, half * 2f + new Vector3(0.2f, 0f, 0.2f));
        return !props.Any(p => p.Intersects(zone));
    }

    static void ShowOldPipes(Transform env, bool show)
    {
        foreach (var name in OldPipes)
        {
            var t = env.Find(name);
            if (t == null || !Area.Contains(new Vector2(t.position.x, t.position.z))) continue;
            Undo.RecordObject(t.gameObject, "Alley taps");
            t.gameObject.SetActive(show);
        }
    }

    static bool Walkable(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) < 0.5f;
}
