using UnityEngine;

// A broken electrical box with a live wire dangling out of it. Most of the time it just buzzes and crackles quietly (the
// light at the exposed end barely twitches). Every few seconds it arcs: a short stutter of bright light, a burst of sparks and
// a louder buzz with a pitch wobble, sometimes followed by a quick second arc. The loose wires hang and sway a little,
// and jolt when it arcs. All timing is random so several boxes never fire in sync.
public class BrokenCircuitBox : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Light sitting at the live wire end.")]
    [SerializeField] Light sparkLight;
    [Tooltip("Looping electrical buzz. Put it on the box so the sound comes from it.")]
    [SerializeField] AudioSource buzz;
    [Tooltip("Spark particles at the live wire end (emission is driven by this script, leave its own emission off).")]
    [SerializeField] ParticleSystem sparks;
    [Tooltip("Roots of the hanging wires. Each is rotated slightly so the wires sway.")]
    [SerializeField] Transform[] swayingWires;

    [Header("Arcing")]
    [Tooltip("Seconds between arcs (random in this range).")]
    [SerializeField] Vector2 secondsBetweenArcs = new Vector2(2.5f, 7f);
    [Tooltip("How long one arc lasts (random in this range).")]
    [SerializeField] Vector2 arcDuration = new Vector2(0.12f, 0.45f);
    [Range(0f, 1f)]
    [Tooltip("Chance that an arc is followed by a quick second one.")]
    [SerializeField] float doubleArcChance = 0.35f;
    [SerializeField] Vector2Int sparksPerArc = new Vector2Int(6, 16);

    [Header("Light")]
    [Tooltip("Faint glow between arcs.")]
    [SerializeField] float idleLightIntensity = 0.12f;
    [Tooltip("Peak brightness during an arc.")]
    [SerializeField] float arcLightIntensity = 2.6f;

    [Header("Sound")]
    [SerializeField] float idleVolume = 0.3f;
    [SerializeField] float arcVolume = 0.95f;
    [Tooltip("Pitch wobble while arcing (+/- this much).")]
    [SerializeField] float arcPitchWobble = 0.08f;

    [Header("Wires")]
    [Tooltip("How far (degrees) the hanging wires swing.")]
    [SerializeField] float swayAngle = 3.5f;

    float seed;
    float nextArcTime;
    float arcEndTime;
    bool arcing;
    bool doubleArcPending;
    float lastSparkTime;
    float volume;
    float volumeVelocity;
    float jolt;
    float joltVelocity;

    void Awake()
    {
        seed = Random.Range(0f, 1000f);
    }

    void Start()
    {
        nextArcTime = Time.time + Random.Range(0.5f, secondsBetweenArcs.y);

        if (buzz != null)
        {
            buzz.loop = true;
            buzz.playOnAwake = false;
            buzz.volume = 0f;
            if (buzz.clip != null) buzz.time = Random.Range(0f, buzz.clip.length * 0.9f);   // so copies never buzz in sync
            buzz.Play();
        }
        volume = idleVolume;
    }

    void Update()
    {
        float now = Time.time;

        if (!arcing && now >= nextArcTime)
            StartArc(now);
        else if (arcing && now >= arcEndTime)
            EndArc(now);

        UpdateLightAndSound(now);
        UpdateWires(now);
    }

    void StartArc(float now)
    {
        arcing = true;
        arcEndTime = now + Random.Range(arcDuration.x, arcDuration.y);
        jolt = 1f;
        EmitSparks(Random.Range(sparksPerArc.x, sparksPerArc.y + 1));
        lastSparkTime = now;
    }

    void EndArc(float now)
    {
        arcing = false;
        if (!doubleArcPending && Random.value < doubleArcChance)
        {
            doubleArcPending = true;
            nextArcTime = now + Random.Range(0.08f, 0.25f);
        }
        else
        {
            doubleArcPending = false;
            nextArcTime = now + Random.Range(secondsBetweenArcs.x, secondsBetweenArcs.y);
        }
    }

    void EmitSparks(int count)
    {
        if (sparks != null) sparks.Emit(count);
    }

    void UpdateLightAndSound(float now)
    {
        float slow = Mathf.PerlinNoise(seed, now * 3.1f);                 // 0..1, wandering
        float fast = Mathf.PerlinNoise(seed + 9.7f, now * 38f);           // 0..1, rapid stutter
        float targetVolume;
        float lightLevel;

        if (arcing)
        {
            float stutter = Mathf.Clamp01(0.25f + fast * 1.1f);            // the arc stutters on and off
            lightLevel = arcLightIntensity * stutter;
            targetVolume = Mathf.Lerp(idleVolume, arcVolume, 0.55f + 0.45f * fast);

            if (now - lastSparkTime > 0.045f)                              // a few more sparks while it holds
            {
                EmitSparks(Random.Range(1, 4));
                lastSparkTime = now;
            }
            if (buzz != null) buzz.pitch = 1f + (fast - 0.5f) * 2f * arcPitchWobble;
        }
        else
        {
            float crackle = fast > 0.82f ? 1.6f : 1f;                      // an occasional little twitch between arcs
            lightLevel = idleLightIntensity * (0.4f + 1.2f * slow) * crackle;
            targetVolume = idleVolume * (0.8f + 0.4f * slow) * crackle * 0.8f;
            if (buzz != null) buzz.pitch = Mathf.Lerp(buzz.pitch, 1f, Time.deltaTime * 6f);
        }

        if (sparkLight != null) sparkLight.intensity = lightLevel;

        volume = Mathf.SmoothDamp(volume, targetVolume, ref volumeVelocity, arcing ? 0.03f : 0.2f);
        if (buzz != null) buzz.volume = Mathf.Clamp01(volume);
    }

    void UpdateWires(float now)
    {
        if (swayingWires == null) return;
        jolt = Mathf.SmoothDamp(jolt, 0f, ref joltVelocity, 0.35f);

        for (int i = 0; i < swayingWires.Length; i++)
        {
            Transform w = swayingWires[i];
            if (w == null) continue;
            float n1 = Mathf.PerlinNoise(seed + i * 5.3f, now * 0.45f) - 0.5f;
            float n2 = Mathf.PerlinNoise(seed + i * 5.3f + 40f, now * 0.38f) - 0.5f;
            float kick = jolt * 4f * (i % 2 == 0 ? 1f : -1f);
            w.localRotation = Quaternion.Euler(n1 * 2f * swayAngle + kick, 0f, n2 * 2f * swayAngle);
        }
    }
}
