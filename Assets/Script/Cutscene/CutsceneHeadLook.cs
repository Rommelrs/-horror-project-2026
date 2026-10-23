using UnityEngine;

// Turns a character's neck + head toward a target on top of whatever animation is playing (runs after the Animator).
// Used in cutscenes for small beats like a head turn, without needing a dedicated clip. weight 0 = animation only.
public class CutsceneHeadLook : MonoBehaviour
{
    public Transform neck;
    public Transform head;
    public Transform target;
    [Range(0f, 1f)] public float weight;
    [Tooltip("Most the head may turn away from where the animation points it.")]
    public float maxAngle = 70f;
    [Tooltip("Share of the turn done by the neck (the head does the rest).")]
    [Range(0f, 1f)] public float neckShare = 0.4f;
    [Tooltip("How much of the up/down difference the head follows (0 = only turns sideways). Raise it when the target is close and much higher or lower.")]
    [Range(0f, 1f)] public float pitchShare = 0.15f;
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

        // turn measured from the body's facing (the animated head looks roughly along it), mostly sideways, a little up/down
        Vector3 bodyFwd = transform.forward;
        Vector3 want = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized * (1f - pitchShare) + Vector3.up * toTarget.normalized.y * pitchShare;
        Quaternion full = Quaternion.FromToRotation(bodyFwd, want);
        full = Quaternion.RotateTowards(Quaternion.identity, full, maxAngle);
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
