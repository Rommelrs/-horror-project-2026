using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The shop fittings of the store makeover: gondola shelving, wall shelving, glass-door fridges, the open dairy chiller,
/// the chest freezer, the produce rack, floor stock - and every product on them.
///
/// Products come from Textures/StoreMakeover/Products/products.json (written by the packaging generator together with
/// products_atlas.png): each entry has a shape (pillow bag, sack, box, gable carton, or a lathe profile for cans /
/// bottles / jars / tubs), its real size and its tile in the atlas. All made-up brands.
/// Each fitting is a handful of combined meshes in world coordinates (one atlas material for all stock).
/// </summary>
public static partial class StoreFittings
{
    const string TexDir = "Assets/Textures/StoreMakeover/";
    const string ProductJson = "Assets/Textures/StoreMakeover/Products/products.json";
    public const float FloorY = 1.61f;

    public static Vector3 off;
    public static System.Func<string, string, Color, float, bool, bool, Material> Mat;   // name, texture, color, gloss, fade, cutout
    public static System.Func<Mesh, string, Mesh> SaveMesh;                              // stores a mesh as a sub-asset
    public static List<Vector3> keepClear = new List<Vector3>();                         // pickups: no product placed on top of them
    public static List<Bounds> fixtureBounds = new List<Bounds>();
    public static List<string> fixtureNames = new List<string>();
    public static int triCount;
    static System.Random rnd;

    static float R() => (float)rnd.NextDouble();
    static float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();

    // ------------------------------------------------------------------ products

    [System.Serializable]
    public class Product
    {
        public string id, cat, shape, stand;
        public float w, h, d, sq, u0, v0, u1, v1, tu0, tv0, tu1, tv1;
        public float[] profile;
        public bool hasTop;
    }

    [System.Serializable]
    class ProductList { public Product[] products; }

    static Dictionary<string, Product> byId;
    static Dictionary<string, List<Product>> byCat;
    static readonly Dictionary<string, LM> meshCache = new Dictionary<string, LM>();

