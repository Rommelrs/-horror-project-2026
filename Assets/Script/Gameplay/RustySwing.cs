using UnityEngine;

// A swing that sways on its own, like it's being nudged by a faint draught. The seat is a real damped
// pendulum (gravity pulls it back to the middle, friction slowly bleeds the motion off) pushed around by
// smooth, uneven gusts of wind, so it never repeats, drifts nearly still for a while, then sways again.
// The squeak is driven by how fast the swing is actually moving: silent when it hangs still, a faint rusty
// creak as it starts to drift, louder and slightly higher pitched on the faster passes.
public class RustySwing : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The empty object at the top bar that the chains hang from. It is rotated around its local X axis.")]
    [SerializeField] Transform pivot;
    [Tooltip("Looping squeak sound. Put it on the seat so the sound comes from where the swing actually is.")]
    [SerializeField] AudioSource squeakSource;

    [Header("Pendulum")]
    [Tooltip("Distance (metres) from the top bar to the seat. Longer = slower, lazier swing.")]
    [SerializeField] float chainLength = 1.75f;
    [Tooltip("Friction (per second). Higher = the swing settles back to hanging still sooner.")]
    [SerializeField] float damping = 0.45f;
    [Tooltip("The swing never goes further than this (degrees); it is gently pushed back as it nears it.")]
    [SerializeField] float softLimitAngle = 11f;

    [Header("Wind")]
    [Tooltip("How hard the wind nudges the swing. Around 0.2 barely moves it, 0.4 gives a slight sway, 0.8 is a proper swing.")]
    [SerializeField] float windStrength = 0.6f;
    [Tooltip("How quickly the gusts change (they are smooth noise). Lower = long slow gusts.")]
    [SerializeField] float gustSpeed = 0.21f;
    [Tooltip("Gives this swing its own wind pattern. Leave on 'random' for each copy to move differently.")]
    [SerializeField] bool randomSeed = true;
    [SerializeField] float seed = 0f;

    [Header("Squeak")]
    [Tooltip("Swing speed (radians/second) below which the swing is silent.")]
    [SerializeField] float silentBelowSpeed = 0.015f;
    [Tooltip("Swing speed at which the squeak reaches full volume.")]
    [SerializeField] float fullVolumeSpeed = 0.2f;
    [SerializeField] float maxVolume = 0.8f;
    [Tooltip("Pitch at the slowest and fastest moving.")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.94f, 1.06f);
    [Tooltip("How quickly the volume follows the swing's speed (seconds). Keeps the sound from cutting in and out.")]
    [SerializeField] float volumeSmoothTime = 0.35f;

    float angle;          // radians, 0 = hanging straight down
    float angularVelocity;
    float volumeVelocity;
    float currentVolume;
    float time;

    void Awake()
    {
        if (randomSeed) seed = Random.Range(0f, 1000f);
    }

    void Start()
    {
        time = seed;

        if (squeakSource != null)
        {
            squeakSource.loop = true;
            squeakSource.volume = 0f;
            squeakSource.playOnAwake = false;
            // start the loop at a random point so several swings never squeak in sync
            if (squeakSource.clip != null)
                squeakSource.time = Random.Range(0f, squeakSource.clip.length * 0.95f);
            squeakSource.Play();
        }
    }

    /// <summary>Gives the swing a shove (radians/second added to its swing speed). Positive swings it forward.</summary>
    public void Push(float impulse)
    {
        angularVelocity += impulse;
    }

    void Update()
    {
        // small fixed steps so the motion stays smooth and stable at any frame rate
        float remaining = Mathf.Min(Time.deltaTime, 0.1f);
        const float step = 1f / 120f;
        while (remaining > 0f)
        {
            float dt = Mathf.Min(step, remaining);
            Integrate(dt);
            remaining -= dt;
        }

        if (pivot != null)
            pivot.localRotation = Quaternion.Euler(angle * Mathf.Rad2Deg, 0f, 0f);

        UpdateSqueak();
    }

    void Integrate(float dt)
    {
        time += dt;

        // Two layers of smooth noise: slow uneven gusts, plus a faster, weaker flutter on top.
        float gust = Signed(Mathf.PerlinNoise(seed, time * gustSpeed) );
        gust = Mathf.Sign(gust) * Mathf.Pow(Mathf.Abs(gust), 1.6f);   // mostly calm, with the odd stronger gust
        float flutter = Signed(Mathf.PerlinNoise(seed + 41.7f, time * gustSpeed * 3.1f));
        float torque = windStrength * (gust + 0.35f * flutter);

        float accel = -(9.81f / chainLength) * Mathf.Sin(angle) - damping * angularVelocity + torque;

        float limit = softLimitAngle * Mathf.Deg2Rad;
        if (Mathf.Abs(angle) > limit)
            accel -= (Mathf.Abs(angle) - limit) * Mathf.Sign(angle) * 25f;

        angularVelocity += accel * dt;
        angle += angularVelocity * dt;
    }

    static float Signed(float perlin01) { return (perlin01 - 0.5f) * 2f; }

    void UpdateSqueak()
    {
        if (squeakSource == null) return;

        float speed = Mathf.Abs(angularVelocity);
        float t = Mathf.InverseLerp(silentBelowSpeed, fullVolumeSpeed, speed);
        float targetVolume = Mathf.SmoothStep(0f, 1f, t) * maxVolume;

        currentVolume = Mathf.SmoothDamp(currentVolume, targetVolume, ref volumeVelocity, volumeSmoothTime);
        squeakSource.volume = currentVolume;
        squeakSource.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, t);
    }
}
