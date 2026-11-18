#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// TEMPORARY test keys for store redesign step 4 (editor only, no scene objects; delete this file once approved):
///   F6 (Store scene)  - play the storage-room cutscene now, from the door.
///   F7 (Store scene)  - take the hammer and leave through the back door: the Game scene loads and the exit cutscene plays.
///   F7 (Game scene)   - play the exit cutscene now, from where the back door puts Khalid.
///   F8 (Game scene)   - the kidnap at the park toilet now (then the chase to Block 1).
///   F11 (Game scene)  - bridge Runner chase reset: plank back, Runner hidden again, Khalid just short of the area at the next
///                       way in (car lot / between the blocks / sand lot / far road). Walk in to play it; press again any time.
///   Alt+F11 (Game)    - Adel tied to the pole on the middle street: Khalid just south of it, walk in.
///   F10 (Game scene)  - just short of the pistol pickup (walk in: auto pickup + the "Pistol pickup" checkpoint).
///   F12 (Game scene)  - Block 2 wardrobe prototype, FRESH run (checkpoint reset to a clean snapshot + reload): at the
///                       wardrobe with knife + gate key (order A). Shift+F12: same at the Block 3 cloth (order B).
///                       Ctrl+F12: straight to the cinder-block escape door (no reset).
///                       Alt+F12: the swarm escape cutscene now (A if the Block 3 cloth is cut, B if not).
/// </summary>
public class Step4DebugJumps : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("[Step4DebugJumps]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<Step4DebugJumps>();
    }

    bool busy;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || busy || Player.instance == null) return;
        string scene = SceneManager.GetActiveScene().name;
        if (kb.f6Key.wasPressedThisFrame && scene == "Store")
        {
            var cs = FindFirstObjectByType<StorageMeetingCutscene>(FindObjectsInactive.Include);
            if (cs != null) cs.DebugPlayNow();
        }
        if (kb.f7Key.wasPressedThisFrame && (scene == "Store" || scene == "Game")) StartCoroutine(Co_ExitCutscene(scene));
        if (kb.f8Key.wasPressedThisFrame && scene == "Game")
        {
            var k = FindFirstObjectByType<KidnapSequence>(FindObjectsInactive.Include);
            if (k != null) k.DebugPlayNow();
        }
        if (kb.f11Key.wasPressedThisFrame && scene == "Game" && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed))
        {
            // Adel tied to the pole on the middle street: Khalid just south of the street, walk in
            var ap = FindFirstObjectByType<AdelPoleSequence>(FindObjectsInactive.Include);
            if (ap != null) ap.DebugArmAndJump();
        }
        else if (kb.f11Key.wasPressedThisFrame && scene == "Game")
        {
            // each press: everything put back, Khalid a few steps short of the area at the next way in - walk in yourself
            // (F9 is the save system's quick load)
            var b = FindFirstObjectByType<BridgeRunnerSequence>(FindObjectsInactive.Include);
            if (b != null) { b.DebugResetAndJump(bridgeSpawn); bridgeSpawn++; }
        }
        if (kb.f10Key.wasPressedThisFrame && scene == "Game") JumpToPistol(player: Player.instance);

        // Block 2 wardrobe prototype: F12 fresh run at the wardrobe (test order A), Shift+F12 fresh run at the Block 3 cloth
        // (order B), Ctrl+F12 straight to the cinder-block escape door (no reset)
        if (kb.f12Key.wasPressedThisFrame && scene == "Game")
        {
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            if (alt)
            {
                // the escape cutscene right now, exactly as the cinder-block door starts it: version A if the Block 3 cloth is
                // cut, version B (street walk, rests outside the cloth) if not
                var doorT = FindT("DoorInteractableBlocks", "Action Sequence");
                var door = doorT != null ? doorT.GetComponent<DoorInteractable>() : null;
                if (door != null) { Debug.Log("[F12] escape cutscene now (A or B by the Block 3 cloth)"); door.OnInteractedSuccess.Invoke(); }
            }
            else if (ctrl) JumpTo(FindT("DoorInteractableBlocks", "Action Sequence"), 0f, true);
            else StartCoroutine(Co_FreshWardrobeRun(shift));
        }
    }

    // ------------------------------------------------------------------ Block 2 wardrobe prototype

    static string SaveDir => System.IO.Path.Combine(Application.persistentDataPath, "Saves");
    static string Snapshot => System.IO.Path.Combine(SaveDir, "debug_block2_snapshot.json");

    /// <summary>Back to a clean state (the checkpoint as it was the first time this was pressed, so cut cloth / used swarm /
    /// picked tape from the last try are gone), reload it, then stand Khalid at the wardrobe (or the Block 3 cloth) with the
    /// knife and the gate key.</summary>
    IEnumerator Co_FreshWardrobeRun(bool atBlock3)
    {
        busy = true;
        string cp = System.IO.Path.Combine(SaveDir, "checkpoint.json");
        if (!System.IO.File.Exists(Snapshot) && System.IO.File.Exists(cp)) System.IO.File.Copy(cp, Snapshot);
        if (!System.IO.File.Exists(Snapshot) || CheckpointManager.instance == null) { Debug.LogWarning("[F12] no checkpoint to start from"); busy = false; yield break; }
        System.IO.File.Copy(Snapshot, cp, true);
        CutsceneTrigger.ResetPlayed();
        var oldPlayer = Player.instance;
        CheckpointManager.instance.ContinueFromCheckpoint();

        // wait for the NEW Khalid (the old one lives on behind the loading screen), then for the restore to be done for a while
        float give = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < give && (Player.instance == null || Player.instance == oldPlayer)) yield return null;
        float settled = 0f;
        while (Time.realtimeSinceStartup < give && settled < 1.5f)
        {
            settled = CheckpointManager.IsLoadingCheckpoint || GameManager.IsPaused ? 0f : settled + Time.unscaledDeltaTime;
            yield return null;
        }
        var player = Player.instance;
        if (player == null) { busy = false; yield break; }

        foreach (var name in new[] { "Knife", "Gate Key" })
            foreach (var it in Resources.LoadAll<Item>("Items"))
                if (it.name == name && !player.inventory.HasItem(it)) player.inventory.PickUp(it);

        Transform spot = atBlock3 ? FindT("CuttingPosition", "ClothBlockage") : FindT("CuttingPosition", "Wardrobe Cloth Seal (prototype)");
        JumpTo(spot, 0f, false);
        Debug.Log("[F12] fresh run " + (atBlock3 ? "at the Block 3 cloth (order B)" : "at the wardrobe (order A)") + " - knife + gate key given");
        busy = false;
    }

    static Transform FindT(string name, string parentName)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.parent != null && t.parent.name == parentName) return t;
        return null;
    }

    static void JumpTo(Transform spot, float back, bool useDoorEnter)
    {
        var player = Player.instance;
        if (player == null || spot == null) { Debug.LogWarning("[F12] spot not found"); return; }
        Vector3 pos = spot.position; Quaternion rot = spot.rotation;
        var door = useDoorEnter ? spot.GetComponent<DoorInteractable>() : null;
        if (door != null && door.enterPoint != null)
        {
            // a step back from the door on its approach side, facing it
            Vector3 away = door.enterPoint.position - door.transform.position; away.y = 0f;
            pos = door.enterPoint.position + away.normalized * 1.2f;
            Vector3 face = door.transform.position - pos; face.y = 0f;
            rot = Quaternion.LookRotation(face);
        }
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(pos + Vector3.up * 0.1f, rot);
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
    }

    int bridgeSpawn;

    // a few steps short of the pistol's walk-in trigger, facing it
    static void JumpToPistol(Player player)
    {
        if (player.controller != null) player.controller.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(-84f, 1.6f, 50.6f), Quaternion.Euler(0f, 90f, 0f));
        if (player.controller != null) player.controller.enabled = true;
        player.OnPlayerTelported?.Invoke();
    }

    static OnPlayerTrigger ExitTrigger()
    {
        foreach (var t in FindObjectsByType<OnPlayerTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Trigger" && t.transform.parent != null && t.transform.parent.name == "Cutscenes") return t;
        return null;
    }

    IEnumerator Co_ExitCutscene(string scene)
    {
        busy = true;
        var player = Player.instance;
        if (scene == "Store")
        {
            // the exit cutscene only plays for a Khalid who has the hammer
            Item hammer = null;
            foreach (var it in Resources.LoadAll<Item>("Items")) if (it.name == "Hammer") hammer = it;
            if (hammer != null && !player.inventory.HasItem(hammer)) player.inventory.PickUp(hammer);
            LoadButton door = null;
            foreach (var b in FindObjectsByType<LoadButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name == "DoorInteractable (BackMain)") door = b;
            if (door != null) door.LoadGame();
            // the trigger in the alley does the rest once the Game scene is up
        }
        else
        {
            var trig = ExitTrigger();
            if (trig != null)
            {
                var so = new UnityEditor.SerializedObject(trig);
                var hammer = so.FindProperty("requiredItem").objectReferenceValue as Item;
                if (hammer != null && !player.inventory.HasItem(hammer)) player.inventory.PickUp(hammer);
                PlayerPrefs.SetString("PreviousScene", "Store");
                if (player.controller != null) player.controller.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(-229.53f, 2.74f, 100.6f), Quaternion.Euler(0f, 181f, 0f));
                if (player.controller != null) player.controller.enabled = true;
                player.OnPlayerTelported?.Invoke();
                yield return null;
                trig.Trigger();
            }
        }
        yield return new WaitForSecondsRealtime(1f);
        busy = false;
    }
}
#endif
