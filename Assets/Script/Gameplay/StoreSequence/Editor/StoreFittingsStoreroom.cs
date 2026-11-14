using UnityEditor;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Storage-room fittings of the store makeover: boltless steel racking full of cartons, pallets of stock, the packing
/// table, steel doors, the staff notice board, tube lights, the tool shelf the hammer lies on.
/// (Set StoreFittings.off.y so that FloorY lands on the storage room's floor before calling these.)
/// </summary>
public static partial class StoreFittings
{
    static Material MRackBlue => Mat("SM_RackBlue", "fit_rack_blue.png", Color.white, 0.3f, false, false);
    static Material MRackOrange => Mat("SM_RackOrange", "fit_rack_orange.png", Color.white, 0.3f, false, false);
    static Material MRackGrey => Mat("SM_RackGrey", "fit_rack_grey.png", Color.white, 0.35f, false, false);
    static Material MChip => Mat("SM_Chipboard", "fit_chipboard.png", Color.white, 0.08f, false, false);
    static Material MPallet => Mat("SM_PalletWood", "fit_pallet.png", Color.white, 0.05f, false, false);

    /// <summary>
    /// Boltless stock racking against a wall: blue slotted uprights, orange beams, chipboard decks, stock by level.
    /// a -> b along the wall, 'face' into the room. A null level plan leaves that deck empty.
    /// </summary>
    public static void Racking(Transform parent, string name, Vector3 a, Vector3 b, Vector3 face, float[] levels, string[][] plan,
                               float depth = 0.6f, float height = 2.4f, int maxStack = 3, float gapChance = 0.18f)
    {
        a.y = 0; b.y = 0;
        Vector3 along = (b - a).normalized; float len = (b - a).magnitude;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        Vector3 mid = (a + b) * 0.5f + face * depth * 0.5f;
        var go = Holder(name, parent, new Bounds(new Vector3(mid.x, FloorY + height * 0.5f, mid.z), new Vector3(alongX ? len : depth, height, alongX ? depth : len)));
        var blue = new Acc(); var orange = new Acc(); var deck = new Acc(); var prod = new Acc();
        int bays = Mathf.Max(1, Mathf.RoundToInt(len / 1.9f));
        for (int k = 0; k <= bays; k++)
            foreach (float d in new[] { 0.035f, depth - 0.035f })
            {
                Vector3 p = a + along * Mathf.Clamp(len * k / bays, 0.03f, len - 0.03f) + face * d;
                blue.BoxM(new Vector3(p.x, FloorY + height * 0.5f, p.z), new Vector3(0.06f, height, 0.06f), face, 0.26f);
            }
        // side braces
        for (int k = 0; k <= bays; k++)
        {
            Vector3 p = a + along * Mathf.Clamp(len * k / bays, 0.03f, len - 0.03f) + face * depth * 0.5f;
            foreach (float y in new[] { 0.3f, height - 0.2f })
                blue.BoxM(new Vector3(p.x, FloorY + y, p.z), new Vector3(depth - 0.07f, 0.04f, 0.02f), along, 0.26f);
        }
        for (int i = 0; i < levels.Length; i++)
        {
            float ly = FloorY + levels[i];
            foreach (float d in new[] { 0.035f, depth - 0.035f })
            {
                Vector3 c = (a + b) * 0.5f + face * d;
                orange.BoxM(new Vector3(c.x, ly - 0.055f, c.z), new Vector3(len - 0.06f, 0.09f, 0.045f), face, 0.5f);
            }
            deck.BoxM(new Vector3(mid.x, ly - 0.009f, mid.z), new Vector3(len - 0.08f, 0.018f, depth - 0.05f), face, 0.9f, true);
            float clear = (i + 1 < levels.Length ? levels[i + 1] - levels[i] - 0.12f : 0.7f);
            if (plan != null && i < plan.Length && plan[i] != null)
            {
                Vector3 from = a + face * (depth - 0.05f) + along * 0.09f; from.y = ly;
                FillShelf(prod, from, along, face, len - 0.18f, depth - 0.1f, clear, plan[i], 2, gapChance, 0f, maxStack);
            }
        }
        blue.Emit(name + " Uprights", go.transform, MRackBlue);
        orange.Emit(name + " Beams", go.transform, MRackOrange, false);
        deck.Emit(name + " Decks", go.transform, MChip);
        prod.Emit(name + " Stock", go.transform, MProducts);
    }

