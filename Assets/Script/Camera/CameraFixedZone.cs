using UnityEngine;

/// <summary>
/// Drop this on a trigger volume placed in a room/corridor to give Tank mode a classic
/// RE/Silent Hill style fixed camera angle while the player is inside it. Requires a
/// Collider with "Is Trigger" enabled and the Player tagged "Player". Set cameraAnchor to
/// an empty Transform positioned/rotated wherever you want the camera to sit in this room.
/// Only affects Tank mode - Modern mode keeps its regular free-follow camera.
/// </summary>
public class CameraFixedZone : MonoBehaviour
{
    [Tooltip("The camera will match this transform's position and rotation while the player is in this zone.")]
    public Transform cameraAnchor;

    [Tooltip("Cut instantly to this angle when entering (recommended when facing changes drastically, e.g. a new room behind the player). Leave off to smoothly blend in instead.")]
    public bool cutOnEnter = false;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (CameraSystem.Instance != null)
            CameraSystem.Instance.EnterFixedZone(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (CameraSystem.Instance != null)
            CameraSystem.Instance.ExitFixedZone(this);
    }

    void OnDrawGizmos()
    {
        if (cameraAnchor == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(cameraAnchor.position, cameraAnchor.position + cameraAnchor.forward * 1.5f);
        Gizmos.DrawWireSphere(cameraAnchor.position, 0.2f);
    }
}
