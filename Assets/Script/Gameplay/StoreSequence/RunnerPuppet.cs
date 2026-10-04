using UnityEngine;

/// <summary>
/// A non-hostile, scripted stand-in for the Runner enemy: only the model and its animator, no AI, health or colliders.
/// StoreFootstepsSequence moves it around and calls Idle / Walk / Run.
/// </summary>
public class RunnerPuppet : MonoBehaviour
{
    [SerializeField] Animator animator;
    [Tooltip("Animator 'Speed' value for the approach. The Runner only has Idle and Run clips, so he runs to the store (the Run clip at normal speed - slowed down it looked like slow motion).")]
    [SerializeField] float walkBlend = 1f;
    [SerializeField] float walkAnimatorSpeed = 1f;
    [SerializeField] float runBlend = 1f;
    [SerializeField] float runAnimatorSpeed = 1f;
    [SerializeField] float turnSpeed = 360f;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public void Idle()
    {
        Set(0f, 1f);
    }

    public void Walk()
    {
        Set(walkBlend, walkAnimatorSpeed);
    }

    public void Run()
    {
        Set(runBlend, runAnimatorSpeed);
    }

    void Set(float speedParam, float animatorSpeed)
    {
        if (animator == null) return;
        animator.SetFloat("Speed", speedParam);
        animator.SetFloat("Velocity", speedParam);
        animator.speed = animatorSpeed;
    }

    public void FaceDirection(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
    }

    public void FaceInstant(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
