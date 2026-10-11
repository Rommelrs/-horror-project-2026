using UnityEngine;

/// <summary>
/// A harmless, scripted stand-in for the Questioner: only the model and its animator, no AI, health, colliders or save data.
/// Something else (e.g. FootstepsTrigger) moves it and tells it to idle or walk. The 'Velocity' value picks the clip in the
/// Questioner's locomotion blend tree: 0 = idle, 0.5 = slow shuffle, about 1 = slow walk, 2 = normal walk.
/// </summary>
public class QuestionerPuppet : MonoBehaviour
{
    [SerializeField] Animator animator;
    [Tooltip("Copies of the body's materials using the DitherDissolve shader, in the same order as the renderer's materials. Swapped in when he fades away.")]
    [SerializeField] Material[] fadeMaterials;

    Renderer fadeRenderer;
    Material[] fadeInstances;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();   // active ones only: the Questioner prefab also holds a switched-off leftover zombie model
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public void Idle()
    {
        SetVelocity(0f);
    }

    /// <summary>velocity is the animator's blend value (roughly the walking speed in m/s).</summary>
    public void Walk(float velocity)
    {
        SetVelocity(velocity);
    }

    /// <summary>Plays the animation faster or slower (1 = normal). Used to keep the steps matching when he moves faster than the walk clip's own pace.</summary>
    public void SetAnimationSpeed(float s)
    {
        if (animator != null) animator.speed = s;
    }

    void SetVelocity(float v)
    {
        if (animator != null) animator.SetFloat("Velocity", v);
    }

    /// <summary>Swaps in the transparent materials so he can fade. Returns false if there are none set up.</summary>
    public bool BeginFade()
    {
        if (fadeInstances != null) return true;
        Renderer r = GetComponentInChildren<SkinnedMeshRenderer>();
        if (r == null || fadeMaterials == null || fadeMaterials.Length == 0 || fadeMaterials.Length != r.sharedMaterials.Length) return false;

        fadeRenderer = r;
        fadeInstances = new Material[fadeMaterials.Length];
        for (int i = 0; i < fadeInstances.Length; i++) fadeInstances[i] = new Material(fadeMaterials[i]);
        r.sharedMaterials = fadeInstances;
        return true;
    }

    /// <summary>1 = solid, 0 = gone. Only works after BeginFade(). He dissolves in a fine dither, staying an opaque material so the fog covers him like everything else.</summary>
    public void SetFadeAlpha(float a)
    {
        if (fadeInstances == null) return;
        a = Mathf.Clamp01(a);
        for (int i = 0; i < fadeInstances.Length; i++) fadeInstances[i].SetFloat("_Opacity", a);
    }

    void OnDestroy()
    {
        if (fadeInstances != null) foreach (Material m in fadeInstances) if (m != null) Destroy(m);
    }

    public void FaceInstant(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
