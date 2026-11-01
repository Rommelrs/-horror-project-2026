using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections.Generic;

// Play-mode test harness for the pipe melee (used from the editor while tuning the clips): MeleeProbe.Run(spec, dir).
// spec: "aim,key[+key],seconds,label,captureEvery[,attack];..." e.g. "0,None,2,carry_idle,0;1,D,2,ready_strafeR,6"
// Melee probe: runs a list of steps (aim held, key held, seconds, label, capture every N frames (0 = none)), measures per frame:
//  neck   = change of the neck.x -> head.x distance (attached head = constant)          [mm, max over the step]
//  shld   = change of shoulder.r -> arm_stretch.r distance (attached arm = constant)    [mm]
//  jitter = head angular acceleration relative to the player root, deg/frame^2 at 60 fps (95th percentile and max)
//  pipe   = left fist distance from the pipe line [cm, max]
public static class MeleeProbe
{
    struct Step { public bool aim; public Key[] key; public float secs; public string label; public int every; public bool attack; }
    static Step[] steps;
    static string Dir, tag;
    static Mouse mouse; static Keyboard kb; static int si; static float stepT; static int frame;
    static Transform hl, pipe, head, neck, shoulder, arm, root; static PlayerMeleeWeapon mw;
    static InputSettings.BackgroundBehavior oldBg; static Camera cam; static RenderTexture rt;
    static System.Text.StringBuilder sb;
    static float neck0, shld0, maxNeck, maxShld, maxPipe, minClear, maxSpread; static Transform[] skin; static Transform chest; static string spreadBone; static List<float> jit = new List<float>();
    static Quaternion q1, q2; static int qn; static bool started;
    static Vector3 home; static Quaternion homeRot; static Vector3 movedFrom; static bool movedSet;

