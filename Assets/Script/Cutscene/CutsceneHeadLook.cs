using UnityEngine;

// Turns a character's neck + head toward a target on top of whatever animation is playing (runs after the Animator).
// Used in cutscenes for small beats like a head turn, without needing a dedicated clip. weight 0 = animation only.
public class CutsceneHeadLook : MonoBehaviour
{
    public Transform neck;
    public Transform head;
    public Transform target;
    [Tooltip("Where the body faces (for Khalid the Player object: his model is turned inside it); this object if empty.")]
    public Transform facing;
    [Range(0f, 1f)] public float weight;
    [Tooltip("Most the head may turn away from where the animation points it.")]
    public float maxAngle = 70f;
    [Tooltip("Share of the turn done by the neck (the head does the rest).")]
    [Range(0f, 1f)] public float neckShare = 0.4f;
    [Tooltip("How much of the up/down difference the head follows (0 = only turns sideways). Raise it when the target is close and much higher or lower.")]
    [Range(0f, 1f)] public float pitchShare = 0.15f;
    [Tooltip("Most the head may tip up or down.")]
    public float maxPitch = 40f;
    [Tooltip("Seconds to ease the weight toward its target value.")]
    public float smoothTime = 0.25f;

    [HideInInspector] public float targetWeight;
    float weightVel;

    void LateUpdate()
    {
        weight = Mathf.SmoothDamp(weight, targetWeight, ref weightVel, smoothTime);
        if (weight <= 0.001f || head == null || target == null) return;

        Vector3 toTarget = target.position - head.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        // measured from the body's facing (the animated head looks roughly along it): a turn about the vertical, then a
        // tip up/down about the body's sideways axis - kept apart, so the head never rolls (one combined arc did)
        Vector3 fwd = Vector3.ProjectOnPlane((facing != null ? facing : transform).forward, Vector3.up).normalized;
        Vector3 flatTo = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        float yaw = flatTo.sqrMagnitude > 1e-6f ? Mathf.Clamp(Vector3.SignedAngle(fwd, flatTo, Vector3.up), -maxAngle, maxAngle) : 0f;
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(toTarget.normalized.y, -1f, 1f)) * Mathf.Rad2Deg * pitchShare, -maxPitch, maxPitch);
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        Quaternion full = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, side);
        Quaternion part = Quaternion.Slerp(Quaternion.identity, full, weight);

        if (neck != null)
        {
            Quaternion n = Quaternion.Slerp(Quaternion.identity, part, neckShare);
            neck.rotation = n * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, part, 1f - neckShare) * head.rotation;
        }
        else head.rotation = part * head.rotation;
    }
}
