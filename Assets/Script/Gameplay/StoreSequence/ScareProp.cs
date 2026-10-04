using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// A loose can / bottle that the fuse-hunt director drops or sets rolling. Plays a clink on every impact (louder for harder
/// hits) and a rolling rumble while it rolls along the floor. Added at runtime by FuseHuntDirector.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ScareProp : MonoBehaviour
{
    public AudioClip[] hitClips;
    public AudioClip[] tinkClips;
    public AudioClip rollLoop;
    public AudioMixerGroup sfxGroup;
    public float hitVolume = 0.9f;
    public float rollVolume = 0.55f;
    public float maxDistance = 28f;
    public float radius = 0.033f;

    Rigidbody rb;
    AudioSource oneShot, roll;
    float nextHit;

    public bool Settled { get; private set; }
    public float Age { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        oneShot = MakeSource("Hit", false);
        roll = MakeSource("Roll", true);
        roll.clip = rollLoop;
        roll.volume = 0f;
    }

    AudioSource MakeSource(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = loop;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 2.5f;
        a.maxDistance = maxDistance;
        a.dopplerLevel = 0f;
        a.outputAudioMixerGroup = sfxGroup;
        return a;
    }

    void OnCollisionEnter(Collision c)
    {
        float v = c.relativeVelocity.magnitude;
        if (v < 0.35f || Time.time < nextHit) return;
        nextHit = Time.time + 0.07f;
        var pool = v > 1.1f ? hitClips : tinkClips;
        if (pool == null || pool.Length == 0) pool = hitClips;
        if (pool == null || pool.Length == 0) return;
        oneShot.pitch = Random.Range(0.92f, 1.08f);
        oneShot.PlayOneShot(pool[Random.Range(0, pool.Length)], Mathf.Clamp(v / 3.5f, 0.12f, 1f) * hitVolume);
    }

    void Update()
    {
        Age += Time.deltaTime;
        if (rb == null) return;

        Vector3 v = rb.linearVelocity;
        float speed = new Vector3(v.x, 0f, v.z).magnitude;
        bool onFloor = Mathf.Abs(v.y) < 0.4f && Physics.Raycast(transform.position, Vector3.down, radius + 0.15f, ~LayerMask.GetMask("RainBlocker"), QueryTriggerInteraction.Ignore);
        float target = (onFloor && speed > 0.12f && rollLoop != null) ? Mathf.Clamp01(speed / 1.4f) * rollVolume : 0f;
        roll.volume = Mathf.MoveTowards(roll.volume, target, Time.deltaTime * 2.5f);
        roll.pitch = 0.75f + 0.55f * Mathf.Clamp01(speed / 2.2f);
        if (target > 0f && !roll.isPlaying) roll.Play();
        else if (roll.volume <= 0.001f && roll.isPlaying) roll.Stop();

        Settled = Age > 0.5f && rb.linearVelocity.sqrMagnitude < 0.0025f && rb.angularVelocity.sqrMagnitude < 0.05f;
    }
}
