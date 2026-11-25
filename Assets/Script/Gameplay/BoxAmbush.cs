using System.Collections;
using UnityEngine;

/// <summary>
/// A pile of cartons with an enemy standing in it: the moment he is switched on (the group's trigger) the cartons burst outwards and he
/// is revealed. They never block the player. Already triggered in the save: the cartons are gone.
/// </summary>
public class BoxAmbush : MonoBehaviour
{
    [SerializeField] GameObject enemy;
    [SerializeField] SaveableTrigger trigger;
    [SerializeField] Rigidbody[] boxes;
    [SerializeField] float outward = 5.5f, upward = 4.2f, torque = 7f;
    [SerializeField] AudioClip[] burstClips;
    [Tooltip("The cartons are cleared away this long after the burst, once the player isn't looking / near (s).")]
    [SerializeField] float clearAfter = 14f;
    bool burst;

    void Start()
    {
        if (trigger != null && trigger.WasAlreadyTriggered()) { burst = true; foreach (var b in boxes) if (b != null) b.gameObject.SetActive(false); return; }
        foreach (var b in boxes) if (b != null) b.isKinematic = true;
    }

    void Update()
    {
        if (!burst && enemy != null && enemy.activeInHierarchy) Burst();
    }

    void Burst()
    {
        burst = true;
        var p = Player.instance;
        var pc = p != null ? p.controller : null;
        var enemyCols = enemy.GetComponentsInChildren<Collider>();
        foreach (var b in boxes)
        {
            if (b == null) continue;
            foreach (var c in b.GetComponentsInChildren<Collider>())
            {
                if (pc != null) Physics.IgnoreCollision(c, pc);
                foreach (var e in enemyCols) if (!e.isTrigger) Physics.IgnoreCollision(c, e);
            }
            b.isKinematic = false;
            Vector3 d = b.position - transform.position; d.y = 0f;
            d = d.sqrMagnitude > 0.01f ? d.normalized : Random.insideUnitSphere;
            b.linearVelocity = d * outward * Random.Range(0.7f, 1.3f) + Vector3.up * upward * Random.Range(0.6f, 1.2f);
            b.angularVelocity = Random.insideUnitSphere * torque;
        }
        if (burstClips != null && SoundEffectManager.instance != null)
            for (int i = 0; i < Mathf.Min(3, burstClips.Length); i++)
                SoundEffectManager.instance.PlaySFXAtPosition(burstClips[(i * 2) % burstClips.Length], transform.position + Vector3.up);
        StartCoroutine(Co_Clear());
    }

    IEnumerator Co_Clear()
    {
        yield return new WaitForSeconds(clearAfter);
        // not while he watches: each goes when it's off screen or far
        bool any = true;
        while (any)
        {
            any = false;
            var cam = Camera.main;
            foreach (var b in boxes)
            {
                if (b == null || !b.gameObject.activeSelf) continue;
                Vector3 v = cam != null ? cam.WorldToViewportPoint(b.position) : Vector3.back;
                bool seen = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f && v.z < 24f;
                if (seen) any = true; else b.gameObject.SetActive(false);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
