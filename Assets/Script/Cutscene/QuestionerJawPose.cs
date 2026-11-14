using UnityEngine;

/// <summary>
/// Closes the Questioner's permanently split jaw. The model's mouth is built hanging open; turning the Jaw bone up and
/// shortening it reads as a closed mouth with the teeth clenched. 'closed' 1 = shut, 0 = the model's own (open) jaw.
/// Runs after the Animator, so it works on top of any clip.
/// </summary>
[DefaultExecutionOrder(200)]
public class QuestionerJawPose : MonoBehaviour
{
    public Transform jaw;
    [Range(0f, 1f)] public float closed = 1f;
    [Tooltip("Degrees the jaw swings up when shut.")]
    public float closedAngle = 68f;
    [Tooltip("Length of the jaw when shut (it is modelled stretched; at full length it sticks out like a snout).")]
    public float closedLength = 0.52f;

    Quaternion rest;
    Quaternion lastWritten;
    bool hasWritten;

    void Awake()
    {
        if (jaw == null)
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Jaw") { jaw = t; break; }
        if (jaw != null) rest = jaw.localRotation;
    }

    void LateUpdate()
    {
        if (jaw == null) return;
        // if no clip moved the jaw this frame it still holds what we wrote: start again from the rest pose
        Quaternion basis = hasWritten && jaw.localRotation == lastWritten ? rest : jaw.localRotation;
        float k = Mathf.Clamp01(closed);
        jaw.localRotation = basis * Quaternion.Euler(closedAngle * k, 0f, 0f);
        jaw.localScale = new Vector3(1f, Mathf.Lerp(1f, closedLength, k), 1f);
        lastWritten = jaw.localRotation;
        hasWritten = true;
    }
}
