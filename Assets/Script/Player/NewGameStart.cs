using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this on the block the player stands on when a new game begins (the Cube under the car crash trigger in Game).
/// A new game runs Menu > OpeningScene > Game. Arriving in Game from OpeningScene, by the cutscene ending or by skipping it, always
/// starts from here with a reset player and an empty inventory, whatever the Player was left with in the editor for testing.
/// Pressing Play directly in Game doesn't go through OpeningScene, so test setups still work. It runs when the scene has loaded,
/// after every Awake and before every Start, so nothing else sees the old position or items.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NewGameStart : MonoBehaviour
{
    const string OpeningSceneName = "OpeningScene";
    const string GameSceneName = "Game";

    [Tooltip("Which way the player faces when a new game begins (degrees around Y).")]
    [SerializeField] float playerFacingY = 90f;

    static string lastScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        lastScene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        bool newGameBegins = lastScene == OpeningSceneName && scene.name == GameSceneName;
        lastScene = scene.name;
        if (!newGameBegins) return;

        Player player = Player.instance;
        if (player == null)
        {
            Debug.LogError("[NewGameStart] No Player in " + scene.name);
            return;
        }

        CheckpointManager.pendingRestoreData = null;
        // leftovers from an earlier run (or an older build) that would put the new game somewhere else: the Store exit's spawn
        // point (SpawnPoint.Start reads it after this) and the maps
        PlayerPrefs.DeleteKey("TargetSpawnPoint");
        PlayerPrefs.DeleteKey("HasMap1");
        PlayerPrefs.DeleteKey("HasMap2");
        PlayerPrefs.SetInt("HasMap", 0);
        PlayerPrefs.Save();
        if (MapHandler.instance != null) MapHandler.instance.SetMaps(false, false);
        player.ResetForNewGame();

        NewGameStart start = FindFirstObjectByType<NewGameStart>(FindObjectsInactive.Include);
        if (start != null) start.PutPlayerOnTop(player);
        else Debug.LogError("[NewGameStart] Nothing marks the start in " + scene.name + ": the Player stays where the scene has him");
    }

    void PutPlayerOnTop(Player player)
    {
        Bounds block = GetComponent<Collider>().bounds;
        Vector3 position = block.center + Vector3.up * block.extents.y;

        // the controller's capsule doesn't have to end at the Player's origin
        CharacterController controller = player.controller;
        if (controller != null)
        {
            position.y -= (controller.center.y - controller.height * 0.5f - controller.skinWidth) * player.transform.lossyScale.y;
            controller.enabled = false;
        }
        player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, playerFacingY, 0f));
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
        player.OnPlayerTelported?.Invoke();
    }
}