    static void LoadProducts()
    {
        byId = new Dictionary<string, Product>(); byCat = new Dictionary<string, List<Product>>();
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ProductJson);
        if (ta == null) { Debug.LogError("[StoreFittings] missing " + ProductJson); return; }
        var list = JsonUtility.FromJson<ProductList>(ta.text);
        foreach (var p in list.products)
        {
            byId[p.id] = p;
            if (!byCat.TryGetValue(p.cat, out var l)) byCat[p.cat] = l = new List<Product>();
            l.Add(p);
        }
        meshCache.Clear();
    }

    static List<Product> Pick(string[] cats)
    {
        var res = new List<Product>();
        foreach (var c in cats)
        {
            if (byId.TryGetValue(c, out var one)) { res.Add(one); continue; }
            if (byCat.TryGetValue(c, out var l)) res.AddRange(l);
            else Debug.LogWarning("[StoreFittings] no products in category '" + c + "'");
        }
        return res;
    }

    /// <summary>A product mesh in its own space: origin bottom-centre, +z toward the customer, +x = the customer's left.</summary>
    public class LM
    {
        public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public List<Vector2> uv = new List<Vector2>();
        public List<int> t = new List<int>();

        public int V(Vector3 p, Vector3 nrm, Vector2 u) { v.Add(p); n.Add(nrm.normalized); uv.Add(u); return v.Count - 1; }

        public void Tri(int a, int b, int c)
        {
            Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(fn, n[a] + n[b] + n[c]) < 0f) { int s = b; b = c; c = s; }
            t.Add(a); t.Add(b); t.Add(c);
        }

        public void Quad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 nrm, Vector2 uBL, Vector2 uTL, Vector2 uTR, Vector2 uBR)
        {
            int a = V(bl, nrm, uBL), b = V(tl, nrm, uTL), c = V(tr, nrm, uTR), d = V(br, nrm, uBR);
            Tri(a, b, c); Tri(a, c, d);
        }

        public void QuadR(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 nrm, Rect r) =>
            Quad(bl, tl, tr, br, nrm, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin));
    }

    static LM Local(Product p, int lod)
    {
        string key = p.id + "#" + lod;
        if (meshCache.TryGetValue(key, out var m)) return m;
        m = new LM();
        switch (p.shape)
        {
            case "bag": BuildBag(m, p, lod, false); break;
            case "sack": BuildBag(m, p, lod, true); break;
            case "carton": BuildCarton(m, p, lod); break;
            case "lathe": BuildLathe(m, p, lod); break;
            default: BuildBox(m, p, lod); break;
        }
        meshCache[key] = m;
        return m;
    }

    static void BuildBag(LM m, Product p, int lod, bool sack)
    {
        int nx = lod == 0 ? 3 : 2, ny = lod == 0 ? 4 : 2;
        float du = p.u1 - p.u0, dv = p.v1 - p.v0;
        System.Func<float, float, float, Vector3> P = (u, v, side) =>
        {
            float t = Mathf.Clamp01((v - 0.045f) / 0.91f);
            float bulge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), sack ? 0.3f : 0.55f) * (1.1f - 0.25f * t);
            float uu = Mathf.Clamp01((u - 0.02f) / 0.96f);
            float thick = p.d * 0.5f * bulge * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * uu)), sack ? 0.35f : 0.6f);
            float wp = 1f - (sack ? 0.03f : 0.07f) * Mathf.Sin(Mathf.PI * v);
            return new Vector3((0.5f - u) * p.w * wp, v * p.h, side * thick);
        };
        for (int side = 1; side >= -1; side -= 2)
        {
            if (side < 0 && lod > 0) break;
            int start = m.v.Count;
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx, v = j / (float)ny;
                    Vector3 pos = P(u, v, side);
                    Vector3 du_ = P(Mathf.Min(1f, u + 0.02f), v, side) - P(Mathf.Max(0f, u - 0.02f), v, side);
                    Vector3 dv_ = P(u, Mathf.Min(1f, v + 0.02f), side) - P(u, Mathf.Max(0f, v - 0.02f), side);
                    Vector3 nn = Vector3.Cross(dv_, du_).normalized;
                    if (nn.z * side < 0) nn = -nn;
                    if (nn.sqrMagnitude < 0.5f) nn = new Vector3(0, 0, side);
                    float tu = side > 0 ? u : 1f - u;
                    m.V(pos, nn, new Vector2(p.u0 + tu * du, p.v0 + v * dv));
                }
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = start + j * (nx + 1) + i, b = a + nx + 1;
                    m.Tri(a, b, b + 1); m.Tri(a, b + 1, a + 1);
                }
        }
    }

    static void BuildBox(LM m, Product p, int lod)
    {
        float hw = p.w * 0.5f, hd = p.d * 0.5f, h = p.h;
        float du = p.u1 - p.u0, dv = p.v1 - p.v0;
        var full = new Rect(p.u0, p.v0, du, dv);
        var side = new Rect(p.u0 + du * 0.01f, p.v0, du * 0.05f, dv);
        Rect top = p.hasTop ? new Rect(p.tu0, p.tv0, p.tu1 - p.tu0, p.tv1 - p.tv0)
                 : p.cat == "candy" ? new Rect(p.u0, p.v0 + dv * 0.5f, du, dv * 0.47f)
                 : new Rect(p.u0, p.v1 - dv * 0.05f, du, dv * 0.04f);
        m.QuadR(new Vector3(hw, 0, hd), new Vector3(hw, h, hd), new Vector3(-hw, h, hd), new Vector3(-hw, 0, hd), Vector3.forward, full);
        m.QuadR(new Vector3(hw, h, hd), new Vector3(hw, h, -hd), new Vector3(-hw, h, -hd), new Vector3(-hw, h, hd), Vector3.up, top);
        if (lod > 0) return;
        m.QuadR(new Vector3(-hw, 0, -hd), new Vector3(-hw, h, -hd), new Vector3(hw, h, -hd), new Vector3(hw, 0, -hd), Vector3.back, full);
        m.QuadR(new Vector3(hw, 0, -hd), new Vector3(hw, h, -hd), new Vector3(hw, h, hd), new Vector3(hw, 0, hd), Vector3.right, side);
        m.QuadR(new Vector3(-hw, 0, hd), new Vector3(-hw, h, hd), new Vector3(-hw, h, -hd), new Vector3(-hw, 0, -hd), Vector3.left, side);
    }

    static void BuildCarton(LM m, Product p, int lod)
    {
        const float g = 0.16f;
        float hw = p.w * 0.5f, hd = p.d * 0.5f, hb = p.h * (1f - g), ht = p.h - 0.012f;
        float du = p.u1 - p.u0, dv = p.v1 - p.v0;
        var body = new Rect(p.u0, p.v0, du, dv * (1f - g));
        var roof = new Rect(p.u0, p.v0 + dv * (1f - g), du, dv * g * 0.9f);
        var strip = new Rect(p.u0 + du * 0.02f, p.v1 - dv * 0.05f, du * 0.1f, dv * 0.03f);
        m.QuadR(new Vector3(hw, 0, hd), new Vector3(hw, hb, hd), new Vector3(-hw, hb, hd), new Vector3(-hw, 0, hd), Vector3.forward, body);
        m.QuadR(new Vector3(hw, hb, hd), new Vector3(hw, ht, 0), new Vector3(-hw, ht, 0), new Vector3(-hw, hb, hd), new Vector3(0, 0.6f, 0.8f), roof);
        m.QuadR(new Vector3(hw, ht, 0), new Vector3(hw, p.h, 0), new Vector3(-hw, p.h, 0), new Vector3(-hw, ht, 0), Vector3.forward, strip);
        if (lod > 0) return;
        m.QuadR(new Vector3(-hw, 0, -hd), new Vector3(-hw, hb, -hd), new Vector3(hw, hb, -hd), new Vector3(hw, 0, -hd), Vector3.back, body);
        m.QuadR(new Vector3(hw, 0, -hd), new Vector3(hw, hb, -hd), new Vector3(hw, hb, hd), new Vector3(hw, 0, hd), Vector3.right, body);
        m.QuadR(new Vector3(-hw, 0, hd), new Vector3(-hw, hb, hd), new Vector3(-hw, hb, -hd), new Vector3(-hw, 0, -hd), Vector3.left, body);
        m.QuadR(new Vector3(-hw, hb, -hd), new Vector3(-hw, ht, 0), new Vector3(hw, ht, 0), new Vector3(hw, hb, -hd), new Vector3(0, 0.6f, -0.8f), roof);
        m.QuadR(new Vector3(-hw, ht, 0), new Vector3(-hw, p.h, 0), new Vector3(hw, p.h, 0), new Vector3(hw, ht, 0), Vector3.back, strip);
        foreach (int s in new[] { 1, -1 })
        {
            int a = m.V(new Vector3(s * hw, hb, hd), new Vector3(s, 0, 0), strip.min), b = m.V(new Vector3(s * hw, hb, -hd), new Vector3(s, 0, 0), strip.min), c = m.V(new Vector3(s * hw, ht, 0), new Vector3(s, 0, 0), strip.max);
            m.Tri(a, b, c);
        }
    }

    static void BuildLathe(LM m, Product p, int lod)
    {
        var ys = new List<float>(); var rs = new List<float>();
        if (p.profile == null || p.profile.Length < 4) { ys.Add(0); rs.Add(1); ys.Add(1); rs.Add(1); }
        else if (lod == 0) for (int i = 0; i + 1 < p.profile.Length; i += 2) { ys.Add(p.profile[i]); rs.Add(p.profile[i + 1]); }
        else
        {   // far rows: the silhouette only
            int n = p.profile.Length / 2;
            int[] keep = n <= 4 ? new[] { 0, n - 1 } : new[] { 0, n * 2 / 3, n - 1 };
            foreach (int k in keep) { ys.Add(p.profile[k * 2]); rs.Add(p.profile[k * 2 + 1]); }
        }
        int half = lod > 0 ? 3 : (p.w > 0.12f ? 6 : p.w > 0.06f ? 4 : 3);
        float R0 = p.w * 0.5f, zs = p.sq <= 0f ? 1f : p.sq;
        float du = p.u1 - p.u0, dv = p.v1 - p.v0;
        int rings = ys.Count;
        for (int side = 0; side < 2; side++)
        {
            int start = m.v.Count;
            for (int j = 0; j < rings; j++)
            {
                int j0 = Mathf.Max(0, j - 1), j1 = Mathf.Min(rings - 1, j + 1);
                float dr = (rs[j1] - rs[j0]) * R0, dy = Mathf.Max(0.0001f, (ys[j1] - ys[j0]) * p.h);
                for (int i = 0; i <= half; i++)
                {
                    float u = i / (float)half, phi = (u - 0.5f) * Mathf.PI;
                    float sx = -Mathf.Sin(phi), cz = Mathf.Cos(phi);
                    if (side == 1) { sx = -sx; cz = -cz; }
                    Vector3 radial = new Vector3(sx, 0, cz);
                    Vector3 pos = new Vector3(sx * rs[j] * R0, ys[j] * p.h, cz * rs[j] * R0 * zs);
                    Vector3 nn = new Vector3(radial.x * dy, -dr, radial.z * dy / Mathf.Max(0.3f, zs));
                    m.V(pos, nn, new Vector2(p.u0 + u * du, p.v0 + ys[j] * dv));
                }
            }
            for (int j = 0; j < rings - 1; j++)
                for (int i = 0; i < half; i++)
                {
                    int a = start + j * (half + 1) + i, b = a + half + 1;
                    if ((m.v[a] - m.v[a + 1]).sqrMagnitude < 1e-10f && (m.v[b] - m.v[b + 1]).sqrMagnitude < 1e-10f) continue;
                    m.Tri(a, b, b + 1); m.Tri(a, b + 1, a + 1);
                }
        }
        // lid
        float rt = rs[rings - 1] * R0;
        if (rt > 0.004f)
        {
            Vector2 capUV = new Vector2(p.u0 + du * 0.5f, p.v1 - dv * 0.012f);
            System.Func<float, float, Vector2> TU = (x, z) => p.hasTop
                ? new Vector2(p.tu0 + (rt - x) / (2f * rt) * (p.tu1 - p.tu0), p.tv0 + (rt * zs - z) / (2f * rt * zs) * (p.tv1 - p.tv0))
                : capUV;
            int seg = half * 2;
            int c = m.V(new Vector3(0, p.h, 0), Vector3.up, TU(0, 0));
            int first = m.v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * rt, z = Mathf.Sin(a) * rt * zs;
                m.V(new Vector3(x, p.h, z), Vector3.up, TU(x, z));
            }
            for (int i = 0; i < seg; i++) m.Tri(c, first + i, first + i + 1);
        }
    }

    // ------------------------------------------------------------------ mesh accumulator (world coordinates)

    public class Acc
    {
        public List<Vector3> v = new List<Vector3>();
        public List<Vector3> n = new List<Vector3>();
        public List<Vector2> uv = new List<Vector2>();
        public List<int> t = new List<int>();

        public void Quad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 nrm, Vector2 uvBL, Vector2 uvTR)
        {
            int i = v.Count;
            v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
            n.Add(nrm); n.Add(nrm); n.Add(nrm); n.Add(nrm);
            uv.Add(uvBL); uv.Add(new Vector2(uvBL.x, uvTR.y)); uv.Add(uvTR); uv.Add(new Vector2(uvTR.x, uvBL.y));
            if (Vector3.Dot(Vector3.Cross(tl - bl, tr - bl), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        /// <summary>A face seen from direction nrm, centred at c, with "up" along upv; uv rect given.</summary>
        public void Face(Vector3 c, Vector3 nrm, Vector3 upv, float w, float h, Vector2 uvBL, Vector2 uvTR)
        {
            Vector3 up = upv.normalized;
            Vector3 right = Vector3.Cross(up, -nrm).normalized;
            Quad(c - right * w * 0.5f - up * h * 0.5f, c - right * w * 0.5f + up * h * 0.5f, c + right * w * 0.5f + up * h * 0.5f, c + right * w * 0.5f - up * h * 0.5f, nrm, uvBL, uvTR);
        }

        /// <summary>Face with a tiling texture: 'mpt' metres per texture repeat.</summary>
        public void FaceM(Vector3 c, Vector3 nrm, Vector3 upv, float w, float h, float mpt, float u0 = 0f, float v0 = 0f) =>
            Face(c, nrm, upv, w, h, new Vector2(u0, v0), new Vector2(u0 + w / mpt, v0 + h / mpt));

        /// <summary>Seen from both sides (cut-out wire, glass).</summary>
        public void Face2(Vector3 c, Vector3 nrm, Vector3 upv, float w, float h, Vector2 uvBL, Vector2 uvTR)
        {
            Face(c, nrm, upv, w, h, uvBL, uvTR);
            Face(c, -nrm, upv, w, h, new Vector2(uvTR.x, uvBL.y), new Vector2(uvBL.x, uvTR.y));
        }

        /// <summary>Box with a tiling texture ('mpt' metres per repeat). 'front' is a horizontal direction.</summary>
        public void BoxM(Vector3 c, Vector3 size, Vector3 front, float mpt, bool bottom = false)
        {
            front = new Vector3(front.x, 0, front.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -front).normalized;
            float w = size.x, h = size.y, d = size.z;
            float o = (c.x * 0.37f + c.z * 0.53f + c.y * 0.71f) % 1f;
            FaceM(c + front * d * 0.5f, front, Vector3.up, w, h, mpt, o, o * 0.7f);
            FaceM(c - front * d * 0.5f, -front, Vector3.up, w, h, mpt, o + 0.3f, o);
            FaceM(c + right * w * 0.5f, right, Vector3.up, d, h, mpt, o + 0.5f, o);
            FaceM(c - right * w * 0.5f, -right, Vector3.up, d, h, mpt, o + 0.7f, o);
            FaceM(c + Vector3.up * h * 0.5f, Vector3.up, front, w, d, mpt, o, o + 0.2f);
            if (bottom) FaceM(c - Vector3.up * h * 0.5f, Vector3.down, front, w, d, mpt, o, o + 0.6f);
        }

        /// <summary>Box whose every face shows the same uv rect (stickers, crates).</summary>
        public void BoxR(Vector3 c, Vector3 size, Vector3 front, Rect r, bool top = true)
        {
            front = new Vector3(front.x, 0, front.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -front).normalized;
            float w = size.x, h = size.y, d = size.z;
            Face(c + front * d * 0.5f, front, Vector3.up, w, h, r.min, r.max);
            Face(c - front * d * 0.5f, -front, Vector3.up, w, h, r.min, r.max);
            Face(c + right * w * 0.5f, right, Vector3.up, d, h, r.min, r.max);
            Face(c - right * w * 0.5f, -right, Vector3.up, d, h, r.min, r.max);
            if (top) Face(c + Vector3.up * h * 0.5f, Vector3.up, front, w, d, r.min, r.max);
        }

        public void Ellipsoid(Vector3 c, Vector3 radii, Quaternion rot, int seg, Rect uvr)
        {
            int rings = Mathf.Max(3, seg / 2);
            int start = v.Count;
            for (int y = 0; y <= rings; y++)
            {
                float phi = Mathf.PI * y / rings;
                for (int x = 0; x <= seg; x++)
                {
                    float th = 2f * Mathf.PI * x / seg;
                    var dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    v.Add(c + rot * Vector3.Scale(dir, radii));
                    n.Add((rot * new Vector3(dir.x / radii.x, dir.y / radii.y, dir.z / radii.z)).normalized);
                    uv.Add(new Vector2(uvr.xMin + uvr.width * x / seg, uvr.yMax - uvr.height * y / rings));
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    int a = start + y * (seg + 1) + x, b = a + seg + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b + 1); t.Add(a); t.Add(b + 1); t.Add(b);
                }
        }

        public void Append(LM m, Vector3 pos, Quaternion rot)
        {
            int s = v.Count;
            for (int i = 0; i < m.v.Count; i++) { v.Add(pos + rot * m.v[i]); n.Add(rot * m.n[i]); uv.Add(m.uv[i]); }
            for (int i = 0; i < m.t.Count; i++) t.Add(s + m.t[i]);
        }

        public GameObject Emit(string name, Transform parent, Material m, bool shadows = true)
        {
            if (v.Count == 0) return null;
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var vv = new List<Vector3>(v.Count); foreach (var p in v) vv.Add(p + off);
            mesh.SetVertices(vv); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            triCount += t.Count / 3;
            mesh = SaveMesh(mesh, name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;   // the mesh is in world coordinates; the holder parent sits at the fitting
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.allowOcclusionWhenDynamic = false;
            return go;
        }
    }

    // ------------------------------------------------------------------ materials

    static Material MMetal => Mat("SM_FitMetal", "fit_metal_cream.png", Color.white, 0.22f, false, false);
    static Material MPeg => Mat("SM_FitPeg", "fit_pegboard.png", Color.white, 0.15f, false, false);
    static Material MUpright => Mat("SM_FitUpright", "fit_upright.png", Color.white, 0.2f, false, false);
    static Material MWire => Mat("SM_FitWire", "fit_wire.png", Color.white, 0.6f, false, true);
    static Material MBasket => Mat("SM_FitBasket", "fit_basket.png", Color.white, 0.6f, false, true);
    static Material MPrice => Mat("SM_PriceStrip", "price_strip.png", Color.white, 0.15f, false, false);
    static Material MProducts => Mat("SM_Products", "Products/products_atlas.png", Color.white, 0.22f, false, false);
    static Material MSteel => Mat("SM_FitSteel", "fit_steel.png", Color.white, 0.55f, false, false);
    static Material MGrille => Mat("SM_FitGrille", "fit_grille.png", Color.white, 0.3f, false, false);
    static Material MGlass => Mat("SM_FitGlass", "fit_glass_dirty.png", new Color(1f, 1f, 1f, 0.55f), 0.9f, true, false);
    static Material MFrost => Mat("SM_FitFrost", "fit_frost.png", new Color(1f, 1f, 1f, 0.5f), 0.7f, true, false);
    static Material MWhiteIn => Mat("SM_FitFridgeInside", "fit_fridge_white.png", new Color(0.92f, 0.94f, 0.95f), 0.25f, false, false);
    static Material MDark => Mat("SM_FitDark", null, new Color(0.07f, 0.07f, 0.08f), 0.45f, false, false);
    static Material MCard => Mat("SM_FitCardboard", "fit_cardboard.png", Color.white, 0.05f, false, false);
    static Material MProduce => Mat("SM_Produce", "produce_atlas.png", Color.white, 0.3f, false, false);
    static Material Body(string tex) => Mat("SM_" + tex, tex + ".png", Color.white, 0.4f, false, false);

    // ------------------------------------------------------------------ stocking

    static readonly List<string> stackable = new List<string> { "canned", "tomato", "evap", "tuna", "soap", "tissue", "toothpaste", "cheese_flat", "butter", "eggs", "juice_small",
        "choc_box", "frozen_box", "cheese_box", "yogurt", "biscuits", "baking", "milkpowder", "noodles_flat", "eggs_tray", "water_pack", "carton_box", "icecream", "choc_egg", "candy", "cig", "cig_carton", "pharma", "counter_misc" };

    static bool NearPickup(Vector3 p, float r = 0.2f)
    {
        foreach (var k in keepClear)
            if ((new Vector3(k.x, 0, k.z) - new Vector3(p.x, 0, p.z)).sqrMagnitude < r * r && Mathf.Abs(k.y - p.y) < 0.4f) return true;
        return false;
    }

    static void Place(Acc a, Product p, Vector3 pos, Vector3 face, int lod, float yaw = 0f, float pitch = 0f, float roll = 0f)
    {
        a.Append(Local(p, lod), pos, Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(pitch, yaw, roll));
    }

    /// <summary>
    /// Stock one shelf: 'from' is one end of the shelf's front edge at shelf height, 'along' runs to the other end,
    /// 'face' points at the customer. Products get a few facings each, rows behind them, never wider than the shelf,
    /// never taller than 'clearH', and nothing lands on a pickup.
    /// </summary>
    static void FillShelf(Acc a, Vector3 from, Vector3 along, Vector3 face, float length, float depth, float clearH, string[] cats,
                          int maxRows = 4, float gapChance = 0.05f, float lean = 0f, int maxStack = 3)
    {
        var all = Pick(cats);
        var list = new List<Product>();
        foreach (var p in all) if (p.h <= clearH - 0.012f && p.w <= length - 0.04f && Mathf.Min(p.d, p.w) <= depth) list.Add(p);
        if (list.Count == 0) return;
        for (int i = list.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var s = list[i]; list[i] = list[j]; list[j] = s; }
        along = along.normalized;
        float x = R(0.015f, 0.04f);
        int idx = 0, guard = 0;
        while (guard++ < 400)
        {
            Product p = list[idx % list.Count]; idx++;
            bool bag = p.shape == "bag" || p.shape == "sack";
            bool lathe = p.shape == "lathe";
            float pw = p.w, pd = bag ? Mathf.Max(0.035f, p.d * 0.8f) : (lathe ? p.w * (p.sq <= 0 ? 1f : p.sq) : p.d);
            int facings = pw > 0.19f ? rnd.Next(1, 3) : pw > 0.1f ? rnd.Next(2, 4) : rnd.Next(3, 6);
            if (x + pw > length - 0.015f) { if (idx > list.Count * 2) break; else continue; }
            for (int f = 0; f < facings; f++)
            {
                if (x + pw > length - 0.015f) break;
                Vector3 basePos = from + along * (x + pw * 0.5f);
                if (NearPickup(basePos - face * (depth * 0.5f), 0.26f)) { x += pw + 0.01f; continue; }
                int rows = Mathf.Clamp(Mathf.FloorToInt((depth - 0.02f) / (pd + 0.006f)), 1, maxRows);
                int stack = stackable.Contains(p.cat) ? Mathf.Clamp(Mathf.FloorToInt((clearH - 0.015f) / p.h), 1, maxStack) : 1;
                int sold = R() < gapChance ? rnd.Next(1, Mathf.Min(rows, 2) + 1) : 0;   // front items already bought
                if (sold >= rows && R() < 0.5f) sold = rows - 1;
                for (int r = sold; r < rows; r++)
                {
                    int lod = r == sold ? 0 : 1;
                    int st = stack;
                    if (stack > 1 && r == sold && R() < 0.3f) st = rnd.Next(1, stack + 1);
                    for (int s = 0; s < st; s++)
                    {
                        Vector3 pos = basePos - face * (0.012f + pd * 0.5f + r * (pd + 0.006f)) + Vector3.up * (s * p.h)
                                    + along * R(-0.003f, 0.003f) + face * R(-0.004f, 0.004f);
                        float yaw = lathe ? R(-16f, 16f) : R(-3.5f, 3.5f);
                        float pitch = bag ? -(lean > 0f ? lean : 7f) - R(0f, 6f) : 0f;
                        Place(a, p, pos, face, s > 0 ? Mathf.Max(lod, 0) : lod, yaw, pitch, bag ? R(-3f, 3f) : 0f);
                    }
                }
                x += pw + R(0.005f, 0.014f);
            }
            if (x > length - 0.06f) break;
        }
    }

    // ------------------------------------------------------------------ fittings

    static GameObject Holder(string name, Transform parent, Bounds solid, bool obstacle = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = solid.center + off;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = solid.size;
        if (obstacle) { var ob = go.AddComponent<NavMeshObstacle>(); ob.shape = NavMeshObstacleShape.Box; ob.size = solid.size; ob.carving = true; }
        fixtureBounds.Add(new Bounds(solid.center + off, solid.size));
        fixtureNames.Add(name);
        return go;
    }

    static Bounds Span(Vector3 a, Vector3 b) { var bb = new Bounds(a, Vector3.zero); bb.Encapsulate(b); return bb; }

    /// <summary>Shelf surfaces above the floor (also used to re-seat the pickups).</summary>
    public static float[] Levels = { 0.14f, 0.52f, 0.88f, 1.24f, 1.58f };
    const float ShelfH = 1.92f, ShelfD = 0.44f;

    /// <summary>One run of shelves on one side of a back panel: boards, price rail, wire front, stock.</summary>
    static void ShelfRun(Acc metal, Acc price, Acc wire, Acc prod, Vector3 backLeft, Vector3 along, Vector3 face, float len, string[][] plan, float depth = ShelfD)
    {
        for (int i = 0; i < Levels.Length; i++)
        {
            float ly = FloorY + Levels[i];
            Vector3 mid = backLeft + along * len * 0.5f + face * depth * 0.5f; mid.y = ly;
            Vector3 front = backLeft + along * len * 0.5f + face * depth; front.y = ly;
            if (i > 0) metal.BoxM(mid + Vector3.down * 0.0125f, new Vector3(len, 0.025f, depth), face, 1.1f, true);
            price.Face(front + face * 0.003f + Vector3.down * 0.022f, face, Vector3.up, len, 0.042f, new Vector2(R(), 0f), new Vector2(R() + len / 1.5f, 1f));
            wire.Face2(front + Vector3.up * 0.032f - face * 0.004f, face, Vector3.up, len, 0.064f, new Vector2(0f, 0.5f), new Vector2(len / 0.25f, 1f));
            float clear = (i + 1 < Levels.Length ? Levels[i + 1] - Levels[i] - 0.03f : 0.44f);
            Vector3 from = backLeft + face * (depth - 0.012f) + along * 0.03f; from.y = ly;
            if (plan != null && i < plan.Length && plan[i] != null)
                FillShelf(prod, from, along, face, len - 0.06f, depth - 0.03f, clear, plan[i], 3, 0.06f);
        }
    }

    /// <summary>Double-sided island shelving along x: cream steel, perforated back, price rails, wire fronts, basket end caps.</summary>
    public static void Gondola(Transform parent, string name, float x0, float x1, float zc, string[][] north, string[][] south, string[] westCap, string[] eastCap)
    {
        const float capD = 0.3f;
        float D = ShelfD * 2f + 0.04f, len = x1 - x0;
        var go = Holder(name, parent, new Bounds(new Vector3((x0 + x1) * 0.5f, FloorY + ShelfH * 0.5f, zc), new Vector3(len + capD * 2f + 0.04f, ShelfH, D + 0.02f)));
        var metal = new Acc(); var peg = new Acc(); var up = new Acc(); var price = new Acc(); var wire = new Acc(); var basket = new Acc(); var prod = new Acc();
        float y0 = FloorY;
        // plinth (bottom deck), back panel, uprights
        metal.BoxM(new Vector3((x0 + x1) * 0.5f, y0 + Levels[0] * 0.5f, zc), new Vector3(len, Levels[0], D), Vector3.forward, 1.1f);
        peg.BoxM(new Vector3((x0 + x1) * 0.5f, y0 + (ShelfH + Levels[0]) * 0.5f, zc), new Vector3(len, ShelfH - Levels[0], 0.03f), Vector3.forward, 0.5f);
        int bays = Mathf.Max(1, Mathf.RoundToInt(len / 1.25f));
        for (int b = 0; b <= bays; b++)
            up.BoxM(new Vector3(x0 + len * b / bays, y0 + ShelfH * 0.5f + 0.02f, zc), new Vector3(0.05f, ShelfH + 0.04f, 0.07f), Vector3.forward, 0.6f);
        ShelfRun(metal, price, wire, prod, new Vector3(x0, 0, zc + 0.02f), Vector3.right, Vector3.forward, len, north);
        ShelfRun(metal, price, wire, prod, new Vector3(x1, 0, zc - 0.02f), Vector3.left, Vector3.back, len, south);
        // end caps: an end panel and wire baskets of snacks facing along the aisle
        foreach (int e in new[] { -1, 1 })
        {
            float xe = e < 0 ? x0 : x1; Vector3 face = new Vector3(e, 0, 0); Vector3 along = new Vector3(0, 0, -e);
            string[] cats = e < 0 ? westCap : eastCap;
            metal.BoxM(new Vector3(xe + e * 0.012f, y0 + ShelfH * 0.5f, zc), new Vector3(D, ShelfH, 0.025f), face, 1.1f);
            for (int i = 0; i < Levels.Length; i++)
            {
                float ly = y0 + Levels[i];
                Vector3 c = new Vector3(xe + e * (0.025f + capD * 0.5f), ly, zc);
                metal.BoxM(c + Vector3.down * 0.01f, new Vector3(D - 0.04f, 0.02f, capD), face, 1.1f, true);
                Vector3 fr = new Vector3(xe + e * (0.025f + capD), ly, zc);
                basket.Face2(fr + Vector3.up * 0.065f, face, Vector3.up, D - 0.04f, 0.13f, new Vector2(0, 0), new Vector2((D - 0.04f) / 0.25f, 1));
                foreach (int s in new[] { -1, 1 })
                    basket.Face2(c + new Vector3(0, 0.065f, s * (D - 0.04f) * 0.5f), new Vector3(0, 0, s), Vector3.up, capD, 0.13f, new Vector2(0, 0), new Vector2(capD / 0.25f, 1));
                price.Face(fr + face * 0.004f + Vector3.down * 0.024f, face, Vector3.up, D - 0.04f, 0.042f, new Vector2(R(), 0f), new Vector2(R() + (D - 0.04f) / 1.5f, 1f));
                float clear = (i + 1 < Levels.Length ? Levels[i + 1] - Levels[i] - 0.025f : 0.44f);
                Vector3 from = fr - face * 0.014f - along * ((D - 0.04f) * 0.5f - 0.02f);
                if (cats != null) FillShelf(prod, from, along, face, D - 0.08f, capD - 0.03f, clear, cats, 3, 0.04f, 12f);
            }
        }
        metal.Emit(name + " Frame", go.transform, MMetal);
        peg.Emit(name + " Back", go.transform, MPeg);
        up.Emit(name + " Uprights", go.transform, MUpright, false);
        price.Emit(name + " Prices", go.transform, MPrice, false);
        wire.Emit(name + " Wire", go.transform, MWire, false);
        basket.Emit(name + " Baskets", go.transform, MBasket, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    /// <summary>Single-sided shelving with its back to a wall. a -> b runs along the wall; 'face' points into the room.</summary>
    public static void WallShelf(Transform parent, string name, Vector3 a, Vector3 b, Vector3 face, string[][] plan, float depth = ShelfD)
    {
        a.y = 0; b.y = 0;
        Vector3 along = (b - a).normalized; float len = (b - a).magnitude;
        Vector3 mid = (a + b) * 0.5f + face * (depth + 0.03f) * 0.5f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        var go = Holder(name, parent, new Bounds(new Vector3(mid.x, FloorY + ShelfH * 0.5f, mid.z), new Vector3(alongX ? len : depth + 0.03f, ShelfH, alongX ? depth + 0.03f : len)));
        var metal = new Acc(); var peg = new Acc(); var up = new Acc(); var price = new Acc(); var wire = new Acc(); var prod = new Acc();
        Vector3 backMid = (a + b) * 0.5f + face * 0.015f;
        peg.BoxM(new Vector3(backMid.x, FloorY + ShelfH * 0.5f, backMid.z), new Vector3(len, ShelfH, 0.03f), face, 0.5f);
        Vector3 pm = (a + b) * 0.5f + face * (0.03f + depth * 0.5f);
        metal.BoxM(new Vector3(pm.x, FloorY + Levels[0] * 0.5f, pm.z), new Vector3(len, Levels[0], depth), face, 1.1f);
        foreach (float s in new[] { 0f, 1f })
        {
            Vector3 e = a + (b - a) * s + face * (0.03f + depth * 0.5f) + along * (s < 0.5f ? 0.012f : -0.012f);
            metal.BoxM(new Vector3(e.x, FloorY + ShelfH * 0.5f, e.z), new Vector3(depth, ShelfH, 0.025f), along, 1.1f);
        }
        int bays = Mathf.Max(1, Mathf.RoundToInt(len / 1.2f));
        for (int k = 1; k < bays; k++)
        {
            Vector3 u = a + (b - a) * (k / (float)bays) + face * 0.05f;
            up.BoxM(new Vector3(u.x, FloorY + ShelfH * 0.5f, u.z), new Vector3(0.05f, ShelfH, 0.05f), face, 0.6f);
        }
        ShelfRun(metal, price, wire, prod, a + face * 0.03f + along * 0.025f, along, face, len - 0.05f, plan, depth);
        metal.Emit(name + " Frame", go.transform, MMetal);
        peg.Emit(name + " Back", go.transform, MPeg);
        up.Emit(name + " Uprights", go.transform, MUpright, false);
        price.Emit(name + " Prices", go.transform, MPrice, false);
        wire.Emit(name + " Wire", go.transform, MWire, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    /// <summary>Upright glass-door drinks fridge against a wall, with a branded header panel.</summary>
    public static void GlassFridge(Transform parent, string name, Vector3 wallLeft, Vector3 along, Vector3 face, int doors, string bodyTex, string header, string[][] plan, float doorW = 0.68f)
    {
        wallLeft.y = 0f;
        float W = doors * doorW + 0.1f, D = 0.72f, H = 2.0f, HH = 0.26f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        System.Func<float, float, float, Vector3> C = (u, y, d) => wallLeft + along * u + face * d + Vector3.up * (FloorY + y);
        var go = Holder(name, parent, new Bounds(C(W * 0.5f, (H + HH) * 0.5f, D * 0.5f), new Vector3(alongX ? W : D, H + HH, alongX ? D : W)));
        var body = new Acc(); var inner = new Acc(); var steel = new Acc(); var grille = new Acc(); var dark = new Acc(); var prod = new Acc(); var glass = new Acc(); var hdr = new Acc(); var shelf = new Acc();
        // shell
        body.BoxM(C(0.025f, H * 0.5f, D * 0.5f), new Vector3(D, H, 0.05f), along, 1.2f);
        body.BoxM(C(W - 0.025f, H * 0.5f, D * 0.5f), new Vector3(D, H, 0.05f), along, 1.2f);
        body.BoxM(C(W * 0.5f, H - 0.03f, D * 0.5f), new Vector3(W, 0.06f, D), face, 1.2f);
        body.BoxM(C(W * 0.5f, 0.11f, D * 0.5f - 0.02f), new Vector3(W - 0.1f, 0.22f, D - 0.06f), face, 1.2f);
        grille.Face(C(W * 0.5f, 0.11f, D - 0.045f), face, Vector3.up, W - 0.1f, 0.2f, Vector2.zero, new Vector2((W - 0.1f) / 0.4f, 1f));
        inner.FaceM(C(W * 0.5f, H * 0.5f + 0.1f, 0.04f), face, Vector3.up, W - 0.1f, H - 0.3f, 1.3f);
        inner.FaceM(C(0.052f, H * 0.5f + 0.1f, D * 0.5f), along, Vector3.up, D - 0.06f, H - 0.3f, 1.3f);
        inner.FaceM(C(W - 0.052f, H * 0.5f + 0.1f, D * 0.5f), -along, Vector3.up, D - 0.06f, H - 0.3f, 1.3f);
        // header panel
        body.BoxM(C(W * 0.5f, H + HH * 0.5f, D * 0.5f + 0.05f), new Vector3(W, HH, D - 0.1f), face, 1.2f);
        hdr.Face(C(W * 0.5f, H + HH * 0.5f, D + 0.003f), face, Vector3.up, W - 0.04f, HH - 0.03f, Vector2.zero, Vector2.one);
        // shelves + stock
        float[] sy = { 0.235f, 0.61f, 0.96f, 1.29f, 1.61f };
        for (int i = 0; i < sy.Length; i++)
        {
            float clear = (i + 1 < sy.Length ? sy[i + 1] - sy[i] : 1.93f - sy[i]) - 0.025f;
            if (i > 0) shelf.BoxM(C(W * 0.5f, sy[i] - 0.008f, D * 0.5f - 0.02f), new Vector3(W - 0.1f, 0.016f, D - 0.2f), face, 1.1f, true);
            if (plan != null && i < plan.Length && plan[i] != null)
                FillShelf(prod, C(0.07f, sy[i], D - 0.13f), along, face, W - 0.14f, D - 0.24f, clear, plan[i], 4, 0.08f);
        }
        // doors
        float dy0 = 0.225f, dh = H - 0.06f - dy0;
        for (int k = 0; k < doors; k++)
        {
            float u0 = 0.05f + k * doorW, uc = u0 + doorW * 0.5f;
            glass.Face(C(uc, dy0 + dh * 0.5f, D - 0.012f), face, Vector3.up, doorW - 0.07f, dh - 0.07f, Vector2.zero, Vector2.one);
            steel.BoxM(C(u0 + 0.02f, dy0 + dh * 0.5f, D - 0.02f), new Vector3(0.04f, dh, 0.04f), face, 0.8f);
            steel.BoxM(C(u0 + doorW - 0.02f, dy0 + dh * 0.5f, D - 0.02f), new Vector3(0.04f, dh, 0.04f), face, 0.8f);
            steel.BoxM(C(uc, dy0 + 0.02f, D - 0.02f), new Vector3(doorW - 0.08f, 0.04f, 0.04f), face, 0.8f);
            steel.BoxM(C(uc, dy0 + dh - 0.02f, D - 0.02f), new Vector3(doorW - 0.08f, 0.04f, 0.04f), face, 0.8f);
            dark.BoxM(C(u0 + doorW - 0.075f, dy0 + dh * 0.5f, D + 0.018f), new Vector3(0.022f, 0.46f, 0.03f), face, 1f);
        }
        body.Emit(name + " Body", go.transform, Body(bodyTex));
        inner.Emit(name + " Interior", go.transform, MWhiteIn, false);
        shelf.Emit(name + " Shelves", go.transform, MMetal, false);
        steel.Emit(name + " Door Frames", go.transform, MSteel, false);
        grille.Emit(name + " Grille", go.transform, MGrille, false);
        dark.Emit(name + " Handles", go.transform, MDark, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
        hdr.Emit(name + " Header", go.transform, Mat("SM_Hdr_" + header, header + ".png", Color.white, 0.35f, false, false), false);
        glass.Emit(name + " Glass", go.transform, MGlass, false);
    }

    /// <summary>Open multideck chiller (dairy): white body, blue fascia and bumper, stepped shelves over a well.</summary>
    public static void DairyChiller(Transform parent, string name, Vector3 wallLeft, Vector3 along, Vector3 face, float W, string[][] plan)
    {
        wallLeft.y = 0f;
        float D = 0.8f, H = 2.0f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        System.Func<float, float, float, Vector3> C = (u, y, d) => wallLeft + along * u + face * d + Vector3.up * (FloorY + y);
        var go = Holder(name, parent, new Bounds(C(W * 0.5f, H * 0.5f, D * 0.5f), new Vector3(alongX ? W : D, H, alongX ? D : W)));
        var white = new Acc(); var blue = new Acc(); var steel = new Acc(); var price = new Acc(); var prod = new Acc(); var hdr = new Acc(); var grille = new Acc();
        white.BoxM(C(W * 0.5f, 0.25f, D * 0.5f), new Vector3(W, 0.5f, D), face, 1.2f);                      // base
        blue.BoxM(C(W * 0.5f, 0.47f, D + 0.012f), new Vector3(W, 0.07f, 0.03f), face, 1.2f);               // bumper rail
        grille.Face(C(W * 0.5f, 0.1f, D + 0.002f), face, Vector3.up, W - 0.2f, 0.14f, Vector2.zero, new Vector2((W - 0.2f) / 0.4f, 0.7f));
        white.BoxM(C(0.025f, H * 0.5f, D * 0.45f), new Vector3(D * 0.9f, H, 0.05f), along, 1.2f);          // cheeks
        white.BoxM(C(W - 0.025f, H * 0.5f, D * 0.45f), new Vector3(D * 0.9f, H, 0.05f), along, 1.2f);
        steel.FaceM(C(W * 0.5f, 1.15f, 0.05f), face, Vector3.up, W - 0.1f, 1.3f, 0.9f);                    // back wall
        white.BoxM(C(W * 0.5f, H - 0.11f, D * 0.36f), new Vector3(W, 0.22f, D * 0.72f), face, 1.2f);       // canopy
        hdr.Face(C(W * 0.5f, H - 0.11f, D * 0.72f + 0.003f), face, Vector3.up, W - 0.02f, 0.2f, Vector2.zero, Vector2.one);
        float[] sy = { 0.5f, 0.93f, 1.25f, 1.54f }; float[] sd = { 0.66f, 0.46f, 0.4f, 0.34f };
        for (int i = 0; i < sy.Length; i++)
        {
            float clear = (i + 1 < sy.Length ? sy[i + 1] - sy[i] : 1.78f - sy[i]) - 0.03f;
            if (i > 0)
            {
                white.BoxM(C(W * 0.5f, sy[i] - 0.012f, 0.05f + sd[i] * 0.5f), new Vector3(W - 0.1f, 0.024f, sd[i]), face, 1.2f, true);
                price.Face(C(W * 0.5f, sy[i] - 0.022f, 0.05f + sd[i] + 0.003f), face, Vector3.up, W - 0.1f, 0.042f, new Vector2(R(), 0), new Vector2(R() + (W - 0.1f) / 1.5f, 1));
            }
            if (plan != null && i < plan.Length && plan[i] != null)
                FillShelf(prod, C(0.07f, sy[i], 0.05f + sd[i] - 0.015f), along, face, W - 0.14f, sd[i] - 0.04f, clear, plan[i], 4, 0.07f);
        }
        white.Emit(name + " Body", go.transform, Body("fit_fridge_white"));
        blue.Emit(name + " Trim", go.transform, Body("fit_fridge_blue"));
        steel.Emit(name + " Back", go.transform, MSteel, false);
        grille.Emit(name + " Grille", go.transform, MGrille, false);
        price.Emit(name + " Prices", go.transform, MPrice, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
        hdr.Emit(name + " Header", go.transform, Mat("SM_Hdr_header_dairy", "header_dairy.png", Color.white, 0.35f, false, false), false);
    }

    /// <summary>Chest freezer with sliding frosted lids; frozen food lying inside, a brand sticker on the front.</summary>
    public static void ChestFreezer(Transform parent, string name, Vector3 wallLeft, Vector3 along, Vector3 face, float W, string[] cats)
    {
        wallLeft.y = 0f;
        float D = 0.85f, H = 0.88f, wall = 0.07f, deck = 0.5f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        System.Func<float, float, float, Vector3> C = (u, y, d) => wallLeft + along * u + face * d + Vector3.up * (FloorY + y);
        var go = Holder(name, parent, new Bounds(C(W * 0.5f, H * 0.5f, D * 0.5f), new Vector3(alongX ? W : D, H, alongX ? D : W)));
        var white = new Acc(); var blue = new Acc(); var prod = new Acc(); var glass = new Acc(); var inner = new Acc(); var sticker = new Acc(); var grille = new Acc();
        white.BoxM(C(W * 0.5f, H * 0.5f, wall * 0.5f), new Vector3(W, H, wall), face, 1.2f);
        white.BoxM(C(W * 0.5f, H * 0.5f, D - wall * 0.5f), new Vector3(W, H, wall), face, 1.2f);
        white.BoxM(C(wall * 0.5f, H * 0.5f, D * 0.5f), new Vector3(D, H, wall), along, 1.2f);
        white.BoxM(C(W - wall * 0.5f, H * 0.5f, D * 0.5f), new Vector3(D, H, wall), along, 1.2f);
        inner.FaceM(C(W * 0.5f, deck, D * 0.5f), Vector3.up, face, W - wall * 2, D - wall * 2, 1.3f);
        // blue top frame
        blue.BoxM(C(W * 0.5f, H + 0.02f, wall * 0.5f), new Vector3(W + 0.02f, 0.05f, wall + 0.01f), face, 1.2f);
        blue.BoxM(C(W * 0.5f, H + 0.02f, D - wall * 0.5f), new Vector3(W + 0.02f, 0.05f, wall + 0.01f), face, 1.2f);
        blue.BoxM(C(wall * 0.5f, H + 0.02f, D * 0.5f), new Vector3(D, 0.05f, wall + 0.01f), along, 1.2f);
        blue.BoxM(C(W - wall * 0.5f, H + 0.02f, D * 0.5f), new Vector3(D, 0.05f, wall + 0.01f), along, 1.2f);
        blue.BoxM(C(W * 0.5f, H + 0.02f, D * 0.5f), new Vector3(0.05f, 0.05f, D - wall * 2), face, 1.2f);
        grille.Face(C(W - 0.3f, 0.13f, D + 0.002f), face, Vector3.up, 0.4f, 0.16f, Vector2.zero, new Vector2(1f, 0.8f));
        sticker.Face(C(W * 0.42f, 0.5f, D + 0.003f), face, Vector3.up, Mathf.Min(1.1f, W * 0.6f), Mathf.Min(1.1f, W * 0.6f) * 0.39f, Vector2.zero, Vector2.one);
        // stock: columns of one product each; bags and boxes lie flat with the picture up
        var list = Pick(cats);
        float cellU = 0.245f, innerD = D - wall * 2 - 0.04f;
        int nu = Mathf.FloorToInt((W - wall * 2 - 0.04f) / cellU);
        float u0 = wall + (W - wall * 2 - nu * cellU) * 0.5f;
        for (int iu = 0; iu < nu && list.Count > 0; iu++)
        {
            Product p = list[(iu * 3 + rnd.Next(3)) % list.Count];
            bool bag = p.shape == "bag";
            float span = bag ? p.h : (p.shape == "lathe" ? p.w : Mathf.Max(p.d, 0.14f));
            int nd = Mathf.Max(1, Mathf.FloorToInt(innerD / (span + 0.012f)));
            for (int id = 0; id < nd; id++)
            {
                if (R() < 0.1f) continue;
                Vector3 c = C(u0 + cellU * (iu + 0.5f), deck, wall + 0.02f + (innerD / nd) * (id + 0.5f));
                if (bag)
                {
                    int n = rnd.Next(2, 5);
                    for (int s = 0; s < n; s++)
                    {
                        Vector3 dir = Quaternion.Euler(0, R(-9f, 9f) + (R() < 0.5f ? 180f : 0f), 0) * face;
                        prod.Append(Local(p, s == n - 1 ? 0 : 1), c - dir * (p.h * 0.5f) + Vector3.up * (p.d * 0.5f + s * p.d * 0.62f) + along * R(-0.012f, 0.012f), Quaternion.LookRotation(Vector3.up, dir));
                    }
                }
                else
                {
                    int n = Mathf.Clamp(Mathf.FloorToInt(0.32f / p.h), 1, 3);
                    if (n > 1 && R() < 0.4f) n--;
                    for (int s = 0; s < n; s++)
                        Place(prod, p, c + Vector3.up * (s * p.h) + along * R(-0.01f, 0.01f), Quaternion.Euler(0, R(-10f, 10f), 0) * face, s == n - 1 ? 0 : 1);
                }
            }
        }
        // lids: two panes, one slid half over the other
        glass.Face(C(W * 0.28f, H + 0.05f, D * 0.5f), Vector3.up, face, W * 0.5f, D - 0.08f, Vector2.zero, Vector2.one);
        glass.Face(C(W * 0.66f, H + 0.065f, D * 0.5f), Vector3.up, face, W * 0.5f, D - 0.08f, new Vector2(0.3f, 0.2f), new Vector2(1.3f, 1.2f));
        white.Emit(name + " Body", go.transform, Body("fit_fridge_white"));
        blue.Emit(name + " Frame", go.transform, Body("fit_fridge_blue"));
        inner.Emit(name + " Floor", go.transform, MWhiteIn, false);
        grille.Emit(name + " Grille", go.transform, MGrille, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
        sticker.Emit(name + " Sticker", go.transform, Mat("SM_PanelPolar", "panel_polar.png", Color.white, 0.4f, false, false), false);
        glass.Emit(name + " Lids", go.transform, MFrost, false);
    }

    static Rect Skin(int i) => new Rect((i % 4) * 0.25f + 0.01f, 0.75f - (i / 4) * 0.25f + 0.01f, 0.23f, 0.23f);

    /// <summary>A plastic crate of one kind of fruit / vegetable. kind = index into the produce atlas.</summary>
    static void Crate(Acc crate, Acc inside, Acc prod, Vector3 c, Vector3 face, Vector3 along, int kind, float w = 0.54f, float h = 0.15f, float d = 0.36f)
    {
        Vector2 a0 = Vector2.zero, a1 = new Vector2(2f, 1f);
        crate.Face2(c + face * d * 0.5f + Vector3.up * h * 0.5f, face, Vector3.up, w, h, a0, a1);
        crate.Face2(c - face * d * 0.5f + Vector3.up * h * 0.5f, -face, Vector3.up, w, h, a0, a1);
        crate.Face2(c + along * w * 0.5f + Vector3.up * h * 0.5f, along, Vector3.up, d, h, a0, new Vector2(1.4f, 1f));
        crate.Face2(c - along * w * 0.5f + Vector3.up * h * 0.5f, -along, Vector3.up, d, h, a0, new Vector2(1.4f, 1f));
        inside.Face(c + Vector3.up * 0.02f, Vector3.up, face, w - 0.01f, d - 0.01f, new Vector2(0.5f, 0.5f), new Vector2(0.51f, 0.51f));
        Rect uv = Skin(kind);
        // sizes per kind: radius, elongation, count
        float r = 0.042f, lenK = 1f; int layers = 3;
        switch (kind)
        {
            case 3: r = 0.045f; lenK = 1.35f; break;                      // potato
            case 6: r = 0.022f; lenK = 4.2f; break;                       // cucumber
            case 8: r = 0.02f; lenK = 4.5f; break;                        // banana
            case 9: r = 0.035f; lenK = 2.6f; break;                       // eggplant
            case 10: r = 0.045f; lenK = 1.2f; break;                      // pepper
            case 11: r = 0.017f; lenK = 5f; break;                        // carrot
            case 12: r = 0.085f; lenK = 1f; layers = 1; break;            // cabbage
            case 13: r = 0.028f; break;                                   // garlic
            case 14: r = 0.045f; lenK = 1.35f; break;                     // mango
            case 5: r = 0.034f; lenK = 1.25f; break;                      // lemon
        }
        float stepU = r * lenK * 2f + 0.006f, stepD = r * 2f + 0.006f;
        for (int L = 0; L < layers; L++)
        {
            float y = c.y + 0.03f + r * 0.95f + L * r * 1.5f;
            float inset = L >= 2 ? stepU * 0.9f : 0f, insetD = L >= 2 ? stepD * 0.7f : 0f;   // the top layer is a mound in the middle
            for (float u = -w * 0.5f + stepU * 0.5f + 0.015f + L * stepU * 0.4f + inset; u < w * 0.5f - stepU * 0.5f - inset; u += stepU)
                for (float dd = -d * 0.5f + stepD * 0.5f + 0.015f + L * stepD * 0.4f + insetD; dd < d * 0.5f - stepD * 0.5f - insetD; dd += stepD)
                {
                    if (L == layers - 1 && layers > 1 && R() < 0.25f) continue;
                    Vector3 p = new Vector3(c.x, y, c.z) + along * (u + R(-0.006f, 0.006f)) + face * (dd + R(-0.006f, 0.006f));
                    float k = R(0.88f, 1.1f);
                    var rot = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(R(-12f, 12f), R(-18f, 18f), R(0f, 360f));
                    prod.Ellipsoid(p, new Vector3(r * k, r * k * (lenK > 1.5f ? 1f : 0.92f), r * k * lenK), rot, 6, uv);
                }
        }
    }

    /// <summary>White steel produce rack against a wall: three stepped tiers of crates, melons and cabbages on top, sacks at the foot.</summary>
    public static void ProduceRack(Transform parent, string name, Vector3 wallLeft, Vector3 along, Vector3 face, float W)
    {
        wallLeft.y = 0f;
        float D = 0.68f, H = 1.62f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        System.Func<float, float, float, Vector3> C = (u, y, d) => wallLeft + along * u + face * d + Vector3.up * (FloorY + y);
        var go = Holder(name, parent, new Bounds(C(W * 0.5f, H * 0.5f, D * 0.5f), new Vector3(alongX ? W : D, H, alongX ? D : W)));
        var metal = new Acc(); var up = new Acc(); var price = new Acc(); var prod = new Acc(); var stock = new Acc();
        var crates = new[] { new Acc(), new Acc(), new Acc() }; var inside = new Acc();
        float[] ty = { 0.22f, 0.66f, 1.08f, 1.46f }; float[] td = { 0.68f, 0.56f, 0.46f, 0.36f };
        int bays = Mathf.Max(1, Mathf.RoundToInt(W / 1.05f));
        for (int b = 0; b <= bays; b++)
        {
            up.BoxM(C(W * b / bays, H * 0.5f, 0.03f), new Vector3(0.045f, H, 0.045f), face, 0.6f);
            up.BoxM(C(W * b / bays, ty[0] * 0.5f, td[0] - 0.03f), new Vector3(0.045f, ty[0], 0.045f), face, 0.6f);
        }
        for (int t = 0; t < ty.Length; t++)
        {
            metal.BoxM(C(W * 0.5f, ty[t] - 0.012f, td[t] * 0.5f), new Vector3(W, 0.024f, td[t]), face, 1.1f, true);
            metal.BoxM(C(W * 0.5f, ty[t] + 0.02f, td[t] - 0.006f), new Vector3(W, 0.06f, 0.012f), face, 1.1f);
            price.Face(C(W * 0.5f, ty[t] + 0.02f, td[t] + 0.002f), face, Vector3.up, W, 0.042f, new Vector2(R(), 0), new Vector2(R() + W / 1.5f, 1));
        }
        int[] kinds = { 0, 1, 2, 3, 4, 5, 6, 9, 10, 11, 13, 14, 15, 8, 2, 0, 5, 1 };
        int ki = 0;
        for (int t = 0; t < 3; t++)
        {
            float cw = 0.54f, gap = 0.035f;
            int n = Mathf.FloorToInt((W - 0.08f) / (cw + gap));
            float startU = (W - n * (cw + gap) + gap) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                int kind = kinds[(ki++) % kinds.Length];
                Vector3 cc = C(startU + cw * 0.5f + i * (cw + gap), ty[t] + 0.002f, td[t] - 0.2f);
                Crate(crates[(i + t) % 3], inside, prod, cc, face, along, kind);
            }
        }
        // top tier: watermelons and cabbages straight on the shelf
        for (float u = 0.2f; u < W - 0.2f; u += 0.34f)
        {
            bool melon = ((int)(u * 10)) % 3 != 0;
            Vector3 p = C(u + R(-0.03f, 0.03f), ty[3] + (melon ? 0.125f : 0.1f), td[3] * 0.5f);
            if (melon) prod.Ellipsoid(p, new Vector3(0.125f, 0.125f, 0.155f), Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0, R(-25f, 25f), R(0f, 360f)), 10, Skin(7));
            else prod.Ellipsoid(p, new Vector3(0.1f, 0.095f, 0.1f), Quaternion.Euler(R(-20f, 20f), R(0f, 360f), 0), 9, Skin(12));
        }
        // sacks of onions and potatoes leaning at the foot, under the lowest tier's front edge
        var sacks = Pick(new[] { "produce_sack" });
        for (int i = 0; i < sacks.Count && i < 3; i++)
        {
            Product s = sacks[i % sacks.Count];
            Vector3 p = C(0.35f + i * 0.4f, 0f, td[0] + 0.16f);
            if (W > 2.5f) Place(stock, s, p, face, 0, R(-8f, 8f), -14f, R(-3f, 3f));
        }
        metal.Emit(name + " Shelves", go.transform, MMetal);
        up.Emit(name + " Uprights", go.transform, MUpright, false);
        price.Emit(name + " Prices", go.transform, MPrice, false);
        string[] cn = { "crate_green", "crate_black", "crate_blue" };
        for (int i = 0; i < 3; i++) crates[i].Emit(name + " Crates " + i, go.transform, Mat("SM_" + cn[i], cn[i] + ".png", Color.white, 0.35f, false, true), false);
        inside.Emit(name + " Crate Floors", go.transform, MDark, false);
        prod.Emit(name + " Produce", go.transform, MProduce, false);
        stock.Emit(name + " Sacks", go.transform, MProducts, false);
    }

    /// <summary>Narrow shelves hung on the front of the counter, full of sweets in their display boxes.</summary>
    public static void CounterSweets(Transform parent, string name, Vector3 a, Vector3 b, Vector3 face, float[] heights)
    {
        a.y = 0; b.y = 0;
        Vector3 along = (b - a).normalized; float len = (b - a).magnitude;
        const float depth = 0.15f;
        float top = 0f; foreach (float hh in heights) top = Mathf.Max(top, hh + 0.14f);
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        var go = Holder(name, parent, new Bounds((a + b) * 0.5f + face * depth * 0.5f + Vector3.up * (FloorY + top * 0.5f), new Vector3(alongX ? len : depth, top, alongX ? depth : len)), false);
        fixtureBounds.RemoveAt(fixtureBounds.Count - 1); fixtureNames.RemoveAt(fixtureNames.Count - 1);   // part of the counter, not a fitting of its own
        var metal = new Acc(); var price = new Acc(); var prod = new Acc();
        foreach (float hgt in heights)
        {
            Vector3 mid = (a + b) * 0.5f + face * depth * 0.5f + Vector3.up * (FloorY + hgt);
            metal.BoxM(mid + Vector3.down * 0.009f, new Vector3(len, 0.018f, depth), face, 1.1f, true);
            price.Face(mid + face * (depth * 0.5f + 0.002f) + Vector3.down * 0.02f, face, Vector3.up, len, 0.036f, new Vector2(R(), 0), new Vector2(R() + len / 1.5f, 1));
            FillShelf(prod, a + face * (depth - 0.01f) + Vector3.up * (FloorY + hgt) + along * 0.02f, along, face, len - 0.04f, depth - 0.02f, 0.2f, new[] { "candy" }, 1, 0.03f, 0f, 1);
        }
        metal.Emit(name + " Shelves", go.transform, MMetal, false);
        price.Emit(name + " Prices", go.transform, MPrice, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    /// <summary>A pile of one product on the floor (water packs, cartons): cols x rows x layers, slightly untidy.</summary>
    public static void FloorStack(Transform parent, string name, Vector3 corner, Vector3 along, Vector3 face, string[] ids, int cols, int rows, int layers, bool obstacle = true, float baseY = 0f)
    {
        corner.y = 0;
        var list = Pick(ids);
        if (list.Count == 0) return;
        Product p0 = list[0];
        float W = cols * (p0.w + 0.01f), Dp = rows * (p0.d + 0.01f), H = layers * p0.h;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        Vector3 centre = corner + along * W * 0.5f + face * Dp * 0.5f + Vector3.up * (FloorY + baseY + H * 0.5f);
        var go = Holder(name, parent, new Bounds(centre, new Vector3(alongX ? W : Dp, H, alongX ? Dp : W)), obstacle);
        var prod = new Acc();
        for (int L = 0; L < layers; L++)
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    if (L == layers - 1 && layers > 1 && R() < 0.35f) continue;
                    Product p = list[(L + c + r) % list.Count];
                    Vector3 pos = corner + along * ((c + 0.5f) * (p0.w + 0.01f)) + face * ((r + 0.5f) * (p0.d + 0.01f)) + Vector3.up * (FloorY + baseY + L * p0.h);
                    Place(prod, p, pos, face, 0, R(-4f, 4f));
                }
        prod.Emit(name + " Stock", go.transform, MProducts);
    }

    /// <summary>Single products standing on the floor (water-cooler bottles, sacks).</summary>
    public static void FloorItems(Transform parent, string name, Vector3[] spots, Vector3 face, string[] ids, float lean = 0f)
    {
        var list = Pick(ids);
        if (list.Count == 0 || spots.Length == 0) return;
        var bb = new Bounds(new Vector3(spots[0].x, FloorY + list[0].h * 0.5f, spots[0].z), Vector3.zero);
        foreach (var s in spots)
        {
            bb.Encapsulate(new Vector3(s.x - list[0].w * 0.5f, FloorY, s.z - list[0].w * 0.5f));
            bb.Encapsulate(new Vector3(s.x + list[0].w * 0.5f, FloorY + list[0].h, s.z + list[0].w * 0.5f));
        }
        var go = Holder(name, parent, bb);
        var prod = new Acc();
        for (int i = 0; i < spots.Length; i++)
            Place(prod, list[i % list.Count], new Vector3(spots[i].x, FloorY, spots[i].z), Quaternion.Euler(0, R(-30f, 30f), 0) * face, 0, 0f, -lean);
        prod.Emit(name + " Stock", go.transform, MProducts);
    }

    public static void Init(int seed)
    {
        rnd = new System.Random(seed);
        fixtureBounds.Clear(); fixtureNames.Clear();
        triCount = 0;
        LoadProducts();
    }
}
