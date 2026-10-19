using UnityEngine;
using UnityEngine.Audio;

// A pipe that drips: a drop slowly swells at the pipe mouth, lets go, falls to the ground and lands with a "plink" (sound + a few
// tiny splash droplets). The sound is played when the drop actually lands, so what you see and hear match. Timing is random so
// several pipes never drip in sync. Put the wet decal under the mouth by hand (the prefab already has one).
public class DrippingPipe : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Where the drops form and let go. Move it to the open end of the pipe.")]
    [SerializeField] Transform mouth;
    [Tooltip("A small mesh used as the water drop (any tiny sphere). Hidden between drops.")]
    [SerializeField] Transform drop;
    [Tooltip("Plays the landing sound. It is moved to where the drop lands, so the sound comes from the puddle.")]
    [SerializeField] AudioSource landSound;
    [SerializeField] AudioClip clip;
    [Tooltip("Optional. If filled, each drop plays a random clip from this list (never the same one twice in a row) instead of the single clip above - a repeating identical sample is what makes a drip annoying.")]
    [SerializeField] AudioClip[] clipVariations;
    [SerializeField] AudioMixerGroup mixerGroup;
    [Tooltip("A few tiny droplets thrown up when the drop lands. Its own emission should stay off.")]
    [SerializeField] ParticleSystem splash;

    [Header("Dripping")]
    [Tooltip("Seconds between drops (random in this range). The drop also needs a moment to swell and fall.")]
    [SerializeField] Vector2 secondsBetweenDrops = new Vector2(1.6f, 4.2f);
    [Tooltip("How long the drop hangs and swells at the mouth before it lets go (random in this range).")]
    [SerializeField] Vector2 swellSeconds = new Vector2(0.5f, 1.1f);
    [Range(0f, 1f)]
    [Tooltip("Chance of a quick second drop right after one.")]
    [SerializeField] float doubleDropChance = 0.18f;
    [Tooltip("Size of a full drop (metres).")]
    [SerializeField] float dropSize = 0.035f;
    [SerializeField] float gravity = 9.81f;

    [Header("Sound")]
    [Tooltip("Volume of each drop is random in this range (0-1).")]
    [SerializeField] Vector2 volume = new Vector2(0.65f, 1f);
    [Tooltip("Pitch of each drop is random in this range, so they do not sound identical.")]
    [SerializeField] Vector2 pitch = new Vector2(0.9f, 1.1f);
    [SerializeField] float minDistance = 1.5f;
    [Tooltip("The drip cannot be heard beyond this distance (fades smoothly to silence before it).")]
    [SerializeField] float maxDistance = 20f;
    [Tooltip("Loudness by distance: left = at the pipe, right = at Max Distance. Must reach 0 at the right edge.")]
    [SerializeField] AnimationCurve distanceFalloff = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.2f, 0.75f), new Keyframe(0.5f, 0.3f), new Keyframe(1f, 0f));

    enum Phase { Waiting, Swelling, Falling }
    Phase phase;
    float timer;
    float swellTime;
    float fallSpeed;
    float groundY;
    bool nextIsQuick;
    Vector3 landPoint;
    int lastVariation = -1;

    void Awake()
    {
        if (mouth == null) mouth = transform;
        SetupAudio();
        if (drop != null) drop.gameObject.SetActive(false);
    }

    void Start()
    {
        // Where do the drops land? Straight down from the mouth to whatever is below (ignoring triggers).
        groundY = mouth.position.y - 1f;
        Vector3 from = mouth.position + Vector3.down * 0.05f;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) groundY = hit.point.y;
        landPoint = new Vector3(mouth.position.x, groundY, mouth.position.z);
        if (landSound != null) landSound.transform.position = landPoint;

        timer = Random.Range(0.3f, secondsBetweenDrops.y);              // so copies never start together
        phase = Phase.Waiting;
    }

    void SetupAudio()
    {
        if (landSound == null) landSound = GetComponentInChildren<AudioSource>();
        if (landSound == null) return;
        landSound.playOnAwake = false;
        landSound.loop = false;
        landSound.spatialBlend = 1f;
        landSound.dopplerLevel = 0f;
        landSound.minDistance = minDistance;
        landSound.maxDistance = maxDistance;
        landSound.rolloffMode = AudioRolloffMode.Custom;
        landSound.SetCustomCurve(AudioSourceCurveType.CustomRolloff, distanceFalloff);    // x = distance / maxDistance, y = volume
        if (mixerGroup != null) landSound.outputAudioMixerGroup = mixerGroup;
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Waiting:
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginSwell();
                break;
            case Phase.Swelling:
                UpdateSwell();
                break;
            case Phase.Falling:
                UpdateFall();
                break;
        }
    }

    void BeginSwell()
    {
        phase = Phase.Swelling;
        timer = 0f;
        swellTime = nextIsQuick ? 0.15f : Random.Range(swellSeconds.x, swellSeconds.y);
        if (drop != null)
        {
            drop.gameObject.SetActive(true);
            drop.position = mouth.position;
            drop.localScale = Vector3.zero;
        }
    }

    void UpdateSwell()
    {
        timer += Time.deltaTime;
        float k = Mathf.Clamp01(timer / swellTime);
        if (drop != null)
        {
            // grows into a slightly pear-shaped bead that sags a little before letting go
            float s = dropSize * Mathf.SmoothStep(0.15f, 1f, k);
            drop.localScale = new Vector3(s, s * (1f + 0.35f * k * k), s);
            drop.position = mouth.position + Vector3.down * (0.35f * s * k * k);
        }
        if (k >= 1f)
        {
            phase = Phase.Falling;
            fallSpeed = 0f;
        }
    }

    void UpdateFall()
    {
        fallSpeed += gravity * Time.deltaTime;
        Vector3 p = drop != null ? drop.position : mouth.position;
        p.y -= fallSpeed * Time.deltaTime;
        if (drop != null)
        {
            drop.position = p;
            float stretch = 1f + Mathf.Clamp(fallSpeed * 0.35f, 0f, 2.2f);       // streaks as it speeds up
            drop.localScale = new Vector3(dropSize * 0.8f, dropSize * stretch, dropSize * 0.8f);
        }
        if (p.y <= groundY) Land();
    }

    void Land()
    {
        if (drop != null) drop.gameObject.SetActive(false);

        AudioClip toPlay = PickClip();
        if (landSound != null && toPlay != null)
        {
            landSound.pitch = Random.Range(pitch.x, pitch.y);
            landSound.PlayOneShot(toPlay, Random.Range(volume.x, volume.y));
        }
        if (splash != null)
        {
            splash.transform.position = landPoint + Vector3.up * 0.01f;
            splash.Emit(Random.Range(3, 7));
        }

        nextIsQuick = Random.value < doubleDropChance && !nextIsQuick;
        phase = Phase.Waiting;
        timer = nextIsQuick ? Random.Range(0.35f, 0.7f) : Random.Range(secondsBetweenDrops.x, secondsBetweenDrops.y);
    }

    AudioClip PickClip()
    {
        if (clipVariations == null || clipVariations.Length == 0) return clip;
        if (clipVariations.Length == 1) return clipVariations[0];
        int i = Random.Range(0, clipVariations.Length);
        if (i == lastVariation) i = (i + 1 + Random.Range(0, clipVariations.Length - 1)) % clipVariations.Length;
        lastVariation = i;
        return clipVariations[i];
    }

    void OnDrawGizmosSelected()
    {
        Transform m = mouth != null ? mouth : transform;
        Gizmos.color = new Color(0.4f, 0.7f, 1f);
        Gizmos.DrawWireSphere(m.position, 0.04f);
        if (Physics.Raycast(m.position + Vector3.down * 0.05f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            Gizmos.DrawLine(m.position, hit.point);
            Gizmos.DrawWireSphere(hit.point, maxDistance);                  // how far the drip can be heard
        }
    }
}
