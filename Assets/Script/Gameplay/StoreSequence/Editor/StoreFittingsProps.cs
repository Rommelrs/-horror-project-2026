using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The props of the store makeover that replace the old downloaded models: vending machines, cash register, counter
/// devices, baskets and trolley, floor clutter, cleaning gear, the fuse box shell. Same rules as the fittings: built from
/// code into combined meshes, made-up brands, textures from Textures/StoreMakeover.
/// </summary>
public static partial class StoreFittings
{
    // ------------------------------------------------------------------ small mesh helpers

    static void TriN(Acc a, int i0, int i1, int i2, Vector3 nrm)
    {
        if (Vector3.Dot(Vector3.Cross(a.v[i1] - a.v[i0], a.v[i2] - a.v[i0]), nrm) >= 0f) { a.t.Add(i0); a.t.Add(i1); a.t.Add(i2); }
        else { a.t.Add(i0); a.t.Add(i2); a.t.Add(i1); }
    }

    /// <summary>Cylinder / cone between two points.</summary>
    static void Cyl(Acc a, Vector3 p0, Vector3 p1, float r0, float r1, int seg, Rect uv, bool cap0 = true, bool cap1 = true)
    {
        Vector3 axis = (p1 - p0).normalized;
        Vector3 t1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 t2 = Vector3.Cross(axis, t1);
        int s = a.v.Count;
        for (int i = 0; i <= seg; i++)
        {
            float ang = i / (float)seg * Mathf.PI * 2f;
            Vector3 dir = t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang);
            a.v.Add(p0 + dir * r0); a.n.Add(dir); a.uv.Add(new Vector2(uv.xMin + uv.width * i / seg, uv.yMin));
            a.v.Add(p1 + dir * r1); a.n.Add(dir); a.uv.Add(new Vector2(uv.xMin + uv.width * i / seg, uv.yMax));
        }
        for (int i = 0; i < seg; i++)
        {
            int k = s + i * 2;
            Vector3 nn = a.n[k] + a.n[k + 2];
            TriN(a, k, k + 1, k + 3, nn); TriN(a, k, k + 3, k + 2, nn);
        }
        for (int e = 0; e < 2; e++)
        {
            if (e == 0 ? !cap0 : !cap1) continue;
            Vector3 c = e == 0 ? p0 : p1; float r = e == 0 ? r0 : r1; Vector3 nn = e == 0 ? -axis : axis;
            if (r < 0.0005f) continue;
            int ci = a.v.Count; a.v.Add(c); a.n.Add(nn); a.uv.Add(uv.center);
            for (int i = 0; i <= seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2f;
                a.v.Add(c + (t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang)) * r); a.n.Add(nn); a.uv.Add(uv.center);
            }
            for (int i = 0; i < seg; i++) TriN(a, ci, ci + 1 + i, ci + 2 + i, nn);
        }
    }

    /// <summary>Box with any rotation. uv: tiling in metres ('mpt') or, when 'r' is given, the same rect on every face.</summary>
    static void OBox(Acc a, Vector3 c, Vector3 size, Quaternion rot, float mpt = 1f, Rect? r = null, bool bottom = true)
    {
        Vector3 X = rot * Vector3.right, Y = rot * Vector3.up, Z = rot * Vector3.forward;
        float w = size.x, h = size.y, d = size.z;
        void F(Vector3 cc, Vector3 n, Vector3 up, float fw, float fh)
        {
            if (r.HasValue) a.Face(cc, n, up, fw, fh, r.Value.min, r.Value.max);
            else a.FaceM(cc, n, up, fw, fh, mpt, (cc.x * 0.37f + cc.z * 0.53f) % 1f, (cc.y * 0.71f) % 1f);
        }
        F(c + Z * d * 0.5f, Z, Y, w, h); F(c - Z * d * 0.5f, -Z, Y, w, h);
        F(c + X * w * 0.5f, X, Y, d, h); F(c - X * w * 0.5f, -X, Y, d, h);
        F(c + Y * h * 0.5f, Y, Z, w, d);
        if (bottom) F(c - Y * h * 0.5f, -Y, Z, w, d);
    }

    /// <summary>Quad seen from both sides.</summary>
    static void Quad2(Acc a, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector2 uv0, Vector2 uv1)
    {
        Vector3 n = Vector3.Cross(tl - bl, br - bl).normalized;
        a.Quad(bl, tl, tr, br, n, uv0, uv1);
        a.Quad(br, tr, tl, bl, -n, new Vector2(uv1.x, uv0.y), new Vector2(uv0.x, uv1.y));
    }

    /// <summary>A prop's parent object: optional solid box, optional nav obstacle. Not a "fitting" (pickups are never seated on it).</summary>
    static GameObject PropHolder(string name, Transform parent, Bounds solid, bool collider, bool obstacle = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = solid.center + off;
        if (collider) { var bc = go.AddComponent<BoxCollider>(); bc.size = solid.size; }
        if (obstacle) { var ob = go.AddComponent<NavMeshObstacle>(); ob.shape = NavMeshObstacleShape.Box; ob.size = solid.size; ob.carving = true; }
        return go;
    }

    static Material Flat(string name, Color c, float gloss = 0.3f) => Mat(name, null, c, gloss, false, false);
    static Material Panel(string tex, float gloss = 0.3f) => Mat("SM_" + tex, tex + ".png", Color.white, gloss, false, false);
    static Material MVendBlack => Mat("SM_VendBlack", "fit_vend_black.png", Color.white, 0.35f, false, false);
    static Material MPlasticDark => Flat("SM_PlasticCharcoal", new Color(0.13f, 0.13f, 0.15f), 0.4f);
    static Material MPlasticCream => Flat("SM_PlasticCreamOld", new Color(0.82f, 0.79f, 0.7f), 0.35f);
    static Material MBasketRed => Mat("SM_BasketRed", "crate_basket_red.png", Color.white, 0.4f, false, true);
    static Material MBasketBlue => Mat("SM_BasketBlue", "crate_basket_blue.png", Color.white, 0.4f, false, true);
    static readonly Rect One = new Rect(0, 0, 1, 1);

    // ------------------------------------------------------------------ vending machines

    /// <summary>Glass-front snack and drink machine: black cabinet, spiral trays of real stock, keypad column, delivery flap.</summary>
    public static void VendingSnack(Transform parent, string name, Vector3 wallLeft, Vector3 along, Vector3 face)
    {
        wallLeft.y = 0f;
        const float W = 1.0f, D = 0.8f, H = 1.83f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        System.Func<float, float, float, Vector3> C = (u, y, d) => wallLeft + along * u + face * d + Vector3.up * (FloorY + y);
        var go = Holder(name, parent, new Bounds(C(W * 0.5f, H * 0.5f, D * 0.5f), new Vector3(alongX ? W : D, H, alongX ? D : W)));
        var body = new Acc(); var inner = new Acc(); var tray = new Acc(); var prod = new Acc(); var coil = new Acc(); var price = new Acc();
        var glass = new Acc(); var ctrl = new Acc(); var flap = new Acc(); var top = new Acc();
        // cabinet
        body.BoxM(C(0.02f, H * 0.5f, D * 0.5f), new Vector3(D, H, 0.04f), along, 1f);
        body.BoxM(C(W - 0.02f, H * 0.5f, D * 0.5f), new Vector3(D, H, 0.04f), along, 1f);
        body.BoxM(C(W * 0.5f, H * 0.5f, 0.02f), new Vector3(W, H, 0.04f), face, 1f);
        body.BoxM(C(W * 0.5f, H - 0.02f, D * 0.5f), new Vector3(W, 0.04f, D), face, 1f);
        body.BoxM(C(W * 0.5f, 0.05f, D * 0.5f - 0.02f), new Vector3(W - 0.06f, 0.1f, D - 0.08f), face, 1f);
        // door: lower panel with the flap, the control column, the brand strip, the window post
        body.BoxM(C(W * 0.5f, 0.3f, D - 0.03f), new Vector3(W, 0.4f, 0.06f), face, 1f, true);
        body.BoxM(C(0.87f, 1.1f, D - 0.03f), new Vector3(0.26f, 1.2f, 0.06f), face, 1f);
        body.BoxM(C(W * 0.5f, 1.765f, D - 0.03f), new Vector3(W, 0.13f, 0.06f), face, 1f);
        body.BoxM(C(0.035f, 1.1f, D - 0.03f), new Vector3(0.07f, 1.2f, 0.06f), face, 1f);
        flap.Face(C(W * 0.5f, 0.3f, D + 0.002f), face, Vector3.up, 0.92f, 0.345f, Vector2.zero, Vector2.one);
        ctrl.Face(C(0.87f, 1.1f, D + 0.002f), face, Vector3.up, 0.23f, 1.15f, Vector2.zero, Vector2.one);
        top.Face(C(W * 0.5f, 1.765f, D + 0.002f), face, Vector3.up, 0.96f, 0.12f, Vector2.zero, Vector2.one);
        // inside
        inner.FaceM(C(0.405f, 1.1f, 0.045f), face, Vector3.up, 0.67f, 1.2f, 0.9f);
        inner.FaceM(C(0.072f, 1.1f, D * 0.5f), along, Vector3.up, D - 0.1f, 1.2f, 0.9f);
        inner.FaceM(C(0.738f, 1.1f, D * 0.5f), -along, Vector3.up, D - 0.1f, 1.2f, 0.9f);
        inner.FaceM(C(0.405f, 0.502f, D * 0.5f), Vector3.up, face, 0.67f, D - 0.1f, 0.9f);
        float[] ys = { 0.54f, 0.695f, 0.93f, 1.13f, 1.415f };
        string[][] plan = { new[] { "soda_can", "juice_small" }, new[] { "malt", "water_small", "energy", "juice_can" }, new[] { "candy", "biscuits", "choc_box" }, new[] { "chips", "nuts" }, new[] { "chips" } };
        for (int i = 0; i < ys.Length; i++)
        {
            float clear = (i + 1 < ys.Length ? ys[i + 1] : 1.7f) - ys[i] - 0.02f;
            tray.BoxM(C(0.405f, ys[i] - 0.008f, 0.37f), new Vector3(0.66f, 0.016f, 0.56f), face, 1f, true);
            price.Face(C(0.405f, ys[i] - 0.004f, 0.652f), face, Vector3.up, 0.66f, 0.034f, new Vector2(R(), 0f), new Vector2(R() + 0.5f, 1f));
            coil.Face2(C(0.405f, ys[i] + 0.055f, 0.636f), face, Vector3.up, 0.66f, 0.11f, Vector2.zero, new Vector2(6f, 1f));
            FillShelf(prod, C(0.085f, ys[i], 0.62f), along, face, 0.64f, 0.5f, clear, plan[i], 3, 0.14f, 4f, 1);
        }
        glass.Face(C(0.405f, 1.1f, D - 0.012f), face, Vector3.up, 0.67f, 1.2f, Vector2.zero, Vector2.one);
        body.Emit(name + " Cabinet", go.transform, MVendBlack);
        inner.Emit(name + " Interior", go.transform, MSteel, false);
        tray.Emit(name + " Trays", go.transform, MDark, false);
        price.Emit(name + " Prices", go.transform, MPrice, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
        coil.Emit(name + " Coils", go.transform, Mat("SM_VendCoil", "decal_coil.png", Color.white, 0.7f, false, true), false);
        ctrl.Emit(name + " Controls", go.transform, Panel("panel_vend_ctrl", 0.35f), false);
        flap.Emit(name + " Flap", go.transform, Panel("panel_vend_flap", 0.35f), false);
        top.Emit(name + " Brand", go.transform, Panel("panel_vend_top", 0.35f), false);
        glass.Emit(name + " Glass", go.transform, MGlass, false);
    }

    /// <summary>Closed-front canned-drinks machine with one big printed door (Sahra Cola).</summary>
    public static void VendingCola(Transform parent, string name, Vector3 wallLeft, Vector3 along, Vector3 face)
    {
        wallLeft.y = 0f;
        const float W = 1.0f, D = 0.72f, H = 1.83f;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        System.Func<float, float, float, Vector3> C = (u, y, d) => wallLeft + along * u + face * d + Vector3.up * (FloorY + y);
        var go = Holder(name, parent, new Bounds(C(W * 0.5f, H * 0.5f, D * 0.5f), new Vector3(alongX ? W : D, H, alongX ? D : W)));
        var body = new Acc(); var front = new Acc(); var dark = new Acc();
        body.BoxM(C(W * 0.5f, H * 0.5f + 0.04f, D * 0.5f - 0.02f), new Vector3(W, H - 0.08f, D - 0.04f), face, 1.2f);
        dark.BoxM(C(W * 0.5f, 0.04f, D * 0.5f - 0.03f), new Vector3(W - 0.06f, 0.08f, D - 0.1f), face, 1f);
        dark.BoxM(C(W * 0.5f, H - 0.015f, D * 0.5f), new Vector3(W + 0.01f, 0.03f, D + 0.01f), face, 1f);
        // the door stands a little proud of the cabinet
        body.BoxM(C(W * 0.5f, H * 0.5f + 0.04f, D - 0.02f), new Vector3(W - 0.02f, H - 0.1f, 0.04f), face, 1.2f);
        front.Face(C(W * 0.5f, H * 0.5f + 0.035f, D + 0.002f), face, Vector3.up, W - 0.03f, H - 0.11f, Vector2.zero, Vector2.one);
        body.Emit(name + " Cabinet", go.transform, Body("fit_fridge_blue"));
        dark.Emit(name + " Trim", go.transform, MVendBlack, false);
        front.Emit(name + " Door", go.transform, Panel("panel_vend_cola", 0.4f), false);
    }

    // ------------------------------------------------------------------ counter: cash register and devices

    /// <summary>Electronic cash register on the counter (keys toward 'toCashier'). Returns the display renderer (it lights up with the power).</summary>
    public static Renderer CashRegister(Transform parent, string name, Vector3 pos, Vector3 toCashier)
    {
        Vector3 f = toCashier.normalized, r = Vector3.Cross(Vector3.up, -f), u = Vector3.up;
        var go = PropHolder(name, parent, new Bounds(pos + u * 0.16f, new Vector3(0.36f, 0.32f, 0.4f)), false);
        var body = new Acc(); var keys = new Acc(); var drawer = new Acc(); var screen = new Acc();
        Quaternion rot = Quaternion.LookRotation(f, u);
        const float w = 0.36f, d = 0.4f, hb = 0.1f;
        OBox(body, pos + u * hb * 0.5f, new Vector3(w, hb, d), rot);
        drawer.Face(pos + u * (hb * 0.5f) + f * (d * 0.5f + 0.002f), f, u, w - 0.02f, hb - 0.014f, Vector2.zero, Vector2.one);
        // the keyboard deck: a wedge rising toward the back
        float yF = hb + 0.03f, yB = hb + 0.125f, zf = d * 0.5f - 0.02f, zb = -d * 0.5f + 0.02f, hw = w * 0.5f - 0.01f;
        Vector3 P(float x, float y, float z) => pos + r * x + u * y + f * z;
        keys.Quad(P(-hw, yF, zf), P(-hw, yB, zb), P(hw, yB, zb), P(hw, yF, zf), (u * 0.97f + f * 0.24f).normalized, Vector2.zero, Vector2.one);
        body.Quad(P(-hw, hb, zf), P(-hw, yF, zf), P(hw, yF, zf), P(hw, hb, zf), f, Vector2.zero, new Vector2(0.3f, 0.03f));
        body.Quad(P(hw, hb, zb), P(hw, yB, zb), P(-hw, yB, zb), P(-hw, hb, zb), -f, Vector2.zero, new Vector2(0.3f, 0.1f));
        body.Quad(P(hw, hb, zf), P(hw, yF, zf), P(hw, yB, zb), P(hw, hb, zb), r, Vector2.zero, new Vector2(0.3f, 0.1f));
        body.Quad(P(-hw, hb, zb), P(-hw, yB, zb), P(-hw, yF, zf), P(-hw, hb, zf), -r, Vector2.zero, new Vector2(0.3f, 0.1f));
        // display on a short neck at the back, readable from both sides
        Vector3 dc = P(0.04f, yB + 0.085f, zb + 0.035f);
        OBox(body, P(0.04f, yB + 0.03f, zb + 0.035f), new Vector3(0.05f, 0.06f, 0.03f), rot);
        OBox(body, dc, new Vector3(0.24f, 0.085f, 0.034f), rot);
        screen.Face(dc + f * 0.018f, f, u, 0.225f, 0.07f, Vector2.zero, Vector2.one);
        screen.Face(dc - f * 0.018f, -f, u, 0.225f, 0.07f, Vector2.zero, Vector2.one);
        body.Emit(name + " Body", go.transform, MPlasticDark);
        keys.Emit(name + " Keys", go.transform, Panel("panel_register_top", 0.3f), false);
        drawer.Emit(name + " Drawer", go.transform, Panel("panel_register_drawer", 0.3f), false);
        var sm = Mat("SM_RegisterScreen", "panel_register_display.png", new Color(0.35f, 0.35f, 0.35f), 0.5f, false, false);
        sm.EnableKeyword("_EMISSION");
        sm.SetTexture("_EmissionMap", sm.mainTexture);
        sm.SetColor("_EmissionColor", Color.black);
        sm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(sm);
        return screen.Emit(name + " Display", go.transform, sm, false).GetComponent<Renderer>();
    }

    /// <summary>Desk phone, card terminal, calculator and an ashtray lying on the counter top (top at 'y').</summary>
    public static void CounterDevices(Transform parent, string name, Vector3 phonePos, Vector3 cardPos, Vector3 calcPos, Vector3 ashPos, Vector3 toCashier)
    {
        Vector3 f = toCashier.normalized, u = Vector3.up;
        var go = PropHolder(name, parent, new Bounds(phonePos, Vector3.one * 0.3f), false);
        var cream = new Acc(); var dark = new Acc(); var faces = new Acc(); var glassA = new Acc(); var ash = new Acc();
        void Device(Acc bodyAcc, Vector3 p, float w, float h, float d, float yaw, float tilt, Rect uv)
        {
            Quaternion rot = Quaternion.LookRotation(Quaternion.Euler(0, yaw, 0) * f, u) * Quaternion.Euler(-tilt, 0, 0);
            Vector3 c = p + u * (h * 0.5f + Mathf.Sin(tilt * Mathf.Deg2Rad) * d * 0.5f);
            OBox(bodyAcc, c, new Vector3(w, h, d), rot);
            // printed top: bottom of the picture toward the cashier
            Vector3 X = rot * Vector3.right, Y = rot * Vector3.up, Z = rot * Vector3.forward;
            Vector3 tc = c + Y * (h * 0.5f + 0.0015f);
            faces.Quad(tc - X * w * 0.48f + Z * d * 0.48f, tc - X * w * 0.48f - Z * d * 0.48f, tc + X * w * 0.48f - Z * d * 0.48f, tc + X * w * 0.48f + Z * d * 0.48f, Y, uv.min, uv.max);
        }
        Device(cream, phonePos, 0.19f, 0.05f, 0.23f, 12f, 8f, new Rect(0f, 0f, 0.5f, 1f));
        // handset on its cradle (the left third of the phone), cord to the body
        {
            Quaternion rot = Quaternion.LookRotation(Quaternion.Euler(0, 12f, 0) * f, u) * Quaternion.Euler(-8f, 0, 0);
            Vector3 hc = phonePos + rot * new Vector3(-0.055f, 0.085f, 0.0f);
            OBox(cream, hc, new Vector3(0.05f, 0.03f, 0.2f), rot);
            OBox(cream, hc + rot * new Vector3(0, -0.012f, 0.078f), new Vector3(0.056f, 0.036f, 0.05f), rot);
            OBox(cream, hc + rot * new Vector3(0, -0.012f, -0.078f), new Vector3(0.056f, 0.036f, 0.05f), rot);
        }
        Device(dark, cardPos, 0.085f, 0.045f, 0.18f, -8f, 14f, new Rect(0.5f, 0f, 0.25f, 1f));
        Device(dark, calcPos, 0.105f, 0.014f, 0.15f, 20f, 0f, new Rect(0.75f, 0f, 0.25f, 1f));
        // heavy glass ashtray with a few stubs
        Cyl(glassA, ashPos, ashPos + u * 0.032f, 0.058f, 0.064f, 10, One, true, false);
        Cyl(ash, ashPos + u * 0.02f, ashPos + u * 0.0205f, 0.05f, 0.05f, 10, One, false, true);
        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = Quaternion.Euler(0, 40f + i * 110f, 0) * Vector3.forward;
            Cyl(cream, ashPos + u * 0.026f + dir * 0.012f, ashPos + u * 0.03f + dir * 0.045f, 0.004f, 0.004f, 5, One);
        }
        cream.Emit(name + " Cream", go.transform, MPlasticCream);
        dark.Emit(name + " Dark", go.transform, MPlasticDark);
        faces.Emit(name + " Faces", go.transform, Panel("panel_devices", 0.3f), false);
        glassA.Emit(name + " Ashtray", go.transform, Flat("SM_AshtrayGlass", new Color(0.42f, 0.5f, 0.52f), 0.9f), false);
        ash.Emit(name + " Ash", go.transform, Flat("SM_Ash", new Color(0.2f, 0.19f, 0.18f), 0.05f), false);
    }

    // ------------------------------------------------------------------ baskets, trolley

    static void BasketMesh(Acc plastic, Acc dark, Vector3 p, Quaternion rot, bool handlesUp)
    {
        const float wt = 0.44f, dt = 0.31f, wb = 0.37f, db = 0.25f, h = 0.22f;
        Vector3 V(float x, float y, float z) => p + rot * new Vector3(x, y, z);
        Vector3[] b = { V(-wb / 2, 0, -db / 2), V(wb / 2, 0, -db / 2), V(wb / 2, 0, db / 2), V(-wb / 2, 0, db / 2) };
        Vector3[] t = { V(-wt / 2, h, -dt / 2), V(wt / 2, h, -dt / 2), V(wt / 2, h, dt / 2), V(-wt / 2, h, dt / 2) };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Quad2(plastic, b[i], t[i], t[j], b[j], new Vector2(0f, 0f), new Vector2(i % 2 == 0 ? 1f : 0.7f, 1f));
        }
        Quad2(plastic, b[0], b[3], b[2], b[1], new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f));   // solid bottom (the rim colour)
        // two wire handles: folded down on the rim, or standing up
        foreach (int s in new[] { -1, 1 })
        {
            if (handlesUp)
            {
                Cyl(dark, V(-0.15f, h, s * 0.02f), V(-0.15f, h + 0.16f, s * 0.02f), 0.006f, 0.006f, 5, One);
                Cyl(dark, V(0.15f, h, s * 0.02f), V(0.15f, h + 0.16f, s * 0.02f), 0.006f, 0.006f, 5, One);
                Cyl(dark, V(-0.15f, h + 0.16f, s * 0.02f), V(0.15f, h + 0.16f, s * 0.02f), 0.011f, 0.011f, 6, One);
            }
            else
            {
                Cyl(dark, V(-0.15f, h + 0.008f, s * (dt / 2 - 0.012f)), V(0.15f, h + 0.008f, s * (dt / 2 - 0.012f)), 0.009f, 0.009f, 6, One);
            }
        }
    }

    /// <summary>A nested stack of hand baskets by the door.</summary>
    public static void BasketStack(Transform parent, string name, Vector3 pos, float yaw, int count)
    {
        pos.y = FloorY;
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * (0.11f + count * 0.03f), new Vector3(0.46f, 0.22f + count * 0.06f, 0.34f)), true, true);
        var red = new Acc(); var blue = new Acc(); var dark = new Acc();
        for (int i = 0; i < count; i++)
            BasketMesh(i % 3 == 2 ? blue : red, dark, pos + Vector3.up * (0.004f + i * 0.062f), Quaternion.Euler(0, yaw + R(-4f, 4f), 0), false);
        red.Emit(name + " Red", go.transform, MBasketRed);
        blue.Emit(name + " Blue", go.transform, MBasketBlue);
        dark.Emit(name + " Handles", go.transform, MDark, false);
    }

    /// <summary>One basket on the floor: standing, or dropped on its side.</summary>
    public static void BasketLoose(Transform parent, string name, Vector3 pos, float yaw, bool tipped, string[] contents = null)
    {
        pos.y = FloorY;
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * 0.14f, new Vector3(0.46f, 0.28f, 0.4f)), !tipped);
        var red = new Acc(); var dark = new Acc(); var prod = new Acc();
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        if (tipped) BasketMesh(red, dark, pos + Vector3.up * 0.155f + rot * new Vector3(0, 0, -0.11f), rot * Quaternion.Euler(86f, 0, 0), true);
        else BasketMesh(red, dark, pos + Vector3.up * 0.004f, rot, true);
        if (contents != null && !tipped)
        {
            var list = Pick(contents);
            for (int i = 0; i < list.Count && i < 4; i++)
            {
                Product p = list[i];
                Vector3 c = pos + rot * new Vector3(-0.11f + (i % 2) * 0.2f, 0.012f, -0.05f + (i / 2) * 0.1f);
                if (p.shape == "bag" || p.shape == "sack")
                    prod.Append(Local(p, 0), c + Vector3.up * (p.d * 0.5f + i * 0.02f) - rot * Vector3.forward * (p.h * 0.5f), Quaternion.LookRotation(Vector3.up, rot * Vector3.forward));
                else Place(prod, p, c, rot * Vector3.forward, 0, R(-30f, 30f));
            }
        }
        red.Emit(name + " Basket", go.transform, MBasketRed);
        dark.Emit(name + " Handles", go.transform, MDark, false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    /// <summary>Wire shopping trolley (the nose points along 'yaw').</summary>
    public static void Trolley(Transform parent, string name, Vector3 pos, float yaw, string[] contents = null)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * 0.52f, yawBox(new Vector3(0.56f, 1.04f, 0.98f), yaw)), true, true);
        var wire = new Acc(); var steel = new Acc(); var dark = new Acc(); var red = new Acc(); var prod = new Acc();
        Vector3 V(float x, float y, float z) => pos + rot * new Vector3(x, y, z);
        // basket: tapers toward the nose (+z) and the bottom
        const float y0 = 0.45f, y1 = 0.98f;
        Vector3[] b = { V(-0.2f, y0, -0.36f), V(0.2f, y0, -0.36f), V(0.16f, y0 + 0.06f, 0.4f), V(-0.16f, y0 + 0.06f, 0.4f) };
        Vector3[] t = { V(-0.27f, y1, -0.44f), V(0.27f, y1, -0.44f), V(0.22f, y1 - 0.04f, 0.5f), V(-0.22f, y1 - 0.04f, 0.5f) };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Quad2(wire, b[i], t[i], t[j], b[j], Vector2.zero, new Vector2(i % 2 == 0 ? 2f : 3.2f, 2f));
        }
        Quad2(wire, b[0], b[3], b[2], b[1], Vector2.zero, new Vector2(3f, 1.6f));
        // rim tube, chassis, uprights
        for (int i = 0; i < 4; i++) Cyl(steel, t[i], t[(i + 1) % 4], 0.011f, 0.011f, 5, One);
        foreach (int s in new[] { -1, 1 })
        {
            Cyl(steel, V(s * 0.21f, 0.13f, -0.4f), V(s * 0.17f, 0.13f, 0.42f), 0.013f, 0.013f, 5, One);              // base rail
            Cyl(steel, V(s * 0.21f, 0.13f, -0.4f), V(s * 0.27f, 1.04f, -0.5f), 0.013f, 0.013f, 5, One);              // rear upright up to the handle
            Cyl(steel, V(s * 0.19f, 0.13f, 0.0f), V(s * 0.2f, y0, -0.2f), 0.011f, 0.011f, 5, One);                  // strut under the basket
            foreach (float z in new[] { -0.4f, 0.42f })
            {
                float x = s * (z < 0 ? 0.21f : 0.17f);
                Cyl(dark, V(x - 0.014f, 0.05f, z), V(x + 0.014f, 0.05f, z), 0.05f, 0.05f, 10, One);                  // caster
                Cyl(steel, V(x, 0.05f, z), V(x, 0.13f, z), 0.008f, 0.008f, 4, One);
            }
        }
        Cyl(steel, V(-0.21f, 0.13f, -0.4f), V(0.21f, 0.13f, -0.4f), 0.011f, 0.011f, 5, One);
        Cyl(steel, V(-0.17f, 0.13f, 0.42f), V(0.17f, 0.13f, 0.42f), 0.011f, 0.011f, 5, One);
        Cyl(red, V(-0.27f, 1.04f, -0.5f), V(0.27f, 1.04f, -0.5f), 0.019f, 0.019f, 8, One);                           // handle grip
        OBox(red, V(0f, 0.9f, -0.435f), new Vector3(0.44f, 0.12f, 0.012f), rot * Quaternion.Euler(-8f, 0, 0));      // child-seat flap
        if (contents != null)
        {
            var list = Pick(contents);
            for (int i = 0; i < list.Count && i < 5; i++)
            {
                Product p = list[i];
                Vector3 c = V(-0.08f + (i % 2) * 0.16f, y0 + 0.035f + (i / 2) * 0.012f, -0.24f + (i / 2) * 0.24f);
                if (p.shape == "bag" || p.shape == "sack")
                    prod.Append(Local(p, 0), c + Vector3.up * (p.d * 0.5f) - rot * Vector3.forward * (p.h * 0.5f), Quaternion.LookRotation(Vector3.up, rot * Quaternion.Euler(0, R(-20f, 20f), 0) * Vector3.forward));
                else Place(prod, p, c, rot * Vector3.right, 0, R(-40f, 40f));
            }
        }
        wire.Emit(name + " Wire", go.transform, MBasket, false);
        steel.Emit(name + " Frame", go.transform, MSteel);
        dark.Emit(name + " Wheels", go.transform, MDark, false);
        red.Emit(name + " Handle", go.transform, Flat("SM_PlasticRed", new Color(0.72f, 0.1f, 0.12f), 0.4f), false);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    static Vector3 yawBox(Vector3 size, float yaw)
    {
        float c = Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)), s = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad));
        return new Vector3(size.x * c + size.z * s, size.y, size.x * s + size.z * c);
    }

    // ------------------------------------------------------------------ floor props

    /// <summary>Open cardboard carton with its flaps up, a few products left inside.</summary>
    public static void OpenBox(Transform parent, string name, Vector3 pos, float yaw, float w, float h, float d, string[] contents = null)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * h * 0.5f, yawBox(new Vector3(w, h, d), yaw)), true);
        var card = new Acc(); var prod = new Acc();
        Vector3 V(float x, float y, float z) => pos + rot * new Vector3(x, y, z);
        Vector3[] b = { V(-w / 2, 0.003f, -d / 2), V(w / 2, 0.003f, -d / 2), V(w / 2, 0.003f, d / 2), V(-w / 2, 0.003f, d / 2) };
        Vector3[] t = { V(-w / 2, h, -d / 2), V(w / 2, h, -d / 2), V(w / 2, h, d / 2), V(-w / 2, h, d / 2) };
        float[] flapAng = { 118f, 152f, 100f, 136f };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            float len = (b[j] - b[i]).magnitude, u0 = R();
            Quad2(card, b[i], t[i], t[j], b[j], new Vector2(u0, 0.1f), new Vector2(u0 + len / 0.7f, 0.1f + h / 0.7f));
            // flap hinged on the top edge, folded outward
            Vector3 edge = (t[j] - t[i]).normalized, outw = Vector3.Cross(edge, Vector3.up).normalized;
            if (Vector3.Dot(outw, (t[i] + t[j]) * 0.5f - (pos + Vector3.up * h)) < 0f) outw = -outw;
            float fl = (i % 2 == 0 ? d : w) * 0.48f, a = flapAng[i] * Mathf.Deg2Rad;
            Vector3 fd = (Vector3.up * -Mathf.Cos(a) + outw * Mathf.Sin(a)).normalized;   // standing up, leaning outward
            Quad2(card, t[i], t[i] + fd * fl, t[j] + fd * fl, t[j], new Vector2(u0, 0.5f), new Vector2(u0 + len / 0.7f, 0.5f + fl / 0.7f));
        }
        Quad2(card, b[0], b[3], b[2], b[1], new Vector2(0.1f, 0.1f), new Vector2(0.1f + w / 0.7f, 0.1f + d / 0.7f));
        if (contents != null)
        {
            var list = Pick(contents);
            if (list.Count > 0)
            {
                Product p = list[rnd.Next(list.Count)];
                float pw = p.w + 0.006f, pd = (p.shape == "lathe" ? p.w : p.d) + 0.006f;
                int nx = Mathf.Max(1, Mathf.FloorToInt((w - 0.03f) / pw)), nz = Mathf.Max(1, Mathf.FloorToInt((d - 0.03f) / pd));
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        if (R() < 0.45f || p.h > h + 0.12f) continue;
                        Place(prod, p, V(-(nx - 1) * pw * 0.5f + ix * pw, 0.006f, -(nz - 1) * pd * 0.5f + iz * pd), rot * Vector3.forward, 0, R(-25f, 25f));
                    }
            }
        }
        card.Emit(name + " Card", go.transform, MCard);
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    /// <summary>Yellow fold-out "wet floor" board.</summary>
    public static void WetFloorSign(Transform parent, string name, Vector3 pos, float yaw)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * 0.3f, yawBox(new Vector3(0.3f, 0.6f, 0.34f), yaw)), true);
        var a = new Acc();
        const float w = 0.3f, h = 0.62f, spread = 0.15f;
        foreach (int s in new[] { -1, 1 })
        {
            Vector3 foot = pos + rot * new Vector3(0, 0, s * spread), head = pos + rot * new Vector3(0, h * 0.97f, s * 0.012f);
            Vector3 x = rot * Vector3.right * (w * 0.5f);
            Vector3 n = Vector3.Cross(head - foot, x).normalized * s;
            if (Vector3.Dot(n, rot * Vector3.forward * s) < 0f) n = -n;
            // outside: the print the right way round; inside: the same sheet
            a.Quad(foot + x * s, head + x * s, head - x * s, foot - x * s, n, Vector2.zero, Vector2.one);
            a.Quad(foot - x * s, head - x * s, head + x * s, foot + x * s, -n, Vector2.zero, Vector2.one);
        }
        a.Emit(name + " Board", go.transform, Panel("panel_wetfloor", 0.35f));
    }

    /// <summary>Door mat lying on the floor (long side along 'yaw').</summary>
    public static void DoorMat(Transform parent, string name, Vector3 pos, float yaw, float w, float len)
    {
        pos.y = FloorY + 0.021f;
        var go = PropHolder(name, parent, new Bounds(pos, new Vector3(w, 0.02f, len)), false);
        var a = new Acc();
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        a.Face(pos, Vector3.up, rot * Vector3.forward, w, len, Vector2.zero, Vector2.one);
        var mr = a.Emit(name + " Mat", go.transform, Panel("panel_mat", 0.05f), false);
    }

    /// <summary>Mop bucket with the mop standing in it, and the spray bottles next to it.</summary>
    public static void MopBucket(Transform parent, string name, Vector3 pos, float yaw, string[] bottles, Vector3[] bottleSpots)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * 0.16f, new Vector3(0.36f, 0.32f, 0.36f)), true);
        var plastic = new Acc(); var water = new Acc(); var wood = new Acc(); var mop = new Acc(); var steel = new Acc(); var prod = new Acc();
        Cyl(plastic, pos, pos + Vector3.up * 0.3f, 0.13f, 0.17f, 12, One, true, false);
        Cyl(plastic, pos + Vector3.up * 0.3f, pos + Vector3.up * 0.02f, 0.162f, 0.124f, 12, One, false, false);          // inside wall
        Cyl(water, pos + Vector3.up * 0.2f, pos + Vector3.up * 0.2005f, 0.15f, 0.15f, 12, One, false, true);
        // wire handle resting on the rim
        Vector3 hx = rot * Vector3.right;
        Cyl(steel, pos + hx * 0.17f + Vector3.up * 0.28f, pos + hx * 0.12f + Vector3.up * 0.33f + rot * Vector3.forward * 0.16f, 0.005f, 0.005f, 4, One);
        Cyl(steel, pos - hx * 0.17f + Vector3.up * 0.28f, pos - hx * 0.12f + Vector3.up * 0.33f + rot * Vector3.forward * 0.16f, 0.005f, 0.005f, 4, One);
        Cyl(steel, pos + hx * 0.12f + Vector3.up * 0.33f + rot * Vector3.forward * 0.16f, pos - hx * 0.12f + Vector3.up * 0.33f + rot * Vector3.forward * 0.16f, 0.005f, 0.005f, 4, One);
        // mop: head in the water, handle leaning on the rim
        Vector3 foot = pos + Vector3.up * 0.1f + rot * new Vector3(0.03f, 0, 0.04f), tip = pos + Vector3.up * 1.32f + rot * new Vector3(-0.2f, 0, -0.34f);
        Cyl(wood, foot, tip, 0.013f, 0.013f, 6, new Rect(0, 0, 0.1f, 1f));
        Cyl(mop, foot + Vector3.down * 0.07f, foot + (tip - foot).normalized * 0.16f, 0.085f, 0.03f, 8, new Rect(0, 0, 1.5f, 1f));
        if (bottles != null)
        {
            var list = Pick(bottles);
            for (int i = 0; i < bottleSpots.Length && list.Count > 0; i++)
                Place(prod, list[(i * 2 + 1) % list.Count], new Vector3(bottleSpots[i].x, FloorY, bottleSpots[i].z), Quaternion.Euler(0, R(0f, 360f), 0) * Vector3.forward, 0);
        }
        plastic.Emit(name + " Bucket", go.transform, Flat("SM_BucketYellow", new Color(0.86f, 0.68f, 0.1f), 0.4f));
        water.Emit(name + " Water", go.transform, Flat("SM_DirtyWater", new Color(0.2f, 0.19f, 0.16f), 0.85f), false);
        wood.Emit(name + " Handle", go.transform, Flat("SM_BroomWood", new Color(0.62f, 0.46f, 0.26f), 0.25f), false);
        mop.Emit(name + " Mop", go.transform, Mat("SM_MopStrands", "fit_bristle.png", new Color(0.62f, 0.62f, 0.6f), 0.05f, false, false), false);
        steel.Emit(name + " Wire", go.transform, MSteel, false);
        prod.Emit(name + " Bottles", go.transform, MProducts, false);
    }

    /// <summary>Broom leaning on a wall with a dustpan at its foot. 'lean' points at the wall.</summary>
    public static void Broom(Transform parent, string name, Vector3 foot, Vector3 lean)
    {
        foot.y = FloorY;
        lean = new Vector3(lean.x, 0, lean.z).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, lean);
        var go = PropHolder(name, parent, new Bounds(foot + Vector3.up * 0.6f, new Vector3(0.36f, 1.2f, 0.36f)), false);
        var wood = new Acc(); var head = new Acc(); var bristle = new Acc(); var pan = new Acc();
        Vector3 tip = foot + Vector3.up * 1.26f + lean * 0.3f, neck = foot + Vector3.up * 0.16f + lean * 0.03f;
        Cyl(wood, neck, tip, 0.012f, 0.012f, 6, new Rect(0, 0, 0.1f, 1f));
        Quaternion rot = Quaternion.LookRotation(lean, (tip - neck).normalized);
        OBox(head, neck, new Vector3(0.3f, 0.045f, 0.05f), rot);
        OBox(bristle, neck + (neck - tip).normalized * 0.085f, new Vector3(0.31f, 0.13f, 0.055f), rot, 1f, new Rect(0, 0, 1, 1));
        // dustpan lying beside it
        Vector3 pc = foot + side * 0.3f - lean * 0.02f + Vector3.up * 0.004f;
        Quaternion pr = Quaternion.LookRotation(Quaternion.Euler(0, 25f, 0) * -lean, Vector3.up);
        OBox(pan, pc, new Vector3(0.26f, 0.008f, 0.22f), pr);
        OBox(pan, pc + pr * new Vector3(0, 0.03f, -0.11f), new Vector3(0.26f, 0.06f, 0.008f), pr);
        OBox(pan, pc + pr * new Vector3(-0.13f, 0.025f, -0.02f), new Vector3(0.008f, 0.05f, 0.18f), pr);
        OBox(pan, pc + pr * new Vector3(0.13f, 0.025f, -0.02f), new Vector3(0.008f, 0.05f, 0.18f), pr);
        OBox(pan, pc + pr * new Vector3(0, 0.05f, -0.2f), new Vector3(0.03f, 0.022f, 0.2f), pr * Quaternion.Euler(-20f, 0, 0));
        wood.Emit(name + " Handle", go.transform, Flat("SM_BroomWood", new Color(0.62f, 0.46f, 0.26f), 0.25f), false);
        head.Emit(name + " Head", go.transform, Flat("SM_PlasticGreen", new Color(0.16f, 0.45f, 0.24f), 0.4f), false);
        bristle.Emit(name + " Bristles", go.transform, Mat("SM_Bristle", "fit_bristle.png", Color.white, 0.05f, false, false), false);
        pan.Emit(name + " Dustpan", go.transform, Flat("SM_PlasticGreen", new Color(0.16f, 0.45f, 0.24f), 0.4f), false);
    }

    /// <summary>Picnic cooler box: blue tub, white lid, carry handle.</summary>
    public static void Cooler(Transform parent, string name, Vector3 pos, float yaw)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * 0.17f, yawBox(new Vector3(0.46f, 0.34f, 0.3f), yaw)), true);
        var blue = new Acc(); var white = new Acc();
        OBox(blue, pos + Vector3.up * 0.135f, new Vector3(0.44f, 0.27f, 0.28f), rot, 1.2f);
        OBox(white, pos + Vector3.up * 0.3f, new Vector3(0.46f, 0.06f, 0.3f), rot, 1.2f);
        OBox(white, pos + Vector3.up * 0.345f, new Vector3(0.2f, 0.03f, 0.05f), rot, 1.2f);
        foreach (int s in new[] { -1, 1 }) OBox(white, pos + Vector3.up * 0.2f + rot * new Vector3(s * 0.225f, 0, 0), new Vector3(0.012f, 0.05f, 0.12f), rot, 1.2f);
        blue.Emit(name + " Tub", go.transform, Body("fit_fridge_blue"));
        white.Emit(name + " Lid", go.transform, Body("fit_fridge_white"), false);
    }

    /// <summary>Plastic bottle crate on the floor with a few bottles left in it.</summary>
    public static void BottleCrate(Transform parent, string name, Vector3 pos, float yaw, string[] contents)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var go = PropHolder(name, parent, new Bounds(pos + Vector3.up * 0.12f, yawBox(new Vector3(0.4f, 0.24f, 0.3f), yaw)), true);
        var crate = new Acc(); var inside = new Acc(); var prod = new Acc();
        Vector3 face = rot * Vector3.forward, along = rot * Vector3.right;
        const float w = 0.4f, h = 0.24f, d = 0.3f;
        Vector3 c = pos + Vector3.up * 0.002f;
        crate.Face2(c + face * d * 0.5f + Vector3.up * h * 0.5f, face, Vector3.up, w, h, Vector2.zero, new Vector2(1.4f, 1f));
        crate.Face2(c - face * d * 0.5f + Vector3.up * h * 0.5f, -face, Vector3.up, w, h, Vector2.zero, new Vector2(1.4f, 1f));
        crate.Face2(c + along * w * 0.5f + Vector3.up * h * 0.5f, along, Vector3.up, d, h, Vector2.zero, new Vector2(1f, 1f));
        crate.Face2(c - along * w * 0.5f + Vector3.up * h * 0.5f, -along, Vector3.up, d, h, Vector2.zero, new Vector2(1f, 1f));
        inside.Face(c + Vector3.up * 0.012f, Vector3.up, face, w - 0.01f, d - 0.01f, Vector2.zero, Vector2.one);
        var list = Pick(contents);
        for (int ix = 0; ix < 4 && list.Count > 0; ix++)
            for (int iz = 0; iz < 3; iz++)
            {
                if (R() < 0.55f) continue;
                Product p = list[(ix + iz) % list.Count];
                if (p.w > 0.095f) continue;
                Place(prod, p, c + Vector3.up * 0.014f + along * (-0.145f + ix * 0.097f) + face * (-0.095f + iz * 0.095f), face, 0, R(0f, 360f));
            }
        crate.Emit(name + " Crate", go.transform, Mat("SM_crate_blue", "crate_blue.png", Color.white, 0.35f, false, true), false);
        inside.Emit(name + " Floor", go.transform, MDark, false);
        prod.Emit(name + " Bottles", go.transform, MProducts, false);
    }

    /// <summary>
    /// Stock that ended up on the floor. Each entry: product id (or category), x, z, yaw and how it lies:
    /// 'f' flat with the picture up (bags, boxes), 's' on its side (cans, bottles roll), 'u' upright.
    /// </summary>
    public static void Spill(Transform parent, string name, (string id, float x, float z, float yaw, char mode)[] items)
    {
        if (items.Length == 0) return;
        var go = PropHolder(name, parent, new Bounds(new Vector3(items[0].x, FloorY, items[0].z), Vector3.one * 0.2f), false);
        var prod = new Acc();
        foreach (var it in items)
        {
            var list = Pick(new[] { it.id });
            if (list.Count == 0) continue;
            Product p = list[rnd.Next(list.Count)];
            Vector3 fl = new Vector3(it.x, FloorY + 0.022f, it.z);
            Vector3 dir = Quaternion.Euler(0, it.yaw, 0) * Vector3.forward;
            if (it.mode == 'f')
            {
                float th = p.shape == "bag" || p.shape == "sack" ? p.d * 0.5f : p.d * 0.5f;
                prod.Append(Local(p, 0), fl - dir * (p.h * 0.5f) + Vector3.up * th, Quaternion.LookRotation(Vector3.up, dir));
            }
            else if (it.mode == 's')
            {
                float rad = p.w * 0.5f * (p.sq <= 0f ? 1f : Mathf.Min(1f, p.sq));
                prod.Append(Local(p, 0), fl - dir * (p.h * 0.5f) + Vector3.up * rad, Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0, R(0f, 360f), 0));
            }
            else Place(prod, p, fl, dir, 0);
        }
        prod.Emit(name + " Stock", go.transform, MProducts, false);
    }

    /// <summary>Steel distribution-board enclosure on a wall, with a conduit up to the ceiling. 'face' points into the room.</summary>
    public static void FuseBoxShell(Transform parent, string name, Vector3 centre, Vector3 face, float w, float h, float d, float ceilingY)
    {
        var go = PropHolder(name, parent, new Bounds(centre, new Vector3(d, h, w)), false);
        var steel = new Acc(); var door = new Acc();
        face = face.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
        OBox(steel, centre, new Vector3(w, h, d), rot, 0.9f);
        door.Face(centre + face * (d * 0.5f + 0.002f), face, Vector3.up, w - 0.012f, h - 0.012f, Vector2.zero, Vector2.one);
        Vector3 top = centre + Vector3.up * h * 0.5f - face * (d * 0.5f - 0.03f);
        Cyl(steel, top + rot * new Vector3(0.1f, 0, 0), new Vector3(top.x, ceilingY, top.z) + rot * new Vector3(0.1f, 0, 0), 0.016f, 0.016f, 6, new Rect(0, 0, 0.1f, 2f));
        Cyl(steel, top + rot * new Vector3(-0.06f, 0, 0), new Vector3(top.x, ceilingY, top.z) + rot * new Vector3(-0.06f, 0, 0), 0.011f, 0.011f, 6, new Rect(0, 0, 0.1f, 2f));
        steel.Emit(name + " Box", go.transform, MSteel);
        door.Emit(name + " Door", go.transform, Panel("panel_fusebox", 0.3f), false);
    }
}