    public static string Run(string spec, string dir)
    {
        if (!EditorApplication.isPlaying || PlayerMeleeWeapon.instance == null) return "not playing";
        var list = new List<Step>();
        foreach (var part in spec.Split(';'))
        {
            var f = part.Split(',');
            if (f.Length < 5) continue;
            list.Add(new Step { aim = f[0] == "1", key = System.Array.ConvertAll(f[1].Split('+'), k => (Key)System.Enum.Parse(typeof(Key), k)), secs = float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                                label = f[3], every = int.Parse(f[4]), attack = f.Length > 5 && f[5] == "1" });
        }
        steps = list.ToArray();
        Dir = dir.TrimEnd('/') + "/"; tag = System.IO.Path.GetFileName(dir.TrimEnd('/'));
        mw = PlayerMeleeWeapon.instance; root = mw.transform;
        foreach (var t in mw.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "hand.l") hl = t; if (t.name == "MeleePipe") pipe = t; if (t.name == "head.x") head = t;
            if (t.name == "neck.x") neck = t; if (t.name == "spine_03.x") chest = t; if (t.name == "shoulder.r") shoulder = t; if (t.name == "arm_stretch.r") arm = t;
        }
        skin = mw.GetComponentInChildren<SkinnedMeshRenderer>().bones;
        // every step starts in the middle of the open field facing +Z (cameras clear of walls, no running into them)
        home = root.position; homeRot = Quaternion.identity;
        var floor = GameObject.Find("QuestionerTestBench/Geometry/A Open Field/Floor");
        if (floor != null) { var b = floor.GetComponent<Renderer>().bounds; home = new Vector3(b.center.x, b.max.y, b.center.z - 4f); }
        System.IO.Directory.CreateDirectory(Dir);
        oldBg = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        mouse = InputSystem.AddDevice<Mouse>("PM" + Random.Range(0, 99999)); kb = InputSystem.AddDevice<Keyboard>("PK" + Random.Range(0, 99999));
        var go = new GameObject("ProbeCam"); cam = go.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 38; cam.nearClipPlane = 0.05f;
        rt = new RenderTexture(300, 380, 24); cam.targetTexture = rt;
        sb = new System.Text.StringBuilder(); si = -1; started = false; frame = 0;
        if (!PlayerMeleeWeapon.Equipped) mw.Select();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "probe started: " + steps.Length + " steps";
    }
    static void In(bool aim, bool fire, Key[] k)
    {
        var ms = new MouseState(); if (aim) ms = ms.WithButton(MouseButton.Right); if (fire) ms = ms.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, ms);
        InputSystem.QueueStateEvent(kb, k == null || k[0] == Key.None ? new KeyboardState() : new KeyboardState(k));
    }
    static void Snap(string name)
    {
        Vector3 focus = root.position + Vector3.up * 1.2f;
        var views = new[] { root.forward * 0.85f - root.right * 0.5f, root.right, -root.forward * 0.6f + root.right * 0.4f };
        for (int v = 0; v < views.Length; v++)
        {
            cam.transform.position = focus + views[v].normalized * 2.3f + Vector3.up * 0.15f; cam.transform.LookAt(focus); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev; System.IO.File.WriteAllBytes(Dir + name + "_v" + v + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }
    }
    static void Begin(int i)
    {
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.SetPositionAndRotation(home, homeRot);
        if (cc != null) cc.enabled = true;
        var st = root.GetComponent<PlayerStamina>();
        if (st != null) { var f = typeof(PlayerStamina).GetField("stamina", (System.Reflection.BindingFlags)36); if (f != null) f.SetValue(st, 100f); }
        si = i; stepT = 0f; frame = 0; maxNeck = maxShld = maxPipe = maxSpread = 0f; minClear = 9f; spreadBone = ""; movedSet = false; jit.Clear(); qn = 0;
        neck0 = (head.position - neck.position).magnitude; shld0 = (arm.position - shoulder.position).magnitude;
        In(steps[i].aim, steps[i].attack, steps[i].key);
    }
    static void End()
    {
        jit.Sort();
        float p95 = jit.Count > 0 ? jit[Mathf.Min(jit.Count - 1, (int)(jit.Count * 0.95f))] : 0f, mx = jit.Count > 0 ? jit[jit.Count - 1] : 0f;
        sb.AppendFormat("{0,-14} neck {1,5:F1}mm  shld {2,5:F1}mm  jitter p95 {3,5:F2} max {4,5:F2}  pipe {5,4:F1}cm  headClear {6,4:F1}cm  spread {7:F2}m ({8})  moved {9:F2}m\n", steps[si].label, maxNeck * 1000, maxShld * 1000, p95, mx, maxPipe * 100, minClear * 100, maxSpread, spreadBone, movedSet ? Vector3.ProjectOnPlane(root.position - movedFrom, Vector3.up).magnitude : 0f);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        if (!started) { started = true; Begin(0); return; }
        var s = steps[si];
        if (s.attack && frame == 2) In(s.aim, false, s.key);
        stepT += Time.deltaTime; frame++;
        // metrics (skip the first 0.4 s of a step: that's the transition into it)
        if (stepT > 0.4f)
        {
            if (!movedSet) { movedFrom = root.position; movedSet = true; }
            maxNeck = Mathf.Max(maxNeck, Mathf.Abs((head.position - neck.position).magnitude - neck0));
            maxShld = Mathf.Max(maxShld, Mathf.Abs((arm.position - shoulder.position).magnitude - shld0));
            Vector3 F = hl.position + hl.up * 0.065f - hl.forward * 0.025f; Vector3 ax = pipe.up, d = F - pipe.position;
            maxPipe = Mathf.Max(maxPipe, (d - Vector3.Dot(d, ax) * ax).magnitude);
            // every skinned bone stays within reach of his chest (catches a bone flying off = stretched mesh)
            foreach (var b in skin)
            {
                if (b == null || b.name == "c_pos" || b.name == "c_traj") continue;
                float sp = (b.position - chest.position).magnitude;
                if (sp > maxSpread) { maxSpread = sp; spreadBone = b.name; }
            }
            if (pipe.gameObject.activeInHierarchy)
            {
                // pipe (15 cm below the grip to 66 cm above it) vs the middle of his skull
                Vector3 hc = head.position + head.up * 0.1f, a0 = pipe.position - ax * 0.15f;
                float tt = Mathf.Clamp(Vector3.Dot(hc - a0, ax), 0f, 0.81f);
                minClear = Mathf.Min(minClear, (a0 + ax * tt - hc).magnitude);
            }
            Quaternion q = Quaternion.Inverse(root.rotation) * head.rotation;
            if (qn >= 2)
            {
                // second difference of the head's rotation (relative to his body) = angular acceleration
                Quaternion v1 = q * Quaternion.Inverse(q1), v0 = q1 * Quaternion.Inverse(q2);
                float k = (Time.deltaTime > 1e-4f ? (1f / 60f) / Time.deltaTime : 1f);
                jit.Add(Quaternion.Angle(v1, v0) * k * k);
            }
            q2 = q1; q1 = q; qn++;
        }
        else { neck0 = (head.position - neck.position).magnitude; shld0 = (arm.position - shoulder.position).magnitude; }
        if (s.every > 0 && frame % s.every == 0) Snap(s.label + "_" + frame.ToString("000"));
        if (stepT >= s.secs)
        {
            End();
            if (si + 1 < steps.Length) Begin(si + 1);
            else
            {
                In(false, false, null);
                EditorApplication.update -= Tick; InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(kb);
                InputSystem.settings.backgroundBehavior = oldBg; Object.DestroyImmediate(cam.gameObject); rt.Release();
                Debug.Log("MPROBE_RESULT " + tag + "\n" + sb);
            }
        }
    }
}
