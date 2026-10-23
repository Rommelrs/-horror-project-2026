using UnityEngine;

// Two-bone arm IK for cutscenes, applied on top of whatever animation is playing (LateUpdate, after the Animator):
// a humanoid's hands (upper arm -> forearm -> hand chain) are put on two target points, e.g. the coward grabbing
// Khalid by the shoulders, whatever his body clip is doing. `weight` eases toward `targetWeight`.
public class CutsceneArmIK : MonoBehaviour
{
    [System.Serializable]
    public class Arm
    {
        public Transform upper, lower, hand;
        [Tooltip("The point the wrist is put on, plus an offset in targetSpace (x right, y up, z forward).")]
        public Transform target;
        public Transform targetSpace;
        public Vector3 targetOffset;
        [Tooltip("Which way the elbow points (in this character's space: x right, y up, z forward).")]
        public Vector3 elbowHint = new Vector3(0.25f, -0.35f, 0f);
        [Tooltip("Bones that give the hand's orientation: the middle finger's base (fingers) and the thumb's base.")]
        public Transform fingerRef, thumbRef;
        [Tooltip("Which way the thumb points (character space), e.g. in toward the other hand.")]
        public Vector3 thumbDirection = Vector3.right;
        [Tooltip("Finger joints curled toward the palm while holding (base to tip, finger after finger).")]
        public Transform[] curlBones = new Transform[0];
    }

    [Tooltip("The character's facing (x right, y up, z forward) for the hints and directions below; this object if empty.")]
    public Transform space;
    public Arm left = new Arm(), right = new Arm();
    [Tooltip("The fingers point this way (character space): forward and a little down = curled over a shoulder.")]
    public Vector3 fingerDirection = new Vector3(0f, -0.35f, 1f);
    [Tooltip("Degrees each finger joint bends toward the palm while holding.")]
    public float curl = 35f;
    [Tooltip("Which way the palms face while holding (character space).")]
    public Vector3 palmDirection = new Vector3(0f, -1f, 0.15f);
    [Range(0f, 1f)] public float weight;
    [HideInInspector] public float targetWeight;
    public float smoothTime = 0.25f;
    float weightVel;

    Transform Space => space != null ? space : transform;

    void LateUpdate()
    {
        weight = Mathf.SmoothDamp(weight, targetWeight, ref weightVel, smoothTime);
        if (weight <= 0.001f) return;
        Hold(left);
        Hold(right);
    }

    // rotate the upper arm, then the forearm (the hand follows), then turn the hand: fingers along fingerDirection,
    // thumb toward thumbDirection
    void Hold(Arm a)
    {
        if (a.upper == null || a.lower == null || a.hand == null || a.target == null) return;
        Vector3 t = a.target.position + (a.targetSpace != null ? a.targetSpace.TransformDirection(a.targetOffset) : a.targetOffset);
        Vector3 s = a.upper.position, e = a.lower.position, w = a.hand.position;
        Solve(s, e, w, Vector3.Lerp(w, t, weight), e + Space.TransformDirection(a.elbowHint), out var e2, out var w2);

        a.upper.rotation = Quaternion.FromToRotation(e - s, e2 - s) * a.upper.rotation;
        Vector3 wMid = a.hand.position;
        a.lower.rotation = Quaternion.FromToRotation(wMid - a.lower.position, w2 - a.lower.position) * a.lower.rotation;

        Vector3 fingers = Space.TransformDirection(fingerDirection).normalized;
        Vector3 along = a.fingerRef != null ? a.fingerRef.position - a.hand.position : (a.hand.childCount > 0 ? a.hand.GetChild(0).position - a.hand.position : a.hand.up);
        Quaternion grip = Quaternion.FromToRotation(along, fingers) * a.hand.rotation;
        if (a.thumbRef != null)
        {
            // twist about the finger axis so the thumb points where it should
            Vector3 thumbNow = grip * Quaternion.Inverse(a.hand.rotation) * (a.thumbRef.position - a.hand.position);
            Vector3 thumbWant = Space.TransformDirection(a.thumbDirection);
            Vector3 from = Vector3.ProjectOnPlane(thumbNow, fingers), to = Vector3.ProjectOnPlane(thumbWant, fingers);
            if (from.sqrMagnitude > 1e-6f && to.sqrMagnitude > 1e-6f)
                grip = Quaternion.AngleAxis(Vector3.SignedAngle(from, to, fingers), fingers) * grip;
        }
        a.hand.rotation = Quaternion.Slerp(a.hand.rotation, grip, weight);

        // fingers wrapped over the shoulder
        Vector3 palm = Space.TransformDirection(palmDirection).normalized;
        foreach (var b in a.curlBones)
        {
            if (b == null || b.childCount == 0) continue;
            Vector3 dir = b.GetChild(0).position - b.position;
            Vector3 axis = Vector3.Cross(dir, palm);
            if (axis.sqrMagnitude < 1e-8f) continue;
            b.rotation = Quaternion.AngleAxis(curl * weight, axis.normalized) * b.rotation;
        }
    }

    // shoulder s, elbow e, wrist w; the wrist goes to t (as far as the arm reaches), the elbow bends toward hint
    static void Solve(Vector3 s, Vector3 e, Vector3 w, Vector3 t, Vector3 hint, out Vector3 e2, out Vector3 w2)
    {
        float la = (e - s).magnitude, lb = (w - e).magnitude;
        Vector3 toT = t - s;
        float c = Mathf.Clamp(toT.magnitude, 0.01f, la + lb - 0.001f);
        Vector3 d = toT.normalized;
        float cosA = Mathf.Clamp((la * la + c * c - lb * lb) / (2f * la * c), -1f, 1f);
        Vector3 axis = Vector3.Cross(d, hint - s);
        if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(d, Vector3.down);
        e2 = s + Quaternion.AngleAxis(Mathf.Acos(cosA) * Mathf.Rad2Deg, axis.normalized) * d * la;
        w2 = s + d * c;
    }
}
