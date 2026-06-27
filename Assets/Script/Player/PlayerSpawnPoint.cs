using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Player targetPlayer; // Manually assign player, or leave null to use Player.instance
    
    [Header("Settings")]
    [SerializeField] private bool repositionPlayer = true;
    [SerializeField] private bool resetPlayerState = false;

    private void Start()
    {
        // Use manually assigned player if available, otherwise fall back to singleton
        Player player = targetPlayer != null ? targetPlayer : Player.instance;
        
        // Find the Player and reposition
        if (player != null && repositionPlayer)
        {
            // Disable CharacterController before moving (required for teleportation)
            if (player.controller != null)
            {
                player.controller.enabled = false;
            }

            // Move player to spawn point
            player.transform.position = transform.position;
            player.transform.rotation = transform.rotation;

            // Re-enable CharacterController
            if (player.controller != null)
            {
                player.controller.enabled = true;
            }

            // Optionally reset player state
            if (resetPlayerState)
            {
                player.ResetPlayer();
            }
        }
    }
}
