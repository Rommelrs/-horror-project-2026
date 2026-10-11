using UnityEngine;

/// <summary>
/// Drop this on a trigger volume placed in a room/corridor to give Tank mode a classic
/// RE/Silent Hill style fixed camera angle while the player is inside it. Requires a
/// Collider with "Is Trigger" enabled and the Player tagged "Player" (or no collider, when something
/// else such as an InteriorZone enters/exits it). Set cameraAnchor to an empty Transform
/// positioned/rotated wherever you want the camera to sit in this room - the camera matches it exactly.
/// Tip: frame the shot in the Scene view, then press "Set Anchor From Scene View" in the inspector.
/// </summary>
public class CameraFixedZone : MonoBehaviour
{
    [Tooltip("The camera will match this transform's position and rotation while the player is in this zone.")]
    public Transform cameraAnchor;

    [Tooltip("Cut instantly to this angle when entering (recommended when facing changes drastically, e.g. a new room behind the player). Leave off to smoothly blend in instead.")]
    public bool cutOnEnter = false;

    [Tooltip("0 = camera is locked exactly on the anchor. Above 0 = seconds the camera takes to glide to the anchor when it moves (or when blending in).")]
    [Min(0f)] public float blendTime = 0f;

    [Tooltip("Ignore any roll on the anchor so the horizon always stays level.")]
    public bool keepLevel = true;

    [Header("Follow the player")]
    [Tooltip("The camera stays at the anchor but turns to keep the player in view.")]
    public bool trackPlayer = true;
    [Tooltip("1 = always aims straight at the player. Lower keeps more of the anchor's own framing and only leans toward the player.")]
    [Range(0f, 1f)] public float trackAmount = 0.9f;
    [Tooltip("Height above the player's feet the camera aims at.")]
    public float lookHeight = 1.2f;
    [Tooltip("Seconds the aim takes to catch up with the player (higher = lazier, smoother).")]
    [Min(0f)] public float trackSmoothTime = 0.35f;
    [Tooltip("Never tilt down more than this (degrees), so the shot doesn't turn into a top-down view of the player's head when they walk under the camera.")]
    [Range(10f, 89f)] public float maxLookDown = 50f;
    [Tooltip("How much the camera itself drifts along with the player's movement (0 = position never moves).")]
    [Range(0f, 1f)] public float followAmount = 0.25f;
    [Tooltip("Furthest the camera may drift from the anchor (metres).")]
    [Min(0f)] public float followRange = 1.0f;
    [Tooltip("The camera never leaves this box (local to this object), so drifting can't push it through a wall.")]
    public Vector3 boundsCenter = new Vector3(0f, 2.3f, 0f);
    public Vector3 boundsSize = new Vector3(3.2f, 1.0f, 4.8f);

    // Where the camera should be for this player position: the anchor, plus a little drift with the player, kept inside the bounds
    public Vector3 GetCameraPosition(Vector3 playerPos)
    {
        if (cameraAnchor == null) return transform.position;
        if (followAmount <= 0f || followRange <= 0f) return cameraAnchor.position;
        Vector3 d = playerPos - transform.position; d.y = 0f;
        Vector3 p = cameraAnchor.position + Vector3.ClampMagnitude(d * followAmount, followRange);
        Vector3 local = transform.InverseTransformPoint(p);
        Vector3 h = boundsSize * 0.5f;
        local = new Vector3(Mathf.Clamp(local.x, boundsCenter.x - h.x, boundsCenter.x + h.x),
                            Mathf.Clamp(local.y, boundsCenter.y - h.y, boundsCenter.y + h.y),
                            Mathf.Clamp(local.z, boundsCenter.z - h.z, boundsCenter.z + h.z));
        return transform.TransformPoint(local);
    }

    // Aim from camPos at the (smoothed) look point, blended with the anchor's own direction by trackAmount
    public Quaternion GetTrackingRotation(Vector3 camPos, Vector3 lookPoint)
    {
        Quaternion own = GetCameraRotation();
        if (!trackPlayer) return own;
        Vector3 dir = lookPoint - camPos;
        if (dir.sqrMagnitude < 1e-4f || Mathf.Abs(dir.normalized.y) > 0.999f) return own;
        Quaternion r = Quaternion.Slerp(own, Quaternion.LookRotation(dir, Vector3.up), trackAmount);
        Vector3 e = r.eulerAngles;
        float pitch = Mathf.DeltaAngle(0f, e.x);                    // + = looking down
        if (pitch > maxLookDown) r = Quaternion.Euler(maxLookDown, e.y, 0f);
        return r;
    }

    public Quaternion GetCameraRotation()
    {
        if (cameraAnchor == null) return Quaternion.identity;
        if (!keepLevel) return cameraAnchor.rotation;
        Vector3 f = cameraAnchor.forward;
        if (Mathf.Abs(f.y) > 0.999f) return cameraAnchor.rotation;   // looking straight up/down: roll is undefined
        return Quaternion.LookRotation(f, Vector3.up);
    }

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
        Gizmos.DrawWireSphere(cameraAnchor.position, 0.12f);
#if UNITY_EDITOR
        // also show the view when only the anchor itself is selected
        if (UnityEditor.Selection.Contains(cameraAnchor.gameObject)) DrawViewFrustum();
#endif
    }

    void OnDrawGizmosSelected()
    {
        DrawViewFrustum();
        if (followAmount > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boundsCenter, boundsSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    // The actual view (game camera's field of view and aspect), so the shot can be framed in the Scene view
    void DrawViewFrustum()
    {
        if (cameraAnchor == null) return;
        Camera cam = Camera.main;
        float fov = cam != null ? cam.fieldOfView : 60f;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(cameraAnchor.position, GetCameraRotation(), Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, fov, 6f, 0.2f, aspect);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
