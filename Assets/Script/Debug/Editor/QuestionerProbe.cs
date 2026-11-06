using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode test harness for the Questioner spec moveset (bench duel). QuestionerProbe.Run(test, dir) from a RunCommand:
///   "far"     - Khalid stands 7 m in front, no input, 25 s: grab (instant bite) / scream / swipe flow
///   "counter" - pipe raised (fake right mouse); swings (left click) timed so the hit lands in the grab rush: he stumbles back
///   "late"    - like counter, but the swing is too late: he bites
///   "hurt"    - pipe raised, swings whenever he's in reach between moves or clawing: recoil -> backs off -> the grab
///   "ads"     - gun out (key 2), aiming down the sights from 8 m: he should answer with the scream; captures the ADS view
///   "scream"  - stays 3.3-4 m away; grabs the player's view (screen echo) and a side view (sound-wave shells) in the scream
///   "stagger" - mouth hit -> kneel; melee-style body hit (downed damage check); second mouth hit -> kneeling death
/// Logs "QPROBE <test>" + every phase change (time, distance, Khalid HP, grabbed, speed multiplier) to Editor.log, and
/// captures 3 views (front / side / Khalid's shoulder) every `every` frames into dir (0 = no captures).
/// </summary>
public static class QuestionerProbe
{
    static string test, dir;
    static int every;
    static QuestionerEnemy q;
    static Keyboard kb;
    static Mouse mouse;
    static bool aimHeld, clickThisFrame;
    static float contactDelay;
    static int counters, bites, hurts, swings;
    static float screamAt; static int adsShots;
    static float t0, until;
    static string last;
    static System.Text.StringBuilder sb;
    static int frame;
    static Camera cam;
    static RenderTexture rt;
    static InputSettings.BackgroundBehavior oldBg;
    static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    static int step;
    static float stepT;
    static bool pressedThisFrame;
    static float minSpeedMul = 1f;
    static QuestionerTestBench bench;
    static Transform jaw, qHead;
    static Quaternion lastJaw;
    static float maxJawStep, sumJawStep; static int jawN;
    static bool camShot;
    static float lastMeleeT;
    static bool oldGod;

    public static string Run(string testName, string outDir, int captureEvery = 0, float seconds = 25f)
    {
        if (!EditorApplication.isPlaying) return "not playing";
        q = null;
        foreach (var e in Object.FindObjectsByType<QuestionerEnemy>(FindObjectsSortMode.None)) if (e.gameObject.activeInHierarchy) q = e;
        if (q == null) return "no active Questioner";
        test = testName; dir = outDir.TrimEnd('/') + "/"; every = captureEvery;
        if (System.IO.Directory.Exists(dir)) foreach (var f in System.IO.Directory.GetFiles(dir, "*.png")) System.IO.File.Delete(f);
        System.IO.Directory.CreateDirectory(dir);
        sb = new System.Text.StringBuilder();
        last = ""; frame = 0; step = 0; stepT = 0f; minSpeedMul = 1f;
        t0 = Time.time; until = t0 + seconds;

        bench = Object.FindFirstObjectByType<QuestionerTestBench>();
        if (bench != null) { oldGod = bench.godMode; bench.godMode = false; }
        var h = Player.instance.health;
        h.Invincible = false;
        h.SetMaxHealth(1000);
        jaw = null; qHead = null; maxJawStep = 0f; sumJawStep = 0f; jawN = 0; camShot = false; holdFrames = 0; lastMeleeT = -99f;
        foreach (var t in q.GetComponentsInChildren<Transform>()) { if (t.name == "Jaw") jaw = t; if (t.name == "Head") qHead = t; }
        if (jaw != null) lastJaw = Quaternion.Inverse(qHead.rotation) * jaw.rotation;
        Place(test == "stagger" || test == "gunstagger" ? 2.5f : test == "melee" || test == "hurt" ? 1.6f : test == "scream" ? 3.7f : test == "ads" ? 8f : 7.0f);
        screamAt = -99f; adsShots = 0;
        counters = bites = hurts = swings = 0;

        oldBg = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        // the fake devices only reach the game while the Game view has focus otherwise
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        kb = InputSystem.AddDevice<Keyboard>("QPK" + Random.Range(0, 99999));
        mouse = InputSystem.AddDevice<Mouse>("QPM" + Random.Range(0, 99999));
        aimHeld = test == "counter" || test == "late" || test == "hurt";
        clickThisFrame = false;
        // swing press -> contact frame (s)
        var mw = PlayerMeleeWeapon.instance;
        contactDelay = 0.2f;
        if (mw != null)
        {
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var clip = typeof(PlayerMeleeWeapon).GetField("attackClip", bf)?.GetValue(mw) as AnimationClip;
            float speed = (float)(typeof(PlayerMeleeWeapon).GetField("attackSpeed", bf)?.GetValue(mw) ?? 1.7f);
            float at = (float)(typeof(PlayerMeleeWeapon).GetField("contactAt", bf)?.GetValue(mw) ?? 0.175f);
            if (clip != null) contactDelay = at * clip.length / speed;
        }
        if (every > 0)
        {
            var go = new GameObject("QProbeCam");
            cam = go.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 40; cam.nearClipPlane = 0.05f;
            rt = new RenderTexture(360, 300, 24); cam.targetTexture = rt;
        }
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "probe " + test + " started vs " + q.name;
    }

