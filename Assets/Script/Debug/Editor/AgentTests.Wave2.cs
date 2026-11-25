#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class AgentTests
{
    static void Put(Vector3 pos, float yaw)
    {
        P.controller.enabled = false;
        P.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        P.controller.enabled = true;
        P.OnPlayerTelported?.Invoke();
    }

    /// <summary>A line on screen / a note open: gone.</summary>
    static void Clear()
    {
        if (ItemInspectionHandler.instance != null && ItemInspectionHandler.instance.InspectionMenuIsActive()) ItemInspectionHandler.instance.CloseInspectionMenu();
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();
    }

    static IEnumerator T30()
    {
        P.health.Invincible = true;
        foreach (var start in new[] { (new Vector3(-169f, 3.2f, 33f), 90f, "west in"), (new Vector3(-150f, 3.2f, 33f), 270f, "east in"), (new Vector3(-165.5f, 3.4f, 33f), 45f, "along north tank"), (new Vector3(-165.5f, 3.4f, 33f), 135f, "along south tank") })
        {
            Put(start.Item1, start.Item2); yield return 1f; Clear();
            Vector3 a = P.transform.position;
            Press(Key.W); yield return 8f; Press(); yield return 0.3f;
            Debug.Log("T30 " + start.Item3 + ": " + a + " -> " + P.transform.position + " moved " + (P.transform.position - a).magnitude);
        }
    }

    static IEnumerator T42()
    {
        P.health.Invincible = true;
        Put(new Vector3(-83.2f, 1.1f, 50.9f), 270f); yield return 1f; Clear();
        var d = AgentTools.Find("Alley 2 Gameplay/DoorInteractable (Entrance) (2)").GetComponent<DoorInteractable>();
        d.hasDoorLock = false;
        DoorInteractionHandler.instance.DoorInteractionTriggerEnter(d, DoorTriggerType.Exit);
        yield return 0.2f;
        DoorInteractionHandler.instance.InteractWithCurrentDoor();
        yield return 6f;
        Debug.Log("T42 out: pos " + P.transform.position + " yaw " + P.transform.eulerAngles.y + " (outside is west, 270)");
        Shot("t42.png"); yield return 0.5f;
    }

    static IEnumerator T58()
    {
        P.health.Invincible = true;
        Put(new Vector3(-48.3f, 1f, 58.5f), 0f); yield return 1f; Clear();
        Shot("t58a.png"); yield return 0.5f;
        Put(new Vector3(-48.3f, 1f, 63f), 0f); yield return 0.3f;
        Press(Key.W); yield return 0.9f; Press(); yield return 0.35f;
        Shot("t58b.png"); yield return 0.6f;
        var g = AgentTools.Find("Alley 2 Gameplay/EnemyGroup4");
        foreach (var e in g.GetComponentsInChildren<Enemy>(true)) Debug.Log("T58 " + e.name + " active " + e.gameObject.activeInHierarchy + " at " + e.transform.position + " nav " + (e.agent != null && e.agent.isOnNavMesh));
        yield return 1.5f;
        Shot("t58c.png"); yield return 0.5f;
        foreach (var e in g.GetComponentsInChildren<Enemy>(true)) Debug.Log("T58 later " + e.name + " at " + e.transform.position);
    }

    static IEnumerator T49()
    {
        P.health.Invincible = true;
        foreach (var f in new[] { AdelDoorSequence.SeenFlag, AdelWrapperDrop.SeenFlag }) Debug.Log("T49 flag " + f + " " + StoryFlags.Has(f));
        var seq = Object.FindFirstObjectByType<AdelDoorSequence>();
        seq.DebugArmAndJump(); yield return 0.7f; Clear();
        Press(Key.W);
        double t0 = Now;
        while (!AdelDoorSequence.IsPlaying && Now - t0 < 12) yield return null;
        Press();
        Debug.Log("T49 playing " + AdelDoorSequence.IsPlaying);
        var vis = AgentTools.Find("Adel Wrapper Drop/Visual");
        for (int i = 0; i < 8; i++)
        {
            yield return 0.35f;
            Shot("t49_" + i + ".png");
            var a = seq.RunningAdel;
            Debug.Log("T49 t " + (Now - t0).ToString("F1") + " adel " + (a ? a.position.ToString("F1") : "-") + " wrapper " + vis.gameObject.activeSelf + " " + vis.position.ToString("F2"));
        }
        while (AdelDoorSequence.IsPlaying) yield return null;
        yield return 0.5f;
        Press(Key.W);
        t0 = Now;
        while (!AutoInteractCutscene.Running && Now - t0 < 14) yield return null;
        Press();
        Debug.Log("T49 walk-over " + AutoInteractCutscene.Running + " gate shut " + seq.GateShut + " from " + P.transform.position + " frozen " + EnemyFreeze.Active);
        yield return 5f;
        Shot("t49_img.png"); yield return 0.5f;
        Debug.Log("T49 at " + P.transform.position + " paused " + GameManager.IsPaused + " running " + AutoInteractCutscene.Running);
        for (int i = 0; i < 6; i++) { Press(Key.E); yield return 0.15f; Press(); yield return 1.2f; }
        yield return 4f;
        Debug.Log("T49 done: seen " + StoryFlags.Has(AdelWrapperDrop.SeenFlag) + " running " + AutoInteractCutscene.Running + " pauseMove " + P.pauseMovement + " ts " + Time.timeScale + " map " + (MapHandler.instance != null && MapHandler.instance.MapIsActive()));
        Shot("t49_end.png"); yield return 0.5f;
    }

    /// <summary>The table knockdown from the player's eyes.</summary>
    static IEnumerator T25()
    {
        foreach (var it in Resources.LoadAll<Item>("Items")) { if (it.name == "Pistol" && !P.inventory.HasItem(it)) P.inventory.PickUp(it); if (it.name == "Light Ammo") P.inventory.PickUp(it, 40); }
        var first = P;
        Jump(24);
        double t0 = Now;
        while (Now - t0 < 40 && (P == null || P == first)) yield return 0.2f;
        bool down = false;
        double sub = 0;
        while (Now - t0 < 110)
        {
            if (P != null) { P.health.Invincible = true; if (P.playerWeaponSystem != null) P.playerWeaponSystem.EnsureLoaded(8); }
            if (Now > sub && SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) { SubtitleManager.instance.ForceClose(); sub = Now + 1; }
            if (P != null && P.isKnockedDown) { down = true; break; }
            yield return null;
        }
        Debug.Log("T25 down " + down + " after " + (Now - t0).ToString("F1"));
        for (int i = 0; i < 24; i++)
        {
            double until = Now + 0.6;
            while (Now < until)
            {
                P.health.Invincible = true;
                if (SubtitleManager.instance.IsSubtitleBusy()) SubtitleManager.instance.ForceClose();
                yield return null;
            }
            var c = Camera.main;
            Debug.Log("T25 " + i + " cam " + c.transform.position.ToString("F2") + " rot " + c.transform.eulerAngles.ToString("F0") + " fov " + c.fieldOfView.ToString("F0") + " aim " + P.playerWeaponSystem.isAiming + " khalid " + P.transform.position.ToString("F2") + " | " + string.Join(" | ", Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Where(e => Vector3.Distance(e.transform.position, P.transform.position) < 25f).Select(e => { var v = c.WorldToViewportPoint(e.transform.position + Vector3.up); return e.name + " " + e.transform.position.ToString("F1") + " vp " + v.ToString("F2"); })));
            Shot("t25_" + i.ToString("00") + ".png");
        }
    }

    static IEnumerator T35()
    {
        Jump(35); yield return 1.5f;
        P.health.Invincible = false;
        P.health.Damage(9999, true);
        double t0 = Now;
        while (LevelManager.instance == null || !LevelManager.instance.isGameOver) { if (Now - t0 > 12) break; yield return 0.2f; }
        Debug.Log("T35 game over " + LevelManager.instance.isGameOver + " count " + PlayerPrefs.GetInt("DeathHelp.deaths", -1));
        yield return 0.5f;
        CheckpointManager.instance.ContinueFromCheckpoint();
        t0 = Now;
        while (!DeathHelp.Open && Now - t0 < 45) yield return 0.2f;
        yield return 0.8f;
        Debug.Log("T35 panel " + DeathHelp.Open + " after " + (Now - t0).ToString("F1"));
        Shot("t35.png"); yield return 0.5f;
        int before = P.playerWeaponSystem.GetReserveAmmo();
        Press(Key.Enter); yield return 0.2f; Press(); yield return 1f;
        Debug.Log("T35 ammo " + before + " -> " + P.playerWeaponSystem.GetReserveAmmo() + " open " + DeathHelp.Open + " ts " + Time.timeScale);
    }

    static IEnumerator Chain(params string[] names)
    {
        foreach (var n in names)
        {
            var m = typeof(AgentTests).GetMethod(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var it = (IEnumerator)m.Invoke(null, null);
            while (it.MoveNext()) yield return it.Current;
        }
    }

    static IEnumerator All2() => Chain("T58", "T49", "T35");
}
#endif
