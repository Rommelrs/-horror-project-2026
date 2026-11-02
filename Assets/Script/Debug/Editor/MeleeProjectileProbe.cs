using ToolBox.Pools;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode test: bag projectiles thrown at Khalid with the pipe raised, the swing timed so it meets them (or not).
/// MeleeProjectileProbe.Run(prefabPath, throws, swing) from a RunCommand; logs "MPROBE" (per throw: batted or not, and
/// what the projectile hit) to Editor.log. swing = false throws without swinging (control: the bags should land / hit him).
/// Questioners are switched off for the run so nothing interferes.
/// </summary>
public static class MeleeProjectileProbe
{
    static Projectile prefab;
    static int throwsLeft, thrown, batted;
    static bool swing, clickFrame;
    static Mouse mouse;
    static Keyboard kb;
    static Projectile current;
    static bool clicked;
    static float nextThrow, until;
    static int startHits;
    static System.Text.StringBuilder sb;
    static InputSettings.BackgroundBehavior oldBg;
    static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    static GameObject[] disabled;

    public static string Run(string prefabPath, int throws, bool doSwing)
    {
        if (!EditorApplication.isPlaying || PlayerMeleeWeapon.instance == null) return "not playing";
        prefab = AssetDatabase.LoadAssetAtPath<Projectile>(prefabPath);
        if (prefab == null) return "no projectile at " + prefabPath;
        throwsLeft = throws; thrown = batted = 0; swing = doSwing; current = null;
        sb = new System.Text.StringBuilder();
        startHits = PlayerMeleeWeapon.instance.ProjectileHits;
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var q in Object.FindObjectsByType<QuestionerEnemy>(FindObjectsSortMode.None)) { list.Add(q.gameObject); q.gameObject.SetActive(false); }
        disabled = list.ToArray();

        oldBg = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        mouse = InputSystem.AddDevice<Mouse>("MPM" + Random.Range(0, 99999));
        kb = InputSystem.AddDevice<Keyboard>("MPK" + Random.Range(0, 99999));
        nextThrow = Time.time + 1.6f;           // time to take the pipe out and raise it
        until = Time.time + 1.6f + throws * 3f + 2f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "throwing " + throws + " x " + prefab.name + (swing ? " (swinging)" : " (no swing)");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }
        var mw = PlayerMeleeWeapon.instance;
        var p = Player.instance;

        // pipe out (key 3) and raised (right mouse held), a left click for one frame = a swing
        if (!PlayerMeleeWeapon.Equipped) InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Digit3));
        else InputSystem.QueueStateEvent(kb, new KeyboardState());
        var ms = new MouseState().WithButton(MouseButton.Right);
        if (clickFrame) { ms = ms.WithButton(MouseButton.Left); clickFrame = false; }
        InputSystem.QueueStateEvent(mouse, ms);

        if (current == null && throwsLeft > 0 && Time.time >= nextThrow)
        {
            // from 8 m in front of him, aimed at his chest
            Vector3 from = p.transform.position + p.transform.forward * 8f + Vector3.up * 1.6f;
            current = prefab.gameObject.Reuse(from, Quaternion.identity).GetComponent<Projectile>();
            current.ShootProjectileAtPosition(p.transform.position + Vector3.up * 1.2f);
            throwsLeft--; thrown++; clicked = false;
            sb.AppendFormat("  throw {0} (pipe ready={1})\n", thrown, PlayerMeleeWeapon.Ready);
        }

        if (current != null)
        {
            if (!current.gameObject.activeInHierarchy)
            {
                bool hit = mw.ProjectileHits - startHits > batted;
                if (hit) batted++;
                sb.AppendFormat("  throw {0}: {1}\n", thrown, hit ? "BATTED out of the air" : "landed / hit him");
                current = null;
                nextThrow = Time.time + 1.5f;
            }
            else if (swing && !clicked && PlayerMeleeWeapon.Ready && !PlayerMeleeWeapon.Attacking)
            {
                // swing so the strike meets it ~1.6 m out (strike starts ~0.1 s after the click)
                Vector3 to = current.transform.position - p.transform.position; to.y = 0f;
                var rb = current.GetComponent<Rigidbody>();
                Vector3 v = rb.linearVelocity; v.y = 0f;
                if (to.magnitude - v.magnitude * 0.16f <= 1.6f)
                {
                    clicked = true; clickFrame = true;
                    sb.AppendFormat("  swing at {0:F2} m\n", to.magnitude);
                }
            }
        }

        if (Time.time >= until || (throwsLeft == 0 && current == null && Time.time >= nextThrow)) Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (kb != null) { InputSystem.RemoveDevice(kb); kb = null; }
        InputSystem.settings.backgroundBehavior = oldBg;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        if (disabled != null) foreach (var g in disabled) if (g != null) g.SetActive(true);
        disabled = null;
        Debug.Log("MPROBE thrown=" + thrown + " batted=" + batted + " swing=" + swing + "\n" + sb);
    }
}