    /// <summary>A wooden pallet with stock stacked on it (cols along x of the pallet, rows along z).</summary>
    public static void Pallet(Transform parent, string name, Vector3 pos, float yaw, string[] ids, int cols, int rows, int layers, float missing = 0.3f)
    {
        pos.y = FloorY;
        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        var list = Pick(ids);
        Product p0 = list.Count > 0 ? list[0] : null;
        float H = 0.145f + (p0 != null ? layers * p0.h : 0f);
        var go = Holder(name, parent, new Bounds(pos + Vector3.up * H * 0.5f, yawBox(new Vector3(1.2f, H, 1.0f), yaw)));
        var wood = new Acc(); var prod = new Acc();
        // 3 runners, 5 top boards, 3 bottom boards
        foreach (float z in new[] { -0.45f, 0f, 0.45f }) OBox(wood, pos + rot * new Vector3(0, 0.072f, z), new Vector3(1.2f, 0.1f, 0.1f), rot, 0.8f);
        foreach (float x in new[] { -0.52f, -0.26f, 0f, 0.26f, 0.52f }) OBox(wood, pos + rot * new Vector3(x, 0.133f, 0), new Vector3(0.14f, 0.022f, 1.0f), rot, 0.8f);
        foreach (float x in new[] { -0.52f, 0f, 0.52f }) OBox(wood, pos + rot * new Vector3(x, 0.011f, 0), new Vector3(0.14f, 0.022f, 1.0f), rot, 0.8f);
        if (p0 != null)
        {
            float sx = p0.w + 0.012f, sz = (p0.shape == "lathe" ? p0.w : p0.d) + 0.012f;
            for (int L = 0; L < layers; L++)
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rows; r++)
                    {
                        if (L == layers - 1 && layers > 1 && R() < missing) continue;
                        Product p = list[(L + c * 2 + r) % list.Count];
                        Vector3 lp = new Vector3(-(cols - 1) * sx * 0.5f + c * sx, 0.146f + L * p0.h, -(rows - 1) * sz * 0.5f + r * sz);
                        Place(prod, p, pos + rot * lp, rot * (r >= rows / 2 ? Vector3.forward : Vector3.back), 0, R(-3f, 3f));
                    }
        }
        wood.Emit(name + " Pallet", go.transform, MPallet);
        prod.Emit(name + " Stock", go.transform, MProducts);
    }

    /// <summary>Steel packing table against a wall: chipboard top, grey frame, lower shelf.</summary>
    public static void WorkTable(Transform parent, string name, Vector3 a, Vector3 b, Vector3 face, float depth = 0.8f, float topH = 0.9f)
    {
        a.y = 0; b.y = 0;
        Vector3 along = (b - a).normalized; float len = (b - a).magnitude;
        bool alongX = Mathf.Abs(along.x) > 0.5f;
        Vector3 mid = (a + b) * 0.5f + face * depth * 0.5f;
        var go = Holder(name, parent, new Bounds(new Vector3(mid.x, FloorY + topH * 0.5f, mid.z), new Vector3(alongX ? len : depth, topH, alongX ? depth : len)));
        var grey = new Acc(); var top = new Acc();
        top.BoxM(new Vector3(mid.x, FloorY + topH - 0.02f, mid.z), new Vector3(len, 0.04f, depth), face, 0.9f, true);
        top.BoxM(new Vector3(mid.x, FloorY + 0.22f, mid.z), new Vector3(len - 0.1f, 0.02f, depth - 0.1f), face, 0.9f, true);
        foreach (float u in new[] { 0.04f, len - 0.04f })
            foreach (float d in new[] { 0.04f, depth - 0.04f })
            {
                Vector3 p = a + along * u + face * d;
                grey.BoxM(new Vector3(p.x, FloorY + (topH - 0.04f) * 0.5f, p.z), new Vector3(0.05f, topH - 0.04f, 0.05f), face, 0.4f);
            }
        foreach (float d in new[] { 0.04f, depth - 0.04f })
        {
            Vector3 c = (a + b) * 0.5f + face * d;
            grey.BoxM(new Vector3(c.x, FloorY + topH - 0.08f, c.z), new Vector3(len - 0.1f, 0.06f, 0.03f), face, 0.4f);
        }
        grey.Emit(name + " Frame", go.transform, MRackGrey);
        top.Emit(name + " Top", go.transform, MChip);
    }

    /// <summary>A flat printed panel on a wall with a thin dark frame (doors, notice board).</summary>
    public static void WallPanel(Transform parent, string name, Vector3 centre, Vector3 face, float w, float h, string tex, float frame = 0.05f, float gloss = 0.25f)
    {
        var go = PropHolder(name, parent, new Bounds(centre, new Vector3(w, h, 0.1f)), false);
        var a = new Acc(); var fr = new Acc();
        face = face.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
        a.Face(centre + face * 0.012f, face, Vector3.up, w, h, Vector2.zero, Vector2.one);
        if (frame > 0f)
        {
            OBox(fr, centre + Vector3.up * (h * 0.5f + frame * 0.5f) + face * 0.02f, new Vector3(w + frame * 2f, frame, 0.06f), rot, 0.5f);
            foreach (int s in new[] { -1, 1 })
                OBox(fr, centre + rot * new Vector3(s * (w * 0.5f + frame * 0.5f), 0, 0) + face * 0.02f, new Vector3(frame, h, 0.06f), rot, 0.5f);
        }
        a.Emit(name + " Face", go.transform, Panel(tex, gloss), false);
        fr.Emit(name + " Frame", go.transform, MRackGrey, false);
    }

    /// <summary>
    /// A painted steel service door in its frame, standing shut in a wall: photo-textured leaf, frame, three hinges, a kick
    /// plate and a real lever handle with its lock (things can hang on it). 'centre' = middle of the leaf on the wall
    /// surface, 'face' points into the room. Returns the middle of the lever (world), e.g. to hang something from it.
    /// </summary>
    public static Vector3 SteelDoor(Transform parent, string name, Vector3 centre, Vector3 face, float w, float h, bool handleOnRight, string leafTex, string signTex = null, float transom = 0f)
    {
        var go = PropHolder(name, parent, new Bounds(centre, new Vector3(w + 0.2f, h + 0.3f, 0.12f)), false);
        face = face.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
        Vector3 R = rot * Vector3.right, U = Vector3.up;
        var leaf = new Acc(); var frame = new Acc(); var steel = new Acc(); var hw = new Acc(); var sign = new Acc(); var box = new Acc();
        var one = new Rect(0.1f, 0.1f, 0.3f, 0.3f);
        const float fw = 0.065f;

        // frame: steel angle, proud of the wall, the leaf sits back inside it
        OBox(frame, centre + U * (h * 0.5f + fw * 0.5f) + face * 0.035f, new Vector3(w + fw * 2f, fw, 0.09f), rot, 0.5f);
        foreach (int sd in new[] { -1, 1 })
            OBox(frame, centre + R * (sd * (w * 0.5f + fw * 0.5f)) + U * (fw * 0.0f) + face * 0.035f, new Vector3(fw, h, 0.09f), rot, 0.5f);
        if (transom > 0f)
        {
            // a fixed panel over the head, in the same frame: the opening in the wall is taller than the leaf
            float top = h * 0.5f + fw;
            foreach (int sd in new[] { -1, 1 })
                OBox(frame, centre + R * (sd * (w * 0.5f + fw * 0.5f)) + U * (top + transom * 0.5f) + face * 0.035f, new Vector3(fw, transom, 0.09f), rot, 0.5f);
            OBox(frame, centre + U * (top + transom + fw * 0.5f) + face * 0.035f, new Vector3(w + fw * 2f, fw, 0.09f), rot, 0.5f);
            leaf.Face(centre + U * (top + transom * 0.5f) + face * 0.05f, face, U, w - 0.008f, transom, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.72f + 0.24f * Mathf.Clamp01(transom / 0.6f)));
        }
        // threshold strip
        OBox(steel, centre + U * (-h * 0.5f + 0.008f) + face * 0.04f, new Vector3(w, 0.016f, 0.1f), rot, 0.6f);

        // leaf
        leaf.Face(centre + face * 0.05f, face, U, w - 0.008f, h - 0.006f, Vector2.zero, Vector2.one);
        // kick plate
        OBox(steel, centre + U * (-h * 0.5f + 0.14f) + face * 0.052f, new Vector3(w - 0.07f, 0.23f, 0.004f), rot, 0.6f);

        // hinges (on the side away from the handle)
        float s = handleOnRight ? 1f : -1f;
        foreach (float hy in new[] { -h * 0.5f + 0.24f, 0.04f, h * 0.5f - 0.24f })
        {
            Vector3 hc = centre + R * (-s * (w * 0.5f - 0.002f)) + U * hy + face * 0.062f;
            Cyl(steel, hc - U * 0.055f, hc + U * 0.055f, 0.012f, 0.012f, 8, one);
            OBox(steel, hc + R * (s * 0.028f) - face * 0.008f, new Vector3(0.05f, 0.1f, 0.004f), rot, 0.6f);
        }

        // lever handle on its plate, cylinder lock under it. 1.02 m above the floor.
        Vector3 hp = centre + R * (s * (w * 0.5f - 0.085f)) + U * (-h * 0.5f + 1.02f);
        OBox(hw, hp - U * 0.045f + face * 0.055f, new Vector3(0.048f, 0.25f, 0.01f), rot, 1f);
        Cyl(hw, hp + face * 0.058f, hp + face * 0.112f, 0.013f, 0.012f, 10, one);
        Vector3 l0 = hp + face * 0.104f + R * (s * 0.013f), l1 = hp + face * 0.104f - R * (s * 0.135f) - U * 0.006f;
        Cyl(hw, l0, l1, 0.0115f, 0.0105f, 10, one);
        Cyl(hw, l1, l1 - face * 0.022f, 0.0105f, 0.0105f, 8, one);                 // the return at the tip
        Cyl(steel, hp - U * 0.115f + face * 0.058f, hp - U * 0.115f + face * 0.072f, 0.0155f, 0.0155f, 12, one);   // cylinder lock
        // a slide bolt high up, the kind every back door there has
        Vector3 bp = centre + R * (s * (w * 0.5f - 0.13f)) + U * (h * 0.5f - 0.42f);
        OBox(hw, bp + face * 0.056f, new Vector3(0.2f, 0.05f, 0.012f), rot, 1f);
        Cyl(steel, bp + face * 0.07f - R * (s * 0.085f), bp + face * 0.07f + R * (s * 0.12f), 0.008f, 0.008f, 8, one);
        Cyl(steel, bp + face * 0.07f - R * (s * 0.03f), bp + face * 0.098f - R * (s * 0.03f), 0.006f, 0.006f, 6, one);

        if (signTex != null)
        {
            Vector3 sc = centre + U * (h * 0.5f + fw + 0.17f) + face * 0.045f;
            OBox(box, sc, new Vector3(0.56f, 0.21f, 0.07f), rot, 1f);
            sign.Face(sc + face * 0.0362f, face, U, 0.53f, 0.185f, Vector2.zero, Vector2.one);
        }

        var leafMat = Mat("SM_" + leafTex, leafTex + ".png", Color.white, 0.28f, false, false);
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/StoreMakeover/door_steel_n.png");
        if (nrm != null)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/StoreMakeover/door_steel_n.png");
            if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
            leafMat.EnableKeyword("_NORMALMAP"); leafMat.SetTexture("_BumpMap", nrm); leafMat.SetFloat("_BumpScale", 0.8f);
            EditorUtility.SetDirty(leafMat);
        }
        leaf.Emit(name + " Leaf", go.transform, leafMat, false);
        frame.Emit(name + " Frame", go.transform, Mat("SM_DoorFrameSteel", "fit_steel.png", new Color(0.3f, 0.32f, 0.31f), 0.3f, false, false), false);
        steel.Emit(name + " Fittings", go.transform, MSteel, false);
        hw.Emit(name + " Handle", go.transform, Flat("SM_DoorHardwareBlack", new Color(0.085f, 0.085f, 0.09f), 0.55f), false);
        if (signTex != null)
        {
            box.Emit(name + " Sign Box", go.transform, MPlasticDark, false);
            var sm = Mat("SM_" + signTex, signTex + ".png", new Color(0.55f, 0.55f, 0.55f), 0.5f, false, false);
            sm.EnableKeyword("_EMISSION");
            sm.SetTexture("_EmissionMap", sm.mainTexture);
            sm.SetColor("_EmissionColor", new Color(0.75f, 0.85f, 0.75f));
            sm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(sm);
            sign.Emit(name + " Sign", go.transform, sm, false);
        }
        return (l0 + l1) * 0.5f + off;
    }

    /// <summary>A twin-head battery emergency light, the kind screwed to the wall of every shop: a cream battery box with two
    /// round lamp heads under it, turned to shine at 'aimAt'. Gives back the lens renderer (its glow is switched by
    /// FuseCabinetHighlight) and makes the spot light (off). World positions.</summary>
    public static GameObject EmergencyLamp(Transform parent, string name, Vector3 wallPoint, Vector3 face, Vector3 aimAt, out Light light, out Renderer lens)
    {
        face = face.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
        Vector3 R = rot * Vector3.right, U = Vector3.up;
        var go = PropHolder(name, parent, new Bounds(wallPoint + face * 0.06f - U * 0.05f, new Vector3(0.34f, 0.26f, 0.2f)), false);
        var body = new Acc(); var dark = new Acc(); var glass = new Acc(); var led = new Acc();
        var one = new Rect(0.1f, 0.1f, 0.3f, 0.3f);
        Vector3 c = wallPoint + face * 0.045f;
        OBox(body, c, new Vector3(0.3f, 0.115f, 0.09f), rot, 0.5f);
        OBox(body, c + U * 0.0605f, new Vector3(0.27f, 0.006f, 0.075f), rot, 0.5f);                 // lid seam
        OBox(dark, c + face * 0.0455f - R * 0.095f + U * 0.02f, new Vector3(0.07f, 0.03f, 0.002f), rot, 1f);   // rating label
        OBox(dark, c + face * 0.0455f + R * 0.11f - U * 0.03f, new Vector3(0.022f, 0.012f, 0.004f), rot, 1f);  // test button
        OBox(led, c + face * 0.0455f + R * 0.075f - U * 0.03f, new Vector3(0.008f, 0.008f, 0.004f), rot, 1f);
        Vector3 mid = Vector3.zero;
        foreach (int sd in new[] { -1, 1 })
        {
            Vector3 joint = c + R * (sd * 0.085f) - U * 0.0575f + face * 0.005f;
            Vector3 head = joint - U * 0.05f + face * 0.02f;
            Vector3 d = (aimAt - (head + off)).normalized;
            Cyl(dark, joint, head, 0.011f, 0.011f, 8, one);                                         // swivel stalk
            Cyl(body, head - d * 0.032f, head + d * 0.03f, 0.036f, 0.046f, 14, one);                // the reflector cup
            Cyl(body, head - d * 0.044f, head - d * 0.032f, 0.022f, 0.036f, 14, one);
            Cyl(dark, head + d * 0.03f, head + d * 0.036f, 0.048f, 0.048f, 14, one);               // bezel
            Cyl(glass, head + d * 0.0362f, head + d * 0.0375f, 0.041f, 0.041f, 14, one);           // lens
            mid += head + d * 0.04f;
        }
        mid *= 0.5f;
        body.Emit(name + " Body", go.transform, Flat("SM_EmergencyLampBody", new Color(0.83f, 0.81f, 0.74f), 0.38f), false);
        dark.Emit(name + " Trim", go.transform, MPlasticDark, false);
        var ledMat = Flat("SM_EmergencyLampLed", new Color(0.1f, 0.5f, 0.12f), 0.5f);
        ledMat.EnableKeyword("_EMISSION"); ledMat.SetColor("_EmissionColor", new Color(0.2f, 1.6f, 0.25f)); ledMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(ledMat);
        led.Emit(name + " LED", go.transform, ledMat, false);
        var lensMat = Flat("SM_EmergencyLampLens", new Color(0.62f, 0.62f, 0.58f), 0.85f);
        lensMat.EnableKeyword("_EMISSION"); lensMat.SetColor("_EmissionColor", Color.black); lensMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(lensMat);
        var lensGo = glass.Emit(name + " Lens", go.transform, lensMat, false);
        lens = lensGo != null ? lensGo.GetComponent<Renderer>() : null;

        var lg = new GameObject(name + " Light");
        lg.transform.SetParent(go.transform, false);
        lg.transform.position = mid + off;
        lg.transform.rotation = Quaternion.LookRotation((aimAt - (mid + off)).normalized, Vector3.up);
        light = lg.AddComponent<Light>();
        light.type = LightType.Spot; light.spotAngle = 78f; light.innerSpotAngle = 40f; light.range = 7.5f; light.intensity = 0f;
        light.color = new Color(1f, 0.93f, 0.78f); light.shadows = LightShadows.Soft; light.shadowStrength = 0.85f; light.shadowBias = 0.03f;
        light.renderMode = LightRenderMode.ForcePixel; light.enabled = false;
        return go;
    }

    /// <summary>The twisted handles of a plastic bag looped over a door lever: from the bag's neck ('tip') up one side of the
    /// lever, over it and down the other. World positions. The object is parented to 'parent' (the bag), so it goes when the bag goes.</summary>
    public static GameObject BagTie(Transform parent, string name, Vector3 tip, Vector3 lever, Vector3 face)
    {
        var a = new Acc(); var one = new Rect(0.1f, 0.1f, 0.2f, 0.2f);
        face = face.normalized;
        Vector3 o = -off;                                   // Emit adds 'off' again
        Vector3[] path =
        {
            tip, lever + face * 0.02f - Vector3.up * 0.012f, lever + face * 0.016f + Vector3.up * 0.012f, lever + Vector3.up * 0.02f,
            lever - face * 0.016f + Vector3.up * 0.012f, lever - face * 0.02f - Vector3.up * 0.012f, tip,
        };
        for (int i = 0; i + 1 < path.Length; i++) Cyl(a, path[i] + o, path[i + 1] + o, 0.0055f, 0.0055f, 6, one);
        Cyl(a, tip + o - Vector3.up * 0.03f, tip + o + Vector3.up * 0.012f, 0.02f, 0.008f, 8, one);     // the knot
        var go = a.Emit(name, null, Flat("SM_BagTieBlack", new Color(0.035f, 0.04f, 0.035f), 0.5f), false);
        if (go != null) go.transform.SetParent(parent, true);
        return go;
    }

    /// <summary>Surface-mounted twin-tube light under the ceiling (the lens glows; the scene's own lights do the lighting).</summary>
    public static void TubeLight(Transform parent, string name, Vector3 centre, Vector3 along, float len)
    {
        var go = PropHolder(name, parent, new Bounds(centre, new Vector3(0.3f, 0.1f, len)), false);
        var body = new Acc(); var lens = new Acc();
        along = along.normalized;
        Quaternion rot = Quaternion.LookRotation(along, Vector3.up);
        OBox(body, centre, new Vector3(0.24f, 0.07f, len), rot, 0.5f);
        lens.Face(centre + Vector3.down * 0.037f, Vector3.down, Vector3.Cross(Vector3.up, along), len - 0.04f, 0.2f, Vector2.zero, Vector2.one);
        body.Emit(name + " Body", go.transform, MRackGrey, false);
        var m = Mat("SM_TubeLens", "panel_tubelight.png", Color.white, 0.2f, false, false);
        m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", m.mainTexture); m.SetColor("_EmissionColor", new Color(0.85f, 0.88f, 0.8f));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        UnityEditor.EditorUtility.SetDirty(m);
        lens.Emit(name + " Lens", go.transform, m, false);
    }

    /// <summary>Tools and odds and ends on a shelf deck: toolbox, paint tins, jerry can, a box of bulbs. 'origin' = left end of the deck's front edge.</summary>
    public static void ShelfTools(Transform parent, string name, Vector3 origin, Vector3 along, Vector3 face, float y)
    {
        along = along.normalized; face = face.normalized;
        Vector3 P(float u, float d, float h) => new Vector3(origin.x, FloorY + y + h, origin.z) + along * u - face * d;
        var go = PropHolder(name, parent, new Bounds(P(0.8f, 0.25f, 0.15f), new Vector3(1.6f, 0.3f, 0.5f)), false);
        var red = new Acc(); var steel = new Acc(); var blue = new Acc(); var card = new Acc(); var dark = new Acc();
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
        // toolbox with a handle
        OBox(red, P(0.32f, 0.26f, 0.09f), new Vector3(0.42f, 0.18f, 0.2f), rot * Quaternion.Euler(0, 8f, 0));
        OBox(dark, P(0.32f, 0.26f, 0.195f), new Vector3(0.2f, 0.03f, 0.03f), rot * Quaternion.Euler(0, 8f, 0));
        OBox(steel, P(0.2f, 0.155f, 0.11f), new Vector3(0.03f, 0.04f, 0.012f), rot * Quaternion.Euler(0, 8f, 0));
        OBox(steel, P(0.44f, 0.165f, 0.11f), new Vector3(0.03f, 0.04f, 0.012f), rot * Quaternion.Euler(0, 8f, 0));
        // paint tins
        float[] us = { 0.74f, 0.9f, 0.82f }; float[] ds = { 0.3f, 0.32f, 0.16f };
        for (int i = 0; i < 3; i++)
        {
            Cyl(steel, P(us[i], ds[i], 0f), P(us[i], ds[i], 0.16f), 0.07f, 0.07f, 10, new Rect(0, 0, 1f, 0.5f));
            Cyl(dark, P(us[i], ds[i], 0.16f), P(us[i], ds[i], 0.162f), 0.066f, 0.066f, 10, One, false, true);
        }
        // jerry can, carton of bulbs
        OBox(blue, P(1.18f, 0.28f, 0.17f), new Vector3(0.16f, 0.34f, 0.3f), rot * Quaternion.Euler(0, -12f, 0), 1.2f);
        OBox(dark, P(1.18f, 0.2f, 0.36f), new Vector3(0.05f, 0.05f, 0.05f), rot);
        OBox(card, P(1.5f, 0.27f, 0.11f), new Vector3(0.3f, 0.22f, 0.26f), rot * Quaternion.Euler(0, 5f, 0), 0.7f);
        red.Emit(name + " Toolbox", go.transform, Flat("SM_ToolboxRed", new Color(0.6f, 0.09f, 0.08f), 0.45f));
        steel.Emit(name + " Tins", go.transform, MSteel, false);
        blue.Emit(name + " Can", go.transform, Body("fit_fridge_blue"), false);
        card.Emit(name + " Carton", go.transform, MCard, false);
        dark.Emit(name + " Dark", go.transform, MDark, false);
    }
}
