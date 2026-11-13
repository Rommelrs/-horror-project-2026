using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// An enemy placed right against a wall or a prop in the editor gets slid out a little when Play starts: his NavMeshAgent puts him
/// on the nearest walkable spot, and the walkable area stops short of walls by the agent radius. This keeps the look the level was
/// built with: the visible body (the Animator's object) is drawn at the placed position until he first moves, then it eases back
/// onto the agent. The placed position is recorded automatically whenever he is moved in the editor.
/// </summary>
[ExecuteAlways]
public class PlacedPoseHold : MonoBehaviour
{
    [SerializeField] Vector3 placedPosition;
    [Tooltip("The visible body (defaults to the Animator's object).")]
    [SerializeField] Transform body;
    [Tooltip("A snap further than this means he was really placed off the walkable area: nothing is held then.")]
    [SerializeField] float maxHold = 0.8f;
    [Tooltip("Seconds to ease back onto the agent once he moves.")]
    [SerializeField] float releaseTime = 0.35f;

    NavMeshAgent agent;
    Vector3 offset;          // world, horizontal
    Vector3 bodyRest;        // the body's own local position
    Vector3 startPosition;
    bool holding, releasing;
    float releaseT;

    void Update()
    {
        if (!Application.isPlaying)
        {
            if ((placedPosition - transform.position).sqrMagnitude > 1e-8f)
            {
                placedPosition = transform.position;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }
    }

    int startFrame = -1;
    bool initialized;

    void Start()
    {
        if (!Application.isPlaying) return;
        if (body == null) { var a = GetComponentInChildren<Animator>(); if (a != null && a.transform != transform) body = a.transform; }
        agent = GetComponent<NavMeshAgent>();
        startFrame = Time.frameCount;
    }

    // the agent puts him on the walkable area during its first update, not before Start: measure the slide a couple of frames in
    void Init()
    {
        initialized = true;
        if (body == null) return;
        offset = placedPosition - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.0004f || offset.magnitude > maxHold) return;
        bodyRest = body.localPosition;
        startPosition = transform.position;
        holding = true;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || startFrame < 0) return;
        if (!initialized)
        {
            if (Time.frameCount - startFrame < 2) return;
            Init();
        }
        if (!holding) return;
        if (!releasing)
        {
            bool moving = (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.0025f) ||
                          (transform.position - startPosition).sqrMagnitude > 0.01f;
            if (moving) releasing = true;
            else { Apply(1f); return; }
        }
        releaseT += Time.deltaTime / Mathf.Max(0.01f, releaseTime);
        Apply(1f - Mathf.Clamp01(releaseT));
        if (releaseT >= 1f) { holding = false; body.localPosition = bodyRest; }
    }

    void Apply(float weight)
    {
        Vector3 localOffset = body.parent != null ? body.parent.InverseTransformVector(offset) : offset;
        body.localPosition = bodyRest + localOffset * weight;
    }
}