    /// <summary>Both in the middle of the bench's open field, d metres apart, facing each other (the Questioner warped, the fight
    /// restarted so the timers start clean).</summary>
    static void Place(float d)
    {
        var p = Player.instance;
        Vector3 c = p.transform.position;
        var floor = GameObject.Find("QuestionerTestBench/Geometry/A Open Field/Floor");
        if (floor != null) { var b = floor.GetComponent<Renderer>().bounds; c = new Vector3(b.center.x, b.max.y, b.center.z); }
        Vector3 ppos = c + Vector3.back * (d * 0.5f);
        Vector3 qpos = c + Vector3.forward * (d * 0.5f);
        var cc = p.controller;
        cc.enabled = false;
        p.transform.position = ppos;
        p.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        cc.enabled = true;
        if (q.agent.enabled) q.agent.Warp(qpos); else q.transform.position = qpos;
        q.transform.rotation = Quaternion.LookRotation(Vector3.back);
        if (q.stateMachine.CurrentState == q.combatState) q.stateMachine.ChangeState(q.chaseState);
    }

    static void Press(Key k)
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState(k));
        pressedThisFrame = true;
    }

    static void Release()
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState());
    }

    /// <summary>The fake mouse: right held = pipe raised, a left click for one frame = a swing.</summary>
    static void MouseTick()
    {
        var ms = new MouseState();
        if (aimHeld) ms = ms.WithButton(MouseButton.Right);
        if (clickThisFrame) ms = ms.WithButton(MouseButton.Left);
        clickThisFrame = false;
        InputSystem.QueueStateEvent(mouse, ms);
    }

    static void Swing(string why)
    {
        clickThisFrame = true;
        swings++;
        sb.AppendFormat("  swing ({0})" + NL, why);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || q == null) { Finish(); return; }
        frame++;
        if (pressedThisFrame) { Release(); pressedThisFrame = false; }
        var p = Player.instance;
        if (p.health.GetHealthValue() < 200) p.health.SetMaxHealth(1000);
        float d = Vector3.Distance(Flat(q.transform.position), Flat(p.transform.position));
        string phase = q.stateMachine.CurrentState == q.combatState ? q.combatState.PhaseName : q.stateMachine.CurrentState.GetType().Name;
        minSpeedMul = Mathf.Min(minSpeedMul, PlayerCloseCombat.SpeedMultiplier < 0.01f ? 1f : PlayerCloseCombat.SpeedMultiplier);
        stepT += Time.deltaTime;

        if (phase != last)
        {
            if (phase == "Stumble") counters++;
            if (phase == "Bite") bites++;
            if (phase == "Hurt") hurts++;
        }
        bool ready = PlayerMeleeWeapon.Ready && !PlayerMeleeWeapon.Attacking;
        // the pipe tests: take the pipe out (key 3) if a gun is in his hands
        if (aimHeld && !PlayerMeleeWeapon.Equipped && frame % 20 == 5) InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Digit1));
        else if (aimHeld && frame % 20 == 9) Release();
        switch (test)
        {
            case "counter":
            case "late":
            {
                // the hit should land as he's ~1.9 m out (counter) / when he's already on Khalid (late)
                float landAt = test == "counter" ? 1.9f : 0.6f;
                if (phase == "GrabRush" && ready && step == 0 && d - q.combatState.RushSpeed * contactDelay <= landAt)
                {
                    step = 1;
                    Swing(string.Format("rush {0:F2} m/s at d={1:F2}, lands ~{2:F2}", q.combatState.RushSpeed, d, d - q.combatState.RushSpeed * contactDelay));
                }
                if (phase != "GrabRush" && phase != "GrabWindup") step = 0;
                break;
            }
            case "ads":
            {
                var ws = p.playerWeaponSystem;
                // key 2 (the pistol) held a few frames, a moment after the fake keyboard appeared
                if (step == 0 && stepT > 0.3f) { InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Digit2)); step = 1; stepT = 0f; }
                else if (step == 1 && stepT > 0.15f) { Release(); step = 2; stepT = 0f; }
                else if (step == 2 && stepT > 0.8f) { ws.aimingOverrideForTesting = true; step = 3; sb.AppendFormat("  ADS on, pipe equipped={0}" + NL, PlayerMeleeWeapon.Equipped); }
                if (phase == "ScreamActive" && screamAt < 0f) screamAt = Time.time;
                // Khalid stands his ground: keep the distance if he walks in
                if (d < 6f && phase == "Free") Place(8f);
                if (screamAt > 0f && every > 0)
                {
                    float[] at = { 0.3f, 1.5f, 3.0f, 4.5f, 6.2f };
                    if (adsShots < at.Length && Time.time - screamAt >= at[adsShots])
                    {
                        ShotMain(string.Format("ads_{0:0.0}s_aiming{1}", at[adsShots], ws.isAiming));
                        adsShots++;
                    }
                }
                break;
            }
            case "hurt":
                if (ready && d < 1.9f && (phase == "Free" || phase == "Release" || phase == "Swipe") && Time.time - lastMeleeT > 0.4f)
                {
                    lastMeleeT = Time.time;
                    Swing(string.Format("d={0:F2} phase={1}", d, phase));
                }
                break;
            case "scream":
                // keep him inside scream range (3-4 m): Khalid backs off when it closes in
                if (d < 3.3f && phase == "Free") Place(3.7f);
                break;
            case "gunstagger":
                // the gun's mouth hit: weak point flag + 50 x 4 damage
                if (step == 0 && Time.time - t0 > 1.0f)
                {
                    step = 1; stepT = 0f;
                    int before = q.health.GetHealthValue();
                    q.health.isDamageByWeakpointHit = true;
                    q.health.Damage(200);
                    sb.AppendFormat("  gun mouth shot (200): hp {0} -> {1} dead={2}" + NL, before, q.health.GetHealthValue(), q.health.IsDead);
                }
                break;
            case "melee":
                // a pipe strike whenever he's within reach (at most every 1.2 s)
                if (d < 1.9f && Time.time - lastMeleeT > 1.2f && q.stateMachine.CurrentState == q.combatState && !q.combatState.IsGrabbing)
                {
                    lastMeleeT = Time.time;
                    q.pendingMeleeHit = true;
                    q.health.Damage(1);
                    sb.AppendFormat("  melee strike at d={0:F2} phase={1}" + NL, d, phase);
                }
                break;
            case "stagger":
                if (step == 0 && Time.time - t0 > 1.0f)
                {
                    step = 1; stepT = 0f;
                    q.health.isDamageByWeakpointHit = true;
                    q.health.Damage(25);
                    sb.AppendFormat("  mouth hit 1 -> hp {0}\n", q.health.GetHealthValue());
                }
                else if (step == 1 && stepT > 1.2f)
                {
                    step = 2; stepT = 0f;
                    int before = q.health.GetHealthValue();
                    bool downed = q.IsDownedForMelee;
                    q.health.Damage(downed ? 40 : 20);
                    sb.AppendFormat("  melee while staggered: downed={0} {1} -> {2}\n", downed, before, q.health.GetHealthValue());
                }
                else if (step == 2 && stepT > 0.8f)
                {
                    step = 3; stepT = 0f;
                    q.health.isDamageByWeakpointHit = true;
                    q.health.Damage(25);
                    sb.AppendFormat("  mouth hit 2 -> hp {0} dead={1}\n", q.health.GetHealthValue(), q.health.IsDead);
                }
                break;
        }

        if (jaw != null && qHead != null)
        {
            Quaternion j = Quaternion.Inverse(qHead.rotation) * jaw.rotation;
            float step_ = Quaternion.Angle(j, lastJaw);
            lastJaw = j;
            if (frame > 2) { maxJawStep = Mathf.Max(maxJawStep, step_); sumJawStep += step_; jawN++; }
        }
        if (test == "scream" && phase == "ScreamActive" && every > 0 && holdFrames++ == 12)
        {
            ShotMain("screamview_" + frame.ToString("0000"));
            Snap("screamside_" + frame.ToString("0000"));
        }
        // the grab sequence through the player's camera (hold -> scream -> bite -> push-off)
        bool grabSeq = phase == "GrabHold" || phase == "GrabScream" || phase == "Bite" || (phase == "Stumble" && p.GetComponent<PlayerCloseCombat>().IsPushingOff);
        if (grabSeq && every > 0 && ++holdFrames % 9 == 0) ShotMain("grabcam_" + frame.ToString("0000") + "_" + phase);
        if (phase != last)
        {
            sb.AppendFormat("  {0,6:F2}s {1,-20} d={2:F2} hp={3} grabbed={4} speedMul={5:F2} jaw-open\n", Time.time - t0, phase, d, p.health.GetHealthValue(), p.isGrabbed, PlayerCloseCombat.SpeedMultiplier);
            last = phase;
        }
        if (every > 0 && frame % every == 0) Snap(phase + "_" + frame.ToString("0000"));
        MouseTick();
        if (Time.time >= until) Finish();
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    const string NL = "\n";
    static int holdFrames;

    /// <summary>What the player sees (Camera.main).</summary>
    static void ShotMain(string name)
    {
        var main = Camera.main;
        if (main == null) return;
        var t = new RenderTexture(640, 360, 24);
        var old = main.targetTexture;
        main.targetTexture = t; main.Render(); main.targetTexture = old;
        var prev = RenderTexture.active; RenderTexture.active = t;
        var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        System.IO.File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); t.Release();
    }

    static void Snap(string name)
    {
        var p = Player.instance.transform;
        Vector3 mid = (q.transform.position + p.position) * 0.5f + Vector3.up * 1.1f;
        Vector3 axis = Flat(p.position - q.transform.position).normalized;
        if (axis.sqrMagnitude < 0.01f) axis = q.transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, axis);
        var views = new[] { side * 3.4f + Vector3.up * 0.3f, (-axis * 1.0f + side * 0.6f).normalized * 3.6f + Vector3.up * 0.4f, (axis * 1.0f + side * 0.35f).normalized * 3.2f + Vector3.up * 0.5f };
        for (int v = 0; v < views.Length; v++)
        {
            cam.transform.position = mid + views[v];
            cam.transform.LookAt(mid);
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(dir + name + "_v" + v + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (kb != null) { Release(); InputSystem.RemoveDevice(kb); kb = null; }
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (Player.instance != null && Player.instance.playerWeaponSystem != null) Player.instance.playerWeaponSystem.aimingOverrideForTesting = false;
        InputSystem.settings.backgroundBehavior = oldBg;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        if (bench != null) bench.godMode = oldGod;
        if (cam != null) { Object.DestroyImmediate(cam.gameObject); rt.Release(); cam = null; }
        Debug.Log("QPROBE " + test + " swings=" + swings + " counters=" + counters + " bites=" + bites + " hurts=" + hurts + " contactDelay=" + contactDelay.ToString("F2") + " minSpeedMul=" + minSpeedMul.ToString("F2") + " jawStep max=" + maxJawStep.ToString("F2") + " mean=" + (jawN > 0 ? sumJawStep / jawN : 0f).ToString("F3") + " deg/frame\n" + sb);
    }
}
