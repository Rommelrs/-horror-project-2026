using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// A trigger zone that plays a "someone is walking nearby" sound when the player walks into it. The sound comes from an
/// EMITTER object you can place anywhere in the world (proximity / 3D audio: you hear which direction it comes from and it
/// is louder the closer you are). It starts heavy and fades away over time, optionally getting more muffled as it goes, and
/// the emitter can optionally walk from where it starts to a target, so the steps pass by or move away.
/// Add a SaveableTrigger (+ UniqueID) to the same object if it should only ever play once across saves.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FootstepsTrigger : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField] AudioClip clip;
    [SerializeField] AudioMixerGroup mixerGroup;
    [Tooltip("Pitch of the footsteps (also scales how long they last). Slightly lower sounds heavier.")]
    [SerializeField] float pitch = 1f;

    [Header("Position (proximity audio)")]
    [Tooltip("Where the footsteps are heard from. Move this child object anywhere in the level. If empty, the trigger's own position is used.")]
    [SerializeField] Transform emitter;
    [Tooltip("Optional. If set, the emitter walks from where it starts to this object while the sound plays (footsteps passing by or leaving).")]
    [SerializeField] Transform walkTo;
    [Tooltip("Closer than this the sound is full volume.")]
    [SerializeField] float minDistance = 5f;
    [Tooltip("The sound cannot be heard beyond this distance (it fades smoothly to silence before it).")]
    [SerializeField] float maxDistance = 30f;

    [Tooltip("How loud the sound is at each distance. Left = right at the emitter, right = at Max Distance. Raise the middle of the curve to make it carry louder; it must reach 0 at the right edge.")]
    [SerializeField] AnimationCurve distanceFalloff = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.2f, 0.88f), new Keyframe(0.45f, 0.52f), new Keyframe(0.75f, 0.2f), new Keyframe(1f, 0f));

    [Header("Heavy, then fading")]
    [Range(0f, 1.5f)]
    [SerializeField] float startVolume = 1f;
    [Range(0f, 1.5f)]
    [Tooltip("Volume at the very end of the sound.")]
    [SerializeField] float endVolume = 0f;
    [Tooltip("How long the sound lasts, in seconds. 0 = the whole clip.")]
    [SerializeField] float duration = 0f;
    [Tooltip("Left = start, right = end. High = full heavy volume, low = faded. Starts heavy and drops away.")]
    [SerializeField] AnimationCurve fade = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.15f, 0.9f), new Keyframe(0.5f, 0.38f), new Keyframe(1f, 0f));
    [Tooltip("The steps also get more muffled (like they are walking further away) as the sound fades.")]
    [SerializeField] bool muffleAsItFades = true;
    [Tooltip("Lowest muffle cutoff (Hz) at the end of the sound.")]
    [SerializeField] float mostMuffledCutoff = 900f;

    [Header("Walking figure (optional)")]
    [Tooltip("If set, this is spawned at the emitter the moment the player enters, and walks away from the player. The footsteps follow him for as long as he walks. Use a harmless puppet such as QuestionerPuppet, never the real enemy.")]
    [SerializeField] GameObject figurePrefab;
    [Tooltip("His normal, slow walking speed (m/s) while the player is far away. About 0.5 is a shuffle, 1 a slow walk, 2 a normal walk.")]
    [SerializeField] float figureWalkSpeed = 0.9f;
    [Tooltip("The most he can ever speed up to (m/s). Keep it above the player's sprint speed or he can be caught.")]
    [SerializeField] float figureMaxSpeed = 5f;
    [Tooltip("The gap (metres) he tries to keep to the player. Walk or run up to him and he matches your speed to keep this distance.")]
    [SerializeField] float figureKeepDistance = 7f;
    [Tooltip("Player distance (metres) inside which he reacts to the player. Further away he just walks slowly.")]
    [SerializeField] float figureReactDistance = 16f;
    [Tooltip("How hard he speeds up when the player is closer than the gap he wants. Higher = he pulls away faster.")]
    [SerializeField] float figureSpeedGain = 1.2f;
    [Tooltip("How quickly (seconds) his speed follows the situation. Higher = smoother and slower to react.")]
    [SerializeField] float figureSpeedSmoothTime = 0.35f;

    [Header("Walking figure - vanishing")]
    [Tooltip("He vanishes once the player has been chasing him and looking at him for this many seconds. (Looking away or stopping lets it drain back down.)")]
    [SerializeField] float figureVanishAfterLookingSeconds = 2.5f;
    [Tooltip("Seconds he takes to fade away once the player has looked at him long enough. He keeps walking while he fades, and the footsteps fade with him. 0 = vanishes instantly.")]
    [SerializeField] float figureFadeOutSeconds = 1.2f;
    [Tooltip("The player must be moving at least this fast (m/s) for it to count as chasing him. Walking is about 1.5-2, sprinting about 3.")]
    [SerializeField] float figureChaseMinPlayerSpeed = 1f;
    [Tooltip("The player must be within this distance (metres) of him for looking at him to count.")]
    [SerializeField] float figureLookDistance = 18f;
    [Tooltip("Length of his path (metres). He walks on until the player looks at him; only at the very end of the path does he vanish just for being out of sight.")]
    [SerializeField] float figureWalkDistance = 150f;
    [Tooltip("Beyond this distance (metres) he is hidden by the dust/fog, so the player cannot see him (looking at him doesn't count). Match it to how far you can see in the fog.")]
    [SerializeField] float figureHiddenByFogDistance = 28f;
    [Tooltip("At the end of his path he vanishes once the player has not been able to see him for this long.")]
    [SerializeField] float figureHiddenSeconds = 0.6f;

    [Header("Walking figure - footsteps")]
    [Tooltip("Footstep volume while he keeps walking, after the heavy start has faded (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] float figureSustainVolume = 0.55f;
    [Tooltip("Muffle cutoff (Hz) while he keeps walking. Lower = more muffled.")]
    [SerializeField] float figureSustainCutoff = 7000f;
    [Tooltip("Footsteps pitch up by this much (1 = no change) when he moves at Figure Max Speed, so faster steps sound faster.")]
    [SerializeField] float figureFootstepPitchAtMaxSpeed = 1.3f;
    [Tooltip("Seconds the footsteps take to fade out when he vanishes.")]
    [SerializeField] float figureSoundFadeOut = 0.6f;

    [Header("Trigger")]
    [Tooltip("Play only the first time the player enters.")]
    [SerializeField] bool playOnce = true;
    [Tooltip("Seconds to wait after the player enters before the sound begins.")]
    [SerializeField] float delay = 0f;
    [SerializeField] bool destroyTriggerAfterPlaying = false;
    [Tooltip("Triggers with the same group name only let ONE of them fire: when one plays, all the others in the group are disabled for good (until the scene reloads, or for good if they have a SaveableTrigger). Leave empty for a trigger that works on its own.")]
    [SerializeField] string exclusiveGroup = "";

    AudioSource source;
    AudioLowPassFilter lowPass;
    Coroutine playCR;
    bool hasPlayed;
    Vector3 emitterHomeLocal;
    Transform figure;
    float figureSpeed;
    float figureAlpha = 1f;                            // 1 = fully there, 0 = faded away (the footsteps follow it)
    bool disabledByGroup;

    static readonly List<FootstepsTrigger> all = new List<FootstepsTrigger>();

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        if (emitter == null) emitter = transform;
        emitterHomeLocal = emitter.localPosition;
        SetupSource();
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDestroy()
    {
        all.Remove(this);
    }

    // Another trigger in the same group fired: this one will never play now.
    void DisableFromGroup()
    {
        disabledByGroup = true;
        SaveableTrigger saveable = GetComponent<SaveableTrigger>();
        if (saveable != null) saveable.MarkAsTriggered();     // stays disabled after loading a save
    }

    void DisableGroupMates()
    {
        if (string.IsNullOrEmpty(exclusiveGroup)) return;
        foreach (FootstepsTrigger other in all)
            if (other != this && other != null && other.exclusiveGroup == exclusiveGroup) other.DisableFromGroup();
    }

    void SetupSource()
    {
        source = emitter.GetComponent<AudioSource>();
        if (source == null) source = emitter.gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
        source.volume = 0f;
        source.pitch = pitch;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, distanceFalloff);    // x = distance / maxDistance, y = volume
        if (mixerGroup != null) source.outputAudioMixerGroup = mixerGroup;

        if (muffleAsItFades)
        {
            lowPass = emitter.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = emitter.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Player>() == null) return;
        if (disabledByGroup) return;                       // another trigger in the group already fired
        if (playCR != null || figure != null) return;     // already playing
        if (playOnce && hasPlayed) return;

        SaveableTrigger saveable = GetComponent<SaveableTrigger>();
        if (saveable != null && saveable.WasAlreadyTriggered())
        {
            hasPlayed = true;
            return;
        }

        Play();
    }

    /// <summary>Starts the footsteps (also callable from UnityEvents / timelines).</summary>
    public void Play()
    {
        if (clip == null || source == null || playCR != null) return;
        hasPlayed = true;
        DisableGroupMates();

        SaveableTrigger saveable = GetComponent<SaveableTrigger>();
        if (saveable != null) saveable.MarkAsTriggered();

        playCR = StartCoroutine(Co_Play());
    }

    IEnumerator Co_Play()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float length = duration > 0f ? duration : clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch));    // how long the heavy start lasts
        Vector3 startPos = emitter.position;
        Vector3 endPos = walkTo != null ? walkTo.position : startPos;

        if (figurePrefab != null) SpawnFigure();
        bool follow = figure != null;                      // with a walking figure the steps go on for as long as he walks

        source.clip = clip;
        source.loop = follow;
        source.pitch = pitch;
        source.volume = startVolume;
        source.time = 0f;
        source.Play();

        float t = 0f;
        while (follow ? figure != null : t < length)
        {
            float n = Mathf.Clamp01(t / length);           // 0..1 through the heavy start
            float f = Mathf.Clamp01(fade.Evaluate(n));     // 1 = heavy, 0 = faded
            float quietest = follow ? figureSustainVolume : endVolume;
            source.volume = Mathf.Lerp(quietest, startVolume, f) * (follow ? figureAlpha : 1f);
            if (lowPass != null) lowPass.cutoffFrequency = Mathf.Lerp(follow ? figureSustainCutoff : mostMuffledCutoff, 22000f, f * f);

            if (follow)
            {
                // faster when he speeds up; the steps come from wherever he is
                source.pitch = pitch * Mathf.Lerp(1f, figureFootstepPitchAtMaxSpeed, Mathf.InverseLerp(figureWalkSpeed, figureMaxSpeed, figureSpeed));
                emitter.position = figure.position + Vector3.up;
            }
            else if (walkTo != null)
            {
                emitter.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, n));
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (follow)                                        // he is gone: the steps fade away where he was
        {
            float from = source.volume;
            for (float k = 0f; k < figureSoundFadeOut; k += Time.deltaTime)
            {
                source.volume = Mathf.Lerp(from, 0f, k / figureSoundFadeOut);
                yield return null;
            }
        }

        source.Stop();
        source.loop = false;
        source.volume = 0f;
        source.pitch = pitch;
        if (lowPass != null) lowPass.cutoffFrequency = 22000f;
        emitter.localPosition = emitterHomeLocal;           // back to where it was placed, so it can play again
        playCR = null;

        if (destroyTriggerAfterPlaying && figure == null) Destroy(gameObject);
    }

    // The figure appears where the emitter is and walks straight away from the player, past the emitter, until figureWalkDistance.
    void SpawnFigure()
    {
        Vector3 spawn = emitter.position;
        Player player = FindObjectOfType<Player>();
        Vector3 from = player != null ? player.transform.position : transform.position;
        Vector3 dir = spawn - from; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = emitter.forward;
        dir.Normalize();

        spawn = OnGround(spawn);
        GameObject go = Instantiate(figurePrefab, spawn, Quaternion.LookRotation(dir, Vector3.up));
        figure = go.transform;
        figureAlpha = 1f;
        StartCoroutine(Co_FigureWalk(go, dir));
    }

    IEnumerator Co_FigureWalk(GameObject go, Vector3 dir)
    {
        QuestionerPuppet puppet = go.GetComponent<QuestionerPuppet>();
        Player player = FindObjectOfType<Player>();

        Vector3 pos = go.transform.position;
        Vector3 lastPlayerPos = player != null ? player.transform.position : Vector3.zero;
        float playerSpeed = 0f;                            // the player's real speed, measured from how far they actually move
        float walked = 0f, hiddenFor = 0f, gaze = 0f;
        float speed = figureWalkSpeed, speedVelocity = 0f;
        bool seen = false;                                 // vanishing because the player stared at him (fades) rather than out of sight (just gone)
        figureSpeed = speed;

        while (go != null)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) { yield return null; continue; }

            float dist = 0f;
            if (player != null)
            {
                Vector3 pp = player.transform.position;
                Vector3 moved = pp - lastPlayerPos; moved.y = 0f;
                float instant = moved.magnitude / dt;
                if (instant > 30f) instant = playerSpeed;                                // a teleport, not running
                playerSpeed = Mathf.Lerp(playerSpeed, instant, 1f - Mathf.Exp(-dt / 0.15f));
                lastPlayerPos = pp;

                Vector3 toPlayer = pp - go.transform.position; toPlayer.y = 0f;
                dist = toPlayer.magnitude;
            }

            // Keep a fixed gap: match the player's own speed so they can never close in, and go faster the closer than the gap they get.
            float target = figureWalkSpeed;
            if (player != null && dist < figureReactDistance)
                target = Mathf.Clamp(playerSpeed + figureSpeedGain * (figureKeepDistance - dist), figureWalkSpeed, figureMaxSpeed);
            speed = Mathf.SmoothDamp(speed, target, ref speedVelocity, figureSpeedSmoothTime);
            figureSpeed = speed;

            if (puppet != null)
            {
                puppet.Walk(speed);                                       // picks the slow-walk / walk blend
                puppet.SetAnimationSpeed(Mathf.Max(1f, speed / 2f));      // the walk clip tops out at ~2 m/s: play it faster beyond that
            }

            float step = speed * dt;
            pos += dir * step;
            walked += step;
            go.transform.position = OnGround(pos);

            // He vanishes when the player has chased him and kept looking at him for a few seconds.
            bool chasing = playerSpeed >= figureChaseMinPlayerSpeed;
            if (player != null && chasing && dist <= figureLookDistance && PlayerIsLookingAt(go.transform, dist))
                gaze += dt;
            else
                gaze = Mathf.Max(0f, gaze - dt * 0.5f);
            if (gaze >= figureVanishAfterLookingSeconds) { seen = true; break; }

            // Only if the player never looks at him does he end up vanishing for good at the end of his path, out of sight.
            if (walked >= figureWalkDistance)
            {
                hiddenFor = CanPlayerSee(go.transform, dist) ? 0f : hiddenFor + dt;
                if (hiddenFor >= figureHiddenSeconds) break;
            }
            if (walked > figureWalkDistance + 60f) break;                 // safety net so he can never walk forever

            yield return null;
        }

        if (seen && puppet != null && figureFadeOutSeconds > 0.01f && puppet.BeginFade())
        {
            // The player saw him: he fades away while still walking, and the footsteps fade with him.
            for (float k = 0f; k < figureFadeOutSeconds && go != null; k += Time.deltaTime)
            {
                figureAlpha = 1f - Mathf.SmoothStep(0f, 1f, k / figureFadeOutSeconds);
                puppet.SetFadeAlpha(figureAlpha);
                pos += dir * speed * Time.deltaTime;
                go.transform.position = OnGround(pos);
                yield return null;
            }
            figureAlpha = 0f;
        }

        if (go != null) Destroy(go);
        figure = null;
        if (destroyTriggerAfterPlaying && playCR == null) Destroy(gameObject);
    }

    // Is the player actually looking at him? He has to be visible AND roughly in the middle of the screen.
    bool PlayerIsLookingAt(Transform who, float distance)
    {
        if (!CanPlayerSee(who, distance)) return false;
        Camera cam = Camera.main;
        if (cam == null) return true;
        Vector3 vp = cam.WorldToViewportPoint(who.position + Vector3.up * 1.3f);
        return Mathf.Abs(vp.x - 0.5f) < 0.38f && Mathf.Abs(vp.y - 0.5f) < 0.45f;
    }

    // Can the player see him right now? Needs to be inside the fog's sight range, on screen, and not behind a wall or prop.
    bool CanPlayerSee(Transform who, float distance)
    {
        if (distance > figureHiddenByFogDistance) return false;
        Camera cam = Camera.main;
        if (cam == null) return true;

        Vector3 chest = who.position + Vector3.up * 1.3f;
        Vector3 vp = cam.WorldToViewportPoint(chest);
        if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return false;

        Vector3 from = cam.transform.position;
        for (int i = 0; i < 4; i++)                                       // skip through the player's own colliders
        {
            Vector3 d = chest - from;
            float len = d.magnitude;
            if (len < 0.1f) return true;
            if (!Physics.Raycast(from, d / len, out RaycastHit hit, len - 0.2f, ~0, QueryTriggerInteraction.Ignore)) return true;
            if (hit.collider.GetComponentInParent<Player>() == null) return false;     // something solid is in the way
            from = hit.point + d / len * 0.1f;
        }
        return true;
    }

    // Puts a point on the floor beneath it (keeps whatever height it had if there is no floor to find)
    static Vector3 OnGround(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            p.y = hit.point.y;
        return p;
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.25f, 0.9f, 0.18f);
            var box = col as BoxCollider;
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(1f, 0.25f, 0.9f, 0.9f);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        Transform e = emitter != null ? emitter : transform;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(e.position, 0.35f);
        Gizmos.DrawLine(transform.position, e.position);
        if (walkTo != null)
        {
            Gizmos.DrawLine(e.position, walkTo.position);
            Gizmos.DrawWireSphere(walkTo.position, 0.2f);
        }
        if (figurePrefab != null)
        {
            Vector3 d = e.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
            {
                d.Normalize();
                Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
                Gizmos.DrawLine(e.position, e.position + d * figureWalkDistance);
                Gizmos.DrawWireSphere(e.position + d * figureWalkDistance, 0.3f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform e = emitter != null ? emitter : transform;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(e.position, minDistance);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(e.position, maxDistance);
    }
}
