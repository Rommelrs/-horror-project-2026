#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only playtest jumps (the whole file is #if UNITY_EDITOR: nothing of it is in a scene or a build).
/// In Play, press ` (the key under Esc), then the number beside a change. Each entry goes when its change is approved, and
/// this file is deleted when the list is empty.
/// </summary>
public class TaskJump : MonoBehaviour
{
    bool open;
    GUIStyle box, hint;
    string note = "";
    float noteUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<TaskJump>() != null) return;
        var go = new GameObject("TaskJump (editor only)") { hideFlags = HideFlags.DontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<TaskJump>();
    }

    static readonly (Key key, string label)[] List =
    {
        (Key.Digit1, "Wardrobe cloth (Block 2): at it, fresh, with the knife + gate key"),
        (Key.Digit2, "Wardrobe cloth: cut it now (hole + duct tape)"),
        (Key.Digit3, "Abu Khalid's door (Block 3, plate 417): press E - \"Not yet...\""),
        (Key.Digit4, "Table knockdown, fresh: Adel drops the barber candy wrapper; standing up saves \"After the table knockdown\""),
        (Key.Digit5, "Adel at the pole, fresh: east of the crossroads (walk west)"),
        (Key.Digit6, "Adel at the pole, fresh: west end of the street (walk east)"),
        (Key.Digit7, "Adel at the pole: kill the Runner (skip the fight)"),
        (Key.Digit8, "Coward meeting, new script (hammer version, from Block 1)"),
        (Key.Digit9, "Adel at the black gate in Block 3 (walk east over the line)"),
        (Key.Digit0, "Khalid invincible on / off"),
        (Key.J, "Continue protection: save here with 5 HP and die - press Continue"),
        (Key.K, "After-store cutscene now: its end saves \"Back from the store\""),
        (Key.L, "Die now (to try Continue from the last checkpoint)"),
        (Key.M, "Swarm, fresh: at the open wardrobe - pick up the tape (E): the tape cutscene, then the swarm"),
        (Key.N, "Swarm: a few steps short of the west door (it locks with a sound + a line)"),
        (Key.P, "Grab escape sliders: the nearest Questioner grabs Khalid now (stability as it is)"),
        (Key.U, "Grab escape sliders at LOW stability (20): the marker runs much faster"),
        (Key.O, "Swarm: the escape zone goes off now - your blocks swap, the camera glances at the alley entrance, Adel calls"),
    };

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.backquoteKey.wasPressedThisFrame) open = !open;
        if (!open) return;
        if (kb.escapeKey.wasPressedThisFrame) { open = false; return; }
        for (int i = 0; i < List.Length; i++)
            if (kb[List[i].key].wasPressedThisFrame) { open = false; Jump(List[i].key); break; }
    }

    void Jump(Key k)
    {
        StopAllCoroutines();
        if (SceneManager.GetActiveScene().name != "Game" || Player.instance == null) { Note("These are in the Game scene."); return; }
        switch (k)
        {
            case Key.Digit1: StartCoroutine(Wardrobe(false)); break;
            case Key.Digit2: StartCoroutine(Wardrobe(true)); break;
            case Key.Digit3: StartCoroutine(AbuKhalidDoor()); break;
            case Key.Digit4: StartCoroutine(FreshKnockdown()); break;
            case Key.Digit5: StartCoroutine(FreshAdel(false)); break;
            case Key.Digit6: StartCoroutine(FreshAdel(true)); break;
            case Key.Digit7: KillRunner(); break;
            case Key.Digit8: StartCoroutine(CowardMeeting()); break;
            case Key.Digit9: AdelDoor(); break;
            case Key.Digit0:
                var h = Player.instance.health;
                h.Invincible = !h.Invincible;
                Note("Khalid invincible: " + (h.Invincible ? "ON" : "off"), 3f);
                break;
            case Key.J: StartCoroutine(ContinueProtection()); break;
            case Key.K: AfterStoreCutscene(); break;
            case Key.M: StartCoroutine(SwarmFresh()); break;
            case Key.N: SwarmWestDoor(); break;
            case Key.O: SwarmEscapeZoneNow(); break;
            case Key.P: GrabNow(-1); break;
            case Key.U: GrabNow(20); break;
            case Key.L:
                Player.instance.health.Invincible = false;
                Player.instance.health.Damage(100000, true);
                Note("L: Dead. Press Continue on the Game Over screen.", 6f);
                break;
        }
    }

    IEnumerator SwarmFresh()
    {
        StoryFlags.ResetSession();      // the session copy of "lane open" from a last try would keep the wall down
        Step4DebugJumps.FreshWardrobeRun(false);
        yield return null;
        while (Step4DebugJumps.Busy) yield return null;
        var w = WardrobeCloth();
        if (w != null && w.CanUseLighter()) w.TryBurn();
        yield return new WaitForSeconds(0.5f);
        Note("M: Pick up the tape in the wardrobe (E) and close it. The cutscene: Khalid walks back towards the camera - the glass breaks, he flinches and the camera zooms onto his face - the horde in the storm, mouths lighting up - over his shoulder, the lights ahead: \"I have to get outta here!\" and he backs away. ` 0 = invincible.");
    }

    void GrabNow(int stability)
    {
        var p = Player.instance;
        if (stability >= 0 && p.playerStability != null) p.playerStability.stability = stability;
        QuestionerEnemy best = null;
        float bestD = float.MaxValue;
        foreach (var q in QuestionerEnemy.All)
        {
            if (q == null || !q.isActiveAndEnabled || q.health == null || q.health.IsDead) continue;
            float d = Vector3.Distance(q.transform.position, p.transform.position);
            if (d < bestD) { bestD = d; best = q; }
        }
        if (best == null || bestD > 40f) { Note("No Questioner near (the swarm: ` M first)"); return; }
        // 2.8 m from Khalid with a clear line to him (in front if there's room, else behind / to a side), facing him
        Vector3 chest = p.transform.position + Vector3.up * 1.2f;
        Vector3 at = p.transform.position + p.transform.forward * 2.8f;
        foreach (var dirn in new[] { p.transform.forward, -p.transform.forward, p.transform.right, -p.transform.right })
        {
            Vector3 want = p.transform.position + dirn * 2.8f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(want, out var h, 0.6f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if (Physics.Linecast(chest, h.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            at = h.position;
            break;
        }
        if (best.agent != null && best.agent.isOnNavMesh) best.agent.Warp(at); else best.transform.position = at;
        Vector3 face = p.transform.position - at; face.y = 0f;
        if (face.sqrMagnitude > 0.01f) best.transform.rotation = Quaternion.LookRotation(face);
        if (best.stateMachine.CurrentState != best.combatState) best.stateMachine.ChangeState(best.combatState);
        best.combatState.DebugGrabNow();
        int st = p.playerStability != null ? p.playerStability.stability : -1;
        Note((stability >= 0 ? "U" : "P") + ": He grabs you (stability " + st + "). Two sliders: press E while the marker is in the orange zone, twice. Both = he's shoved off, no bite; a miss = the bite.");
    }

    static Transform SwarmPart(string name)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.parent != null && t.parent.name == "Action Sequence") return t;
        return null;
    }

    void SwarmWestDoor()
    {
        var p = Player.instance;
        if (p.controller != null) p.controller.enabled = false;
        p.transform.SetPositionAndRotation(new Vector3(-74f, 1.0f, 51f), Quaternion.Euler(0f, 270f, 0f));
        if (p.controller != null) p.controller.enabled = true;
        p.OnPlayerTelported?.Invoke();
        Note("N: Walk west to the door: crossing x -80.5 locks it - a loud lock and \"It's locked... I'm trapped.\" (needs the tape: ` M first). Then stay in the west end 15 s: the escape zone goes off even with no Questioners.");
    }

    void SwarmEscapeZoneNow()
    {
        var zone = SwarmPart("WeakpointHitArea (1)");
        if (zone == null) { Note("The escape zone is gone (it already went off this run): ` M for a fresh run"); return; }
        var p = Player.instance;
        if (p.controller != null) p.controller.enabled = false;
        p.transform.SetPositionAndRotation(new Vector3(-55f, 1.0f, 51f), Quaternion.Euler(0f, 90f, 0f));   // in the zone, as in play
        if (p.controller != null) p.controller.enabled = true;
        p.OnPlayerTelported?.Invoke();
        zone.gameObject.SetActive(true);
        zone.SendMessage("TriggerWeakpointHit");
        Note("O: The escape zone went off: your blocks swap (BlocksToDestroy off, your rubble on), \"I can't go there\" at the alley is gone, the camera glances at the alley entrance, Adel calls \"Khalid! Over here!\" from the exit and keeps calling until you get there.");
    }

    IEnumerator ContinueProtection()
    {
        var p = Player.instance;
        var cm = CheckpointManager.instance;
        if (cm == null) { Note("No checkpoint manager in this scene"); yield break; }
        p.health.Invincible = false;
        p.health.SetHealthSilently(5);
        cm.TriggerCheckpoint("debug: 5 HP here");
        yield return null; yield return null;   // written at the end of the frame
        p.health.Damage(100000, true);
        Note("J: Saved here with 5 HP, then died. Press Continue: he's back here with 50 HP (half), and for 3 s after the loading screen nothing can hurt or grab him.");
    }

    void AfterStoreCutscene()
    {
        foreach (var c in FindObjectsByType<CutsceneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.name != "Cutscene_AfterStore") continue;
            c.gameObject.SetActive(true);
            c.PlayCutscene();
            Note("K: The after-store cutscene (hold E skips). When it ends the checkpoint \"Back from the store\" is saved - then ` L to die and Continue: it doesn't play again.");
            return;
        }
        Note("Cutscene_AfterStore not found");
    }

    void OnGUI()
    {
        if (box == null)
        {
            box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, wordWrap = true, richText = true, padding = new RectOffset(10, 10, 8, 8) };
            box.normal.textColor = Color.white;
            hint = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        }
        if (open)
        {
            string text = "<b>Jump to (press the number, ` or Esc closes)</b>";
            foreach (var e in List) text += "\n<b>" + e.key.ToString().Replace("Digit", "") + "</b>   " + e.label;
            GUI.Box(new Rect(12, 12, 600, 30 + 21 * List.Length), text, box);
        }
        else if (Time.unscaledTime < noteUntil) GUI.Box(new Rect(12, 12, 600, 76), note, box);
        else GUI.Label(new Rect(14, 8, 200, 22), "` jumps", hint);
        // the swarm's attack meter (QuestionerHordeDirector), while a horde is about
        if (QuestionerHordeDirector.Pressure > 0f || QuestionerHordeDirector.Attacker != null)
        {
            var a = QuestionerHordeDirector.Attacker;
            GUI.Label(new Rect(Screen.width - 260, 8, 250, 22),
                "swarm pressure " + Mathf.RoundToInt(QuestionerHordeDirector.Pressure * 100f) + "%" + (a != null ? "  - ATTACKING" : ""), hint);
        }
    }

    void Note(string text, float seconds = 12f) { note = text; noteUntil = Time.unscaledTime + seconds; }

    // ---------------- helpers ----------------

    static IEnumerator Place(Vector3 pos, Vector3 facing)
    {
        while (Player.instance == null) yield return null;
        var p = Player.instance;
        // a known height: look for the ground just above it (from high up a roof or sign can be hit first)
        Vector3 from = pos.y > 0.01f ? pos + Vector3.up * 2.5f : pos + Vector3.up * 20f;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit ground, 40f, ~0, QueryTriggerInteraction.Ignore)) pos.y = ground.point.y + 0.1f;
        facing.y = 0f;
        if (p.controller != null) p.controller.enabled = false;
        p.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing : Vector3.forward));
        if (p.controller != null) p.controller.enabled = true;
        p.OnPlayerTelported?.Invoke();
        yield return null;
    }

    static BurnableObstacle WardrobeCloth()
    {
        foreach (var o in FindObjectsByType<BurnableObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (var t = o.transform; t != null; t = t.parent)
                if (t.name.StartsWith("Wardrobe Cloth Seal")) return o;
        return null;
    }

    static Enemy AdelRunner(AdelPoleSequence seq) =>
        seq == null ? null : new UnityEditor.SerializedObject(seq).FindProperty("runner").objectReferenceValue as Enemy;

    // ---------------- the changes ----------------

    IEnumerator Wardrobe(bool cut)
    {
        var w = WardrobeCloth();
        if (!cut || (w != null && !w.CanUseLighter()))
        {
            // fresh run (checkpoint snapshot + reload, as F12): an intact cloth again
            Step4DebugJumps.FreshWardrobeRun(false);
            yield return null;
            while (Step4DebugJumps.Busy) yield return null;
            w = WardrobeCloth();
        }
        if (!cut) { Note("1: The wardrobe cloth: thick canvas, rope ties, a bloody handprint. Cut it with the knife (you have it): a hole opens with the duct tape inside."); yield break; }
        if (w == null) { Note("Wardrobe cloth not found"); yield break; }
        var inv = Player.instance.inventory;
        foreach (var it in Resources.LoadAll<Item>("Items"))
            if (it.name == "Knife" && !inv.HasItem(it)) inv.PickUp(it);
        w.TryBurn();
        Note("2: Cut. The duct tape sits in the hole: walk up and pick it up.");
    }

    /// <summary>The encounter as if it never happened (the save forgets "Adel done" and the dead Runner, the bridge chase counts
    /// as done since this comes after it), Khalid stood at a way in, a checkpoint saved there and loaded back - so it is
    /// exactly what a real retry gives (and dying in the fight comes back here).</summary>
    IEnumerator FreshAdel(bool fromWest)
    {
        var seq = FindFirstObjectByType<AdelPoleSequence>(FindObjectsInactive.Include);
        var sm = SaveManager.instance;
        var cm = CheckpointManager.instance;
        if (seq == null || sm == null || cm == null) { Note("Adel pole sequence / save system not found"); yield break; }
        var runner = AdelRunner(seq);
        var uid = runner != null ? runner.GetComponent<UniqueID>() : null;

        var used = new List<string>(sm.GetUsedInteractables());
        used.Remove("flag:" + AdelPoleSequence.DoneFlag);
        var dead = new List<string>(sm.GetDeadEnemies());
        if (uid != null) dead.Remove(uid.ID);
        sm.RestoreFromCheckpoint(new List<string>(sm.GetPickedUpItems()), dead, new List<string>(sm.GetOpenedContainers()),
            new List<string>(sm.GetActivatedSwitches()), new List<string>(sm.GetStoppedSpawners()), new List<string>(sm.GetTriggeredZones()),
            used, sm.GetCurrentMusicTrack());
        StoryFlags.ResetSession();
        StoryFlags.Set(BridgeRunnerSequence.DoneFlag);

        if (fromWest) yield return Place(new Vector3(-62f, 0f, -10f), Vector3.right);
        else yield return Place(new Vector3(-10f, 0f, -9.5f), Vector3.left);
        cm.TriggerCheckpoint("debug: before the Adel street");
        yield return null; yield return null;

        var old = Player.instance;
        cm.ContinueFromCheckpoint();
        float give = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < give && (Player.instance == null || Player.instance == old)) yield return null;
        float settled = 0f;
        while (Time.realtimeSinceStartup < give && settled < 1.0f)
        {
            settled = CheckpointManager.IsLoadingCheckpoint || GameManager.IsPaused ? 0f : settled + Time.unscaledDeltaTime;
            yield return null;
        }
        Note(fromWest
            ? "6: Fresh (checkpoint saved here). Walk east along the street: it starts when you get near Adel, the Runner comes from the crossroads side."
            : "5: Fresh (checkpoint saved here). Walk west onto the road through the crossroads: Adel tied to the lamp by the red strip, the Runner barges in, then forced aim until you press aim.");
    }

    /// <summary>The coward meeting as every player gets it now: no gun (the hammer version), no duct tape (it sets off the
    /// Block 2 swarm), stood in Block 1 just out of the rope gate first so everything round him is there, then played.</summary>
    IEnumerator CowardMeeting()
    {
        var cs = FindFirstObjectByType<CowardMeetingCutscene>(FindObjectsInactive.Include);
        if (cs == null) { Note("Coward meeting not found"); yield break; }
        var inv = Player.instance.inventory;
        foreach (var it in Resources.LoadAll<Item>("Items"))
        {
            if (it.itemType == ItemType.DuctTape || it.name == "Pistol" || it.name == "Shotgun") while (inv.HasItem(it)) inv.RemoveItem(it);
            if (it.name == "Hammer" && !inv.HasItem(it)) inv.PickUp(it);
        }
        yield return Place(cs.transform.position + new Vector3(-1.5f, 0f, 0f), Vector3.right);
        yield return new WaitForSeconds(0.6f);
        cs.DebugPlayNow();
        Note("8: The coward meeting, new script (hammer version). Lines without a recording yet play silent with their subtitle. Hold E to skip.", 8f);
    }

    IEnumerator AbuKhalidDoor()
    {
        // the locked iron door with the lantern and the doorbell (Environmental Storytelling/Door Locked), street side = +x
        yield return Place(new Vector3(-52.6f, 0.9f, -90.1f), Vector3.left);
        Note("3: Abu Khalid's door (417 on the plate beside it). Press E on it: the locked knock, then \"Not yet...\". The other houses' plates follow the map's numbers.", 10f);
    }

    /// <summary>The knockdown as if it never happened (the save forgets it, its Runner and enemies live again, the wrapper is
    /// unseen), Khalid stood just west of its trigger, a checkpoint saved there and loaded back.</summary>
    IEnumerator FreshKnockdown()
    {
        var kd = FindFirstObjectByType<CinematicKnockdownSequence>(FindObjectsInactive.Include);
        var sm = SaveManager.instance;
        var cm = CheckpointManager.instance;
        if (kd == null || sm == null || cm == null) { Note("Knockdown / save system not found"); yield break; }
        var kso = new UnityEditor.SerializedObject(kd);
        var enemies = new List<Enemy>();
        if (kso.FindProperty("runnerEnemy").objectReferenceValue is Enemy runner) enemies.Add(runner);
        var list = kso.FindProperty("enemiesToKill");
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue is Enemy e) enemies.Add(e);
        // a checkpoint reload doesn't bring back enemies killed in this Play session
        if (enemies.Count == 0 || enemies.Exists(e => e == null || (e.health != null && e.health.IsDead)))
        { Note("4: The knockdown's Runner / enemies are already dead in this Play session - stop and restart Play, then press 4 again.", 8f); yield break; }

        var used = new List<string>(sm.GetUsedInteractables());
        used.RemoveAll(u => u.StartsWith("knockdown-done:") || u == "flag:" + AdelWrapperDrop.SeenFlag);
        var dead = new List<string>(sm.GetDeadEnemies());
        foreach (var e in enemies) { var uid = e.GetComponent<UniqueID>(); if (uid != null) dead.Remove(uid.ID); }
        sm.RestoreFromCheckpoint(new List<string>(sm.GetPickedUpItems()), dead, new List<string>(sm.GetOpenedContainers()),
            new List<string>(sm.GetActivatedSwitches()), new List<string>(sm.GetStoppedSpawners()), new List<string>(sm.GetTriggeredZones()),
            used, sm.GetCurrentMusicTrack());
        StoryFlags.ResetSession();

        yield return Place(new Vector3(-21f, 0f, -73.2f), Vector3.right);
        cm.TriggerCheckpoint("debug: before the table knockdown");
        yield return null; yield return null;
        var old = Player.instance;
        cm.ContinueFromCheckpoint();
        float give = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < give && (Player.instance == null || Player.instance == old)) yield return null;
        float settled = 0f;
        while (Time.realtimeSinceStartup < give && settled < 1.0f)
        {
            settled = CheckpointManager.IsLoadingCheckpoint || GameManager.IsPaused ? 0f : settled + Time.unscaledDeltaTime;
            yield return null;
        }
        // where the whispering door puts him (its ExitPoint, inside the pre-cutscene's trigger), as in the real run
        yield return Place(new Vector3(-15.39f, 0.92f, -74.41f), Vector3.right);
        Note("4: Through the whispering door (checkpoint saved just before it). Adel runs off and drops the candy wrapper, the Runner pushes you onto the table. After the fight walk on to it - \"Emerald Saloon?\" (0 = invincible).", 12f);
    }

    void AdelDoor()
    {
        var seq = FindFirstObjectByType<AdelDoorSequence>(FindObjectsInactive.Include);
        if (seq == null) { Note("Adel door sequence not found"); return; }
        seq.DebugArmAndJump();
        Note("9: Walk east over the line: the enemies freeze, Adel runs through the half-open gate. Walk up to the gate - it slams shut. Then press E on it.", 10f);
    }

    void KillRunner()
    {
        var seq = FindFirstObjectByType<AdelPoleSequence>(FindObjectsInactive.Include);
        var r = AdelRunner(seq);
        var h = r != null ? r.GetComponent<Health>() : null;
        if (h == null || h.IsDead) { Note("The Runner is already dead (restart Play for it again).", 5f); return; }
        h.Invincible = false;
        h.Damage(99999, true);
        Note("7: Runner killed - the blade pickup, freeing Adel, the shove and the crawl under the gate follow.", 6f);
    }
}
#endif
