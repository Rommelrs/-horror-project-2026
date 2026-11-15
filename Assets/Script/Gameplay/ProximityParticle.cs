using UnityEngine;

/// <summary>
/// Dust glints that play while the player is near something that can be used or picked up. Once that thing has been used
/// for good (the fuse box has its fuse, the register its code, a pickup is gone) the dust stops and never comes back.
/// </summary>
public class ProximityParticle : MonoBehaviour
{
    [SerializeField] ParticleSystem particle;
    [SerializeField] float activationRange = 5f;
    [Tooltip("What this dust is for. Empty: the Interactable it sits under (if any). Fusebox: stops once it has power; cash register: once the code is in.")]
    [SerializeField] MonoBehaviour owner;

    bool finished;

    void Awake()
    {
        if (owner == null) owner = GetComponentInParent<Interactable>(true);
    }

    void Update()
    {
        if (finished || Player.instance == null || particle == null) return;

        if (OwnerDone())
        {
            finished = true;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            enabled = false;
            return;
        }

        float dist = Vector3.Distance(transform.position, Player.instance.transform.position);

        if (dist <= activationRange)
        {
            if (!particle.isPlaying)
                particle.Play();
        }
        else
        {
            if (particle.isPlaying)
                particle.Stop();
        }
    }

    bool OwnerDone()
    {
        if (owner == null) return false;
        if (owner is Fusebox fusebox) return fusebox.hasEnergy;
        if (owner is CashRegisterInteractable register) return register.CodeSolved;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationRange);
    }
}
