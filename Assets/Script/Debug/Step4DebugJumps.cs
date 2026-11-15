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
