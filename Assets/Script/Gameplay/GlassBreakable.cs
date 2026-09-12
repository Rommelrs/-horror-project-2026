using UnityEngine;
using UnityEngine.Events;

public class GlassBreakable : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("Explosion force applied to all pieces on break.")]
    [SerializeField] float explosionForce = 300f;
    [Tooltip("Radius of the explosion.")]
    [SerializeField] float explosionRadius = 1.5f;
    [Tooltip("Upward bias — how much pieces fly upward (0 = outward only).")]
    [SerializeField] float upwardModifier = 0.5f;
    [Tooltip("Optionally override the explosion origin. Leave empty to use this object's center.")]
    [SerializeField] Transform explosionOrigin;

    [Header("Optional Effects")]
    [Tooltip("Optional particle effect to play on break.")]
    [SerializeField] ParticleSystem shatterParticles;

    [Header("Audio")]
    [SerializeField] AudioClip shatterSound;
    [SerializeField] AudioSource audioSource;
    [SerializeField] float shatterVolume = 1f;

    [Header("Events")]
    public UnityEvent OnBroken;

    [Header("Debug")]
    [Tooltip("Press this key in Play mode to test the break effect.")]
    [SerializeField] KeyCode debugBreakKey = KeyCode.E;
    [SerializeField] bool enableDebugKey = true;

    bool isBroken = false;
    Rigidbody[] pieces;

    private void Awake()
    {
        // Collect all Rigidbodies on this GameObject and children
        pieces = GetComponentsInChildren<Rigidbody>(true);

        // Make all pieces kinematic so they stay frozen until Break() is called
        foreach (var rb in pieces)
        {
            rb.isKinematic = true;
        }
    }

    private void Update()
    {
        if (enableDebugKey && !isBroken && Input.GetKeyDown(debugBreakKey))
            Break();
    }

    /// <summary>Call this to shatter the glass/table with physics.</summary>
    public void Break()
    {
        if (isBroken) return;
        isBroken = true;

        Vector3 origin = explosionOrigin != null ? explosionOrigin.position : transform.position;

        foreach (var rb in pieces)
        {
            if (rb == null) continue;

            // Unfreeze
            rb.isKinematic = false;

            // Apply explosion force so pieces scatter
            rb.AddExplosionForce(explosionForce, origin, explosionRadius, upwardModifier, ForceMode.Impulse);
        }

        // Particles
        if (shatterParticles != null) shatterParticles.Play();

        // Sound
        if (shatterSound != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(shatterSound, shatterVolume);
            else
                AudioSource.PlayClipAtPoint(shatterSound, transform.position, shatterVolume);
        }

        OnBroken?.Invoke();
    }
}
