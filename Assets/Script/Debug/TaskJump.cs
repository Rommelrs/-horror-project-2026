#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only playtest jumps (the whole file is #if UNITY_EDITOR: nothing of it is in a scene or a build).
/// In Play, press ` (the key under Esc), TYPE THE TASK NUMBER and press Enter. Numbers = the user's list of 2026-11-25. Each entry
/// goes when its change is approved, and this file is deleted when the list is empty.
/// </summary>
public class TaskJump : MonoBehaviour
{
    bool open;
    string typed = "";
    Vector2 scroll;
    GUIStyle box, hint;
    string note = "";
    float noteUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<TaskJump>() != null) return;
        var go = new GameObject("TaskJump (editor only)");      // (no DontSave: those survived every Play and piled up)
        DontDestroyOnLoad(go);
        go.AddComponent<TaskJump>();
    }

    static readonly (int no, string label)[] List =
    {
        (1, "Park chase WITHOUT the hammer: walk down the lane - nothing may start"),
        (4, "Store scene: register Runner (open the Store scene and leave the register at any moment)"),
        (5, "Combination lock: 52 arrows, 53 camera to the drawing, 5 hint button after 45 s"),
        (6, "First Questioner: the grab dash lesson (world stops until you strike)"),
        (7, "Stability tutorial pop-up"),
        (8, "Alley 2 start: no loose bags flying"),
        (9, "Map with EVERY marker on (9, 44, 46)"),
        (11, "Runner up close: raw knees (15: their sound once you pick one)"),
        (12, "Free camera on: look sideways, then aim"),
        (13, "Runner encounter at the pole: no aiming in the cutscene (16, 17 in the fight)"),
        (18, "Second hole: planks at the bottom"),
        (19, "Whispering hole: wires to the door"),
        (20, "Swarm: at the locked west door - they can't be pushed"),
        (21, "Coward meeting: try 1 / 2 / wheel during it"),
        (22, "After-swarm cutscene, version A (122 = version B)"),
        (23, "Gates: one-way / sealed (230 = needs a key)"),
        (24, "Whispering door / table knockdown, fresh: spam E at the door"),
        (27, "Questioner grabs you now (fail = same speed, escape = faster)"),
        (28, "Circuit box key on the car hood: dust"),
        (29, "\"I have to find the kid first\": goes by itself after 2 s"),
        (33, "Delivery map in the inventory: straight, new text"),
        (30, "Between the park tanks"),
        (31, "Park ladder"),
        (34, "First Questioner grab: the slider waits, E flashes"),
        (35, "Death help: die once here, then Continue"),
        (36, "A door with enemies about: they freeze while it fades"),
        (37, "Knife handle: no bags thrown after it"),
        (39, "Park chase now: M must not open the map"),
        (40, "Park main entrance with the hammer: chained shut"),
        (41, "Electricity alley: bag bearer reloads first, 45 bigger weakpoint, 56 no walking through backwards"),
        (42, "Alley 2 entrance gate from the inside"),
        (43, "Gate key in the inventory: new description"),
        (48, "Swarm: escape zone now - Adel's line goes when you get near"),
        (49, "Adel drops the wrapper at the gate, then you walk to it"),
        (50, "Shows the save time, live"),
        (54, "\"Press E to interact\" at the first pickup"),
        (55, "Pistol pickup: no switching, no walking off before the first shot"),
        (57, "Alley 1 Runner gone quicker (157 = Alley 2's)"),
        (58, "Electricity alley: the cartons burst (59 = OFF board)"),
        (60, "Store door WITH the hammer: no prompt, E does nothing"),
        (0, "Khalid invincible on / off"),
    };

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.backquoteKey.wasPressedThisFrame) { open = !open; typed = ""; }
        if (!open) return;
        if (kb.escapeKey.wasPressedThisFrame) { open = false; return; }
        for (int d = 0; d <= 9; d++)
            if (kb[(Key)((int)Key.Digit1 + (d + 9) % 10)].wasPressedThisFrame || kb[(Key)((int)Key.Numpad0 + d)].wasPressedThisFrame) typed += d;
        if (kb.backspaceKey.wasPressedThisFrame && typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            open = false;
            if (int.TryParse(typed, out int no)) Jump(no);
        }
        if (kb.downArrowKey.isPressed) scroll.y += 400f * Time.unscaledDeltaTime;
        if (kb.upArrowKey.isPressed) scroll.y -= 400f * Time.unscaledDeltaTime;
    }

    /// <summary>Also called by tests.</summary>
    public void Jump(int no)
    {
        StopAllCoroutines();
        if (SceneManager.GetActiveScene().name != "Game" || Player.instance == null)
        {
            Note(no == 4 ? "4: in the Store scene: use the register (fuse in), leave it at any moment - he shows for a blink." : "These are in the Game scene.");
            return;
        }
        switch (no)
        {
            case 0:
                var h = Player.instance.health;
                h.Invincible = !h.Invincible;
                Note("Khalid invincible: " + (h.Invincible ? "ON" : "off"), 3f);
                break;
            case 1: StartCoroutine(NoHammerLane()); break;
            case 4: Note("4: open the Store scene and press Play there."); break;
            case 5: case 52: case 53: StartCoroutine(AtLock()); break;
            case 6: StartCoroutine(FirstQuestioner(true)); break;
            case 11: case 15: StartCoroutine(RunnerClose()); break;
            case 18: StartCoroutine(At(new Vector3(86f, 1f, -72f), Vector3.left, "18: look down the hole: the same planks.")); break;
            case 19: StartCoroutine(At(new Vector3(-21f, 1f, -72.6f), new Vector3(1f, 0f, -0.9f), "19: wires from the hole to the door's box.")); break;
            case 23: StartCoroutine(At(new Vector3(-142.5f, 1.5f, -8.8f), Vector3.left, "23: one-way gate from the sealed side (other side: yellow bolt).")); break;
            case 230: StartCoroutine(At(new Vector3(-8.5f, 1.6f, 26f), Vector3.back, "23: the gate that needs the gate key: red lock plate + chain, gone once unlocked.")); break;
            case 30: StartCoroutine(At(new Vector3(-168f, 3f, 33f), Vector3.right, "30: walk between the tanks, hugging them.")); break;
            case 31: StartCoroutine(At(new Vector3(-193f, 3f, 78f), new Vector3(1f, 0f, -0.3f), "31: the yellow ladder.")); break;
            case 35: Give("Pistol"); CheckpointManager.instance.TriggerCheckpoint("debug: death help"); DeathHelp.DebugPrime(); Note("35: five deaths counted here. Die once more (0 = invincible off) and press Continue."); break;
            case 42: StartCoroutine(At(new Vector3(-82.5f, 1.1f, 50.9f), Vector3.left, "42: leave through the gate (shoot the bag off first): you come out facing away from it.")); break;
            case 49: { var s = FindFirstObjectByType<AdelDoorSequence>(); if (s != null) { StoryFlags.ResetSession(); Forget(AdelDoorSequence.SeenFlag); Forget(AdelWrapperDrop.SeenFlag); s.DebugArmAndJump(); } Note("49: walk east: Adel drops it at the gate; go near, the gate shuts, you walk to it by yourself."); break; }
            case 58: case 59: Give("Hammer"); StartCoroutine(At(new Vector3(-48.3f, 1f, 59.5f), Vector3.forward, "58 / 59: walk north; the cartons burst. The OFF board is at the far end, right wall.")); break;
            case 34: StartCoroutine(FirstQuestioner(false)); break;
            case 7: Forget(StabilityTutorial.Flag); StabilityTutorial.ShowOnce(0.5f); Note("7: also in Options > Stability Tutorial."); break;
            case 12: if (CameraSystem.Instance != null) CameraSystem.Instance.SetFreeCamera(true); Give("Pistol"); Note("12: turn the camera sideways with the mouse, then aim (gun out: key 2)."); break;
            case 40: Give("Hammer"); StartCoroutine(At(new Vector3(-147f, 3f, 69f), Vector3.right, "40: the entrance is chained shut (outside it too).")); break;
            case 8: StartCoroutine(At(new Vector3(-64f, 0.9f, 49.5f), Vector3.right, "8: walk into Alley 2: no bags from nowhere.")); break;
            case 9: case 44: case 46: StartCoroutine(AllMarkers()); break;
            case 13: case 14: case 16: case 17: StartCoroutine(FreshAdel(true)); break;
            case 20: StartCoroutine(SwarmAtDoor()); break;
            case 48: SwarmEscapeZoneNow(); break;
            case 21: StartCoroutine(CowardMeeting()); break;
            case 22: Escape("ActionSequenceCutscene"); break;
            case 122: Escape("ActionSequenceCutscene B"); break;
            case 24: case 26: case 51: StartCoroutine(FreshKnockdown()); break;
            case 27: GrabNow(-1); break;
            case 28: StartCoroutine(AtObject("CircuitBoxKeyPickup", 2.2f, "28: the circuit box key's dust on the hood.")); break;
            case 29: StartCoroutine(AtObject("Cube (5)", 4f, "29: walk into the boundary: the line goes by itself after 2 s.", "Boundaries")); break;
            case 33: Give("Delivery Map"); Note("33: open the inventory, inspect the delivery map: straight, and the new text."); break;
            case 36: StartCoroutine(At(new Vector3(-176.7f, 1.6f, 9.6f), Vector3.forward, "36: use the gate: anything chasing you stands still while the screen is black.")); break;
            case 37: StartCoroutine(AtObject("KnifeHandlePickup", 2.5f, "37: take the knife handle: no bags thrown.")); break;
            case 39: StartCoroutine(KidnapNow()); break;
            case 41: case 45: case 56:
                Give("Hammer");
                StartCoroutine(At(new Vector3(-48.4f, 1.1f, 57.5f), Vector3.forward, "41 / 45 / 56: walk north over the line; the bag bearer reloads first (bigger weakpoint). Then back into the sparks with your back to them."));
                break;
            case 43: Give("Gate Key"); Note("43: look at the Gate Key in the inventory."); break;
            case 50: StartCoroutine(LiveTime()); break;
            case 54: StartCoroutine(FirstPickup()); break;
            case 55: StartCoroutine(At(new Vector3(-83f, 1.1f, 50.8f), Vector3.right, "55: walk to the pistol: reload, aim and shoot - no switching, no walking off before the shot.")); break;
            case 57: StartCoroutine(At(new Vector3(-176.7f, 1.6f, 9.6f), Vector3.back, "57: the Alley 1 Runner, past the gate.")); break;
            case 157: StartCoroutine(At(new Vector3(-60f, 0.9f, 50f), Vector3.right, "57: the Alley 2 Runner.")); break;
            case 60: Give("Hammer"); StartCoroutine(At(new Vector3(-237.5f, 4.9f, 102.9f), Vector3.left, "60: with the hammer the store door is dead: no prompt, no E.")); break;
            default: Note("No jump " + no + " yet."); break;
        }
    }

    // ---------------- the jumps ----------------

    IEnumerator At(Vector3 pos, Vector3 facing, string text) { yield return Place(pos, facing); Note(text); }

    IEnumerator AtObject(string objectName, float back, string text, string parent = null)
    {
        Transform found = null;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == objectName && (parent == null || (t.parent != null && t.parent.name == parent))) { found = t; break; }
        if (found == null) { Note(objectName + " is gone (picked up this run?)"); yield break; }
        Vector3 at = found.position;
        foreach (var d in new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward })
        {
            if (!UnityEngine.AI.NavMesh.SamplePosition(found.position + d * back, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            at = hit.position;
            break;
        }
        yield return Place(at, found.position - at);
        Note(text);
    }

    static Item Find(string itemAsset)
    {
        foreach (var it in Resources.LoadAll<Item>("Items")) if (it.name == itemAsset) return it;
        return null;
    }

    static void Give(string itemAsset)
    {
        var it = Find(itemAsset);
        if (it != null && !Player.instance.inventory.HasItem(it)) Player.instance.inventory.PickUp(it);
    }

    static void Take(string itemAsset)
    {
        var it = Find(itemAsset);
        while (it != null && Player.instance.inventory.HasItem(it)) Player.instance.inventory.RemoveItem(it);
    }

    /// <summary>A story flag forgotten (session copy and save).</summary>
    static void Forget(string flag)
    {
        StoryFlags.ResetSession();
        var sm = SaveManager.instance;
        if (sm == null) return;
        var used = new List<string>(sm.GetUsedInteractables());
        if (!used.Remove("flag:" + flag)) return;
        sm.RestoreFromCheckpoint(new List<string>(sm.GetPickedUpItems()), new List<string>(sm.GetDeadEnemies()), new List<string>(sm.GetOpenedContainers()),
            new List<string>(sm.GetActivatedSwitches()), new List<string>(sm.GetStoppedSpawners()), new List<string>(sm.GetTriggeredZones()), used, sm.GetCurrentMusicTrack());
    }

    IEnumerator NoHammerLane()
    {
        Take("Hammer");
        Forget(KidnapSequence.DoneFlag);
        yield return Place(new Vector3(-229.5f, 2.9f, 84f), Vector3.back);
        Note("1: no hammer: walk down the lane - the park chase must not start. (The weapon is called the Hammer.)");
    }

    IEnumerator KidnapNow()
    {
        Give("Hammer");
        var k = FindFirstObjectByType<KidnapSequence>(FindObjectsInactive.Include);
        if (k == null) { Note("Kidnap sequence not found"); yield break; }
        yield return Place(new Vector3(-229.5f, 2.9f, 84f), Vector3.back);
        yield return null;
        k.DebugPlayNow();
        Note("39: the park chase: press M during the cutscene and the chase - no map.");
    }

    IEnumerator SwarmAtDoor()
    {
        if (WardrobeCloth() != null) yield return SwarmFresh();
        SwarmWestDoor();
        Note("20: get the swarm on you, walk west into the locked door - from the lock on you can't push through them there.");
    }

    IEnumerator AtLock()
    {
        Forget("LockDrawingShown");
        yield return Place(new Vector3(63.0f, 1.6f, -45.6f), Vector3.forward);
        Note("5 / 52 / 53: the lock: arrows over and under each dial. Turn one, leave with E: the camera shows the hopscotch. 45 s later the lock's screen has a Hint button (H).");
    }

    IEnumerator AllMarkers()
    {
        if (MapHandler.instance == null) yield break;
        MapHandler.instance.UnlockMap1();
        Give("Knife");
        foreach (var t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool under = false;
            for (var p = t.parent; p != null; p = p.parent) if (p.name == "MapMarkers") { under = true; break; }
            if (under) t.gameObject.SetActive(true);
        }
        yield return null;
        MapHandler.instance.EnableMapMenu(true);
        Note("9 / 44 / 46: every marker on (load a checkpoint afterwards to get your own map back).");
    }

    void Escape(string cutscene)
    {
        foreach (var d in FindObjectsByType<CutsceneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (d.transform.parent == null || d.transform.parent.name != cutscene) continue;
            d.transform.parent.gameObject.SetActive(true);
            d.gameObject.SetActive(true);
            d.PlayCutscene();
            Note("22: " + cutscene + ": at ~40 s \"Am I in a dream or something?\", at ~105 s \"No... I can't stop. Not right now.\"", 14f);
            return;
        }
        Note(cutscene + " not found");
    }

    IEnumerator LiveTime()
    {
        while (true)
        {
            var ts = System.TimeSpan.FromSeconds(SaveManager.instance != null ? SaveManager.instance.Playtime : 0f);
            Note("50: save time " + ts.ToString(@"hh\:mm\:ss") + " - open menus, read notes, watch a cutscene: it never stops.", 2f);
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    IEnumerator FirstPickup()
    {
        Forget("FirstPickupHintDone");
        InspectableItemPickup best = null;
        foreach (var p in FindObjectsByType<InspectableItemPickup>(FindObjectsSortMode.None))
            if (p.itemToPickup != null && p.itemToPickup.itemType == ItemType.Ammo) { best = p; break; }
        if (best == null) { Note("no ammo pickup left"); yield break; }
        Vector3 at = best.transform.position + Vector3.back * 2.5f;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
        yield return Place(at, best.transform.position - at);
        Note("54: walk up to it: \"Press E to interact\" - and never again once something is taken.");
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
            string text = "<b>Type the task number + Enter: " + typed + "_</b>   (` or Esc closes, arrows scroll)";
            foreach (var e in List) text += "\n<b>" + e.no + "</b>   " + e.label;
            float height = 46 + 21 * (List.Length + 2);
            float view = Mathf.Min(height, Screen.height - 24);
            scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, height - view));
            GUI.BeginGroup(new Rect(12, 12, 780, view));
            GUI.Box(new Rect(0, -scroll.y, 780, height), text, box);
            GUI.EndGroup();
        }
        else if (Time.unscaledTime < noteUntil) GUI.Box(new Rect(12, 12, 600, 76), note, box);
        else GUI.Label(new Rect(14, 8, 200, 22), "` jumps", hint);
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

    IEnumerator RunnerClose()
    {
        yield return Place(new Vector3(-176.7f, 1.6f, -2f), Vector3.back);
        Player.instance.health.Invincible = true;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/RunnerEnemy.prefab");
        UnityEngine.AI.NavMesh.SamplePosition(Player.instance.transform.position + Vector3.back * 5f, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas);
        Instantiate(prefab, h.position, Quaternion.LookRotation(Vector3.forward));
        Note("11: his knees (you are invincible, 0 = off).");
    }

    IEnumerator FirstQuestioner(bool lesson)
    {
        Give("Hammer");
        Take("Pistol");
        yield return Place(new Vector3(-176.7f, 1.6f, -2f), Vector3.back);
        Forget(GrabCounterTutorial.Flag); Forget(GrabEscapeQTE.LessonFlag);
        if (!lesson) StoryFlags.Set(GrabCounterTutorial.Flag);
        yield return new WaitForSeconds(0.5f);
        var p = Player.instance;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/QuestionerEnemy.prefab");
        UnityEngine.AI.NavMesh.SamplePosition(p.transform.position + Vector3.back * 6f, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas);
        var q = Instantiate(prefab, h.position, Quaternion.LookRotation(Vector3.forward)).GetComponent<QuestionerEnemy>();
        yield return new WaitForSeconds(0.5f);
        q.ChangeEnemyType(EnemyType.Aggressive);
        Note(lesson ? "6: keep the hammer out: he dashes, the world stops, raise it and strike." : "34: let him grab you (he should soon): the slider stops in the zone, E flashes.");
        if (lesson) yield break;
        q.stateMachine.ChangeState(q.combatState);
        q.combatState.DebugGrabNow();
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

    static Transform SwarmPart(string name)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.parent != null && t.parent.name == "Action Sequence") return t;
        return null;
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

    void Note(string text, float seconds = 12f) { note = text; noteUntil = Time.unscaledTime + seconds; }

    static Enemy AdelRunner(AdelPoleSequence seq) =>
        seq == null ? null : new UnityEditor.SerializedObject(seq).FindProperty("runner").objectReferenceValue as Enemy;
}
#endif
