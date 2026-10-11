using UnityEngine;
using UnityEngine.Events;

// A self-contained interior that sits away from the world (e.g. the park toilet room under the map, entered through a DoorInteractable).
// Checks the player's position every frame instead of using trigger callbacks, so it still works after loading a save/checkpoint
// that puts the player straight inside. While the player is inside, Enviro's fog post effect is switched off (it would turn a 4 m room
// into a sand cloud); on leaving, the fog goes back to whatever it was. Optionally locks the camera to a fixed room angle
// (a CameraFixedZone used as a plain anchor, no trigger needed) while inside, and cuts back behind the player on leaving.
public class InteriorZone : MonoBehaviour
{
    [Tooltip("Zone box in this object's local space.")]
    [SerializeField] Vector3 center = new Vector3(0f, 1.5f, 0f);
    [SerializeField] Vector3 size = new Vector3(4.4f, 3.4f, 5.8f);
    [SerializeField] bool disableFogInside = true;
    [Tooltip("Fixed room camera while inside (leave empty for the normal follow camera).")]
    [SerializeField] CameraFixedZone fixedCamera;

    public UnityEvent OnPlayerEntered;
    public UnityEvent OnPlayerExited;

    bool inside;
    bool fogWasOn;

    public bool PlayerInside => inside;

    void LateUpdate()
    {
        Player player = Player.instance;
        if (player == null) return;

        Vector3 local = transform.InverseTransformPoint(player.transform.position) - center;
        Vector3 half = size * 0.5f;
        bool now = Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;

        if (now && !inside)
        {
            inside = true;
            if (EnviroSkyLite.instance != null) fogWasOn = EnviroSkyLite.instance.usePostEffectFog;
            if (fixedCamera != null && CameraSystem.Instance != null) CameraSystem.Instance.EnterFixedZone(fixedCamera);
            OnPlayerEntered?.Invoke();
        }
        else if (!now && inside)
        {
            inside = false;
            if (disableFogInside && EnviroSkyLite.instance != null) EnviroSkyLite.instance.usePostEffectFog = fogWasOn;
            if (fixedCamera != null && CameraSystem.Instance != null) CameraSystem.Instance.ExitFixedZone(fixedCamera, true);
            OnPlayerExited?.Invoke();
        }

        // Enviro re-applies its own setting every frame, so keep ours on top while inside
        if (inside && disableFogInside && EnviroSkyLite.instance != null)
            EnviroSkyLite.instance.usePostEffectFog = false;
    }

    void OnDisable()
    {
        if (inside && disableFogInside && EnviroSkyLite.instance != null)
            EnviroSkyLite.instance.usePostEffectFog = fogWasOn;
        if (inside && fixedCamera != null && CameraSystem.Instance != null)
            CameraSystem.Instance.ExitFixedZone(fixedCamera, true);
        inside = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(center, size);
    }
}
