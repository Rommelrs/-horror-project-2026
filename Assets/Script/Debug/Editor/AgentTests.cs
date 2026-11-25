#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>In-Play checks driven from automation (Claude). Results go to the log as "T..." lines.</summary>
public static partial class AgentTests
{
    static IEnumerator running;
    static double waitUntil;
    static MouseState mouse; static KeyboardState keys; static bool driveMouse, driveKeys;

    public static void Run(string name)
    {
        var m = typeof(AgentTests).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        running = (IEnumerator)m.Invoke(null, null);
        waitUntil = 0; driveMouse = driveKeys = false;
        // keys reach the game even while another window has the focus; put back in Stop
        var s = InputSystem.settings;
        if (!saved) { saved = true; savedEditor = s.editorInputBehaviorInPlayMode; savedBack = s.backgroundBehavior; }
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.runInBackground = true;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Debug.Log("T start " + name);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        if (driveMouse && Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, mouse);
        if (driveKeys && Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, keys);
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        try
        {
            if (!running.MoveNext()) { Stop(); Debug.Log("T end"); return; }
            if (running.Current is float f) waitUntil = EditorApplication.timeSinceStartup + f;
        }
        catch (Exception e) { Debug.Log("T EXC " + e); Stop(); }
    }

    static bool saved;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditor;
    static InputSettings.BackgroundBehavior savedBack;

