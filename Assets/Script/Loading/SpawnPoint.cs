using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    public string spawnPointID;

    private void Start()
    {
        // Check if this is the target spawn point
        string targetSpawn = PlayerPrefs.GetString("TargetSpawnPoint", "");

        if (!string.IsNullOrEmpty(targetSpawn) && targetSpawn == spawnPointID)
        {
            if (Player.instance != null)
            {
                // Disable CharacterController to allow position change
                CharacterController cc = Player.instance.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                Player.instance.transform.position = transform.position;
                Player.instance.transform.rotation = transform.rotation;

                if (cc != null) cc.enabled = true;
            }

            // Clear after use
            PlayerPrefs.DeleteKey("TargetSpawnPoint");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}
