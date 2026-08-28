using UnityEngine;

public class ProximityParticle : MonoBehaviour
{
    [SerializeField] ParticleSystem particle;
    [SerializeField] float activationRange = 5f;

    void Update()
    {
        if (Player.instance == null || particle == null) return;

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationRange);
    }
}