    static void Stop() { if (saved) { InputSystem.settings.editorInputBehaviorInPlayMode = savedEditor; InputSystem.settings.backgroundBehavior = savedBack; saved = false; } EditorApplication.update -= Tick; driveMouse = driveKeys = false; if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState()); if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); }

    static void Hold(bool rmb, bool lmb) { driveMouse = true; mouse = new MouseState(); if (rmb) mouse = mouse.WithButton(MouseButton.Right); if (lmb) mouse = mouse.WithButton(MouseButton.Left); }
    static void Press(params Key[] k) { driveKeys = true; keys = new KeyboardState(k); }
    static void Shot(string n) => ScreenCapture.CaptureScreenshot(AgentTools.Dir + n);
    static void Jump(int no) => Resources.FindObjectsOfTypeAll<TaskJump>()[0].Jump(no);
    static Player P => Player.instance;
    static string Hp(Health h) => h.GetHealthValue().ToString();
    static double Now => EditorApplication.timeSinceStartup;

    static IEnumerator T6()
    {
        while (P == null) yield return 0.2f;
        yield return 2f;
        Jump(6);
        double t0 = Now;
        while (!GrabCounterTutorial.Holding) { if (Now - t0 > 45) { Debug.Log("T6 never held"); yield break; } yield return null; }
        QuestionerEnemy q = null;
        Debug.Log("T6 HOLD after " + (Now - t0).ToString("F1") + " s");
        foreach (var e in QuestionerEnemy.All) if (Vector3.Distance(e.transform.position, P.transform.position) < 3f) { q = e; Debug.Log("T6 dist " + Vector3.Distance(e.transform.position, P.transform.position) + " " + e.combatState.PhaseName); }
        string hp = Hp(P.health);
        yield return 3f;
        Shot("t6a.png"); yield return 0.3f;
        Debug.Log("T6 still " + Vector3.Distance(q.transform.position, P.transform.position) + " " + q.combatState.PhaseName);
        Hold(true, false); yield return 2f;
        Debug.Log("T6 ready " + PlayerMeleeWeapon.Ready);
        Shot("t6b.png"); yield return 0.3f;
        Hold(true, true); yield return 0.2f; Hold(true, false);
        t0 = Now;
        while (GrabCounterTutorial.Holding && Now - t0 < 6) yield return null;
        Debug.Log("T6 released " + (Now - t0).ToString("F2") + " s after the press; he is " + q.combatState.PhaseName + " hp " + Hp(q.health) + " khalid " + hp + "->" + Hp(P.health) + " flag " + StoryFlags.Has(GrabCounterTutorial.Flag));
        Hold(false, false); yield return 2f;
        Shot("t6c.png"); yield return 0.5f;
        Debug.Log("T6 vol " + AudioListener.volume);
    }

    static IEnumerator T34()
    {
        while (P == null) yield return 0.2f;
        yield return 2f;
        Jump(34);
        var cc = PlayerCloseCombat.instance;
        double t0 = Now;
        while (!cc.IsGrabbed) { if (Now - t0 > 45) { Debug.Log("T34 not grabbed"); yield break; } yield return null; }
        string hp = Hp(P.health);
        var qte = UnityEngine.Object.FindFirstObjectByType<GrabEscapeQTE>();
        yield return 4f;
        Debug.Log("T34 lesson " + qte.Lesson + " marker " + qte.Marker + " zone " + qte.ZoneStart + "+" + qte.ZoneWidth + " state " + qte.State);
        Shot("t34a.png"); yield return 0.4f;
        Press(Key.E); yield return 0.15f; Press(); yield return 3f;
        Debug.Log("T34 slider " + qte.Slider + " marker " + qte.Marker + " state " + qte.State);
        Press(Key.E); yield return 0.15f; Press(); yield return 1f;
        Debug.Log("T34 state " + qte.State + " grabbed " + cc.IsGrabbed + " hp " + hp + "->" + Hp(P.health) + " flag " + StoryFlags.Has(GrabEscapeQTE.LessonFlag));
    }

    static IEnumerator T12()
    {
        while (P == null) yield return 0.2f;
        yield return 1f;
        Jump(12); yield return 0.5f;
        var cs = CameraSystem.Instance;
        foreach (bool free in new[] { true, false })
        {
            cs.SetFreeCamera(free); yield return 1f;
            float before = P.transform.eulerAngles.y;
            if (free) for (int i = 0; i < 60; i++) { driveMouse = true; mouse = new MouseState { delta = new Vector2(25f, 0f) }; yield return null; }
            driveMouse = true; mouse = new MouseState(); yield return 0.3f;
            float cam = Camera.main.transform.eulerAngles.y;
            Hold(true, false); yield return 1f;
            Debug.Log("T12 free " + free + " khalid " + before.ToString("F0") + " camera was " + cam.ToString("F0") + " -> aiming " + P.playerWeaponSystem.isAiming + " khalid " + P.transform.eulerAngles.y.ToString("F0") + " cam " + Camera.main.transform.eulerAngles.y.ToString("F0"));
            Hold(false, false); yield return 1f;
        }
    }

    static IEnumerator T40()
    {
        while (P == null) yield return 0.2f;
        Jump(40); yield return 2f;
        var g = GameObject.Find("Park Gate").transform;
        Debug.Log("T40 owns " + PlayerMeleeWeapon.OwnsPipe + " open " + g.Find("Open").gameObject.activeSelf + " closed " + g.Find("Closed").gameObject.activeSelf);
        Press(Key.W); yield return 6f; Press();
        Debug.Log("T40 walked to x " + P.transform.position.x + " (gate x " + g.position.x + ")");
        Shot("t40.png"); yield return 0.5f;
    }

    static IEnumerator T7()
    {
        while (P == null) yield return 0.2f;
        yield return 1f;
        Jump(7); yield return 4.5f;
        Debug.Log("T7 open " + StabilityTutorial.Open + " paused " + GameManager.IsPaused);
        Shot("t7.png"); yield return 0.5f;
        Press(Key.Space); yield return 0.1f; Press(); yield return 1f;
        Debug.Log("T7 closed " + !StabilityTutorial.Open + " paused " + GameManager.IsPaused + " ts " + Time.timeScale);
    }

    static IEnumerator All1()
    {
        foreach (var t in new[] { T7(), T12(), T40(), T6(), T34() })
            while (t.MoveNext()) yield return t.Current;
    }
}
#endif
