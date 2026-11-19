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
        (Key.Digit5, "Adel at the pole, fresh: east of the crossroads (walk west)"),
        (Key.Digit6, "Adel at the pole, fresh: west end of the street (walk east)"),
        (Key.Digit7, "Adel at the pole: kill the Runner (skip the fight)"),
        (Key.Digit8, "Coward meeting, new script (hammer version, from Block 1)"),
        (Key.Digit9, "Adel at the black gate in Block 3 (walk east over the line)"),
        (Key.Digit0, "Khalid invincible on / off"),
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
        }
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
