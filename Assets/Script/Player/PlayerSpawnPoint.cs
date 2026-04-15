using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private bool repositionPlayer = true;
    [SerializeField] private bool resetPlayerState = false;

    private void Start()
    {
        // Find the Player and reposition
        if (Player.instance != null && repositionPlayer)
        {
            // Disable CharacterController before moving (required for teleportation)
            if (Player.instance.controller != null)
            {
                Player.instance.controller.enabled = false;
            }

            // Move player to spawn point
            Player.instance.transform.position = transform.position;
            Player.instance.transform.rotation = transform.rotation;

            // Re-enable CharacterController
            if (Player.instance.controller != null)
            {
                Player.instance.controller.enabled = true;
            }

            // Optionally reset player state
            if (resetPlayerState)
            {
                Player.instance.ResetPlayer();
            }
        }
    }
}
