using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.Localization;

/// <summary>
/// Phase 2 of the store level - footsteps outside. Starts when StoreSequenceManager says the sequence begins.
///  1. The Runner stands across the road watching the store (visible only if the player looks out); faint shuffling steps start.
///  2. He walks to the entrance, the footsteps getting closer. Nothing forces the player to look.
///  3. He stands outside the glass door, staring in, then the lock engages (a click that can be heard anywhere in the store).
///  4. The door stays locked: trying it plays the locked sound and Khalid reacts.
///  5. He runs off along the street into the fog. A few seconds later fast running footsteps are heard somewhere else
///     outside the building and he is never seen again.
/// </summary>
public class StoreFootstepsSequence : MonoBehaviour
{
    public enum FuseTrigger { None, FuseRevealed, FusePickedUp }

    [Header("Trigger")]
    [Tooltip("Start when the store sequence (phase 1) starts.")]
    [SerializeField] bool startOnSequenceStart = false;
    [Tooltip("Start when the player finds the fuse: when it appears in the last cabinet, or when they pick it up.")]
    [SerializeField] FuseTrigger startOnFuse = FuseTrigger.FusePickedUp;

    [Header("Actor")]
    [SerializeField] RunnerPuppet actor;

    [Header("Route (all points are snapped to the ground)")]
    [Tooltip("Across the road: where he stands watching the store. Rotate it to face the store.")]
    [SerializeField] Transform watchPoint;
    [SerializeField] Transform[] approachPoints;
    [Tooltip("Directly outside the glass door; rotate it to face the door.")]
    [SerializeField] Transform doorStandPoint;
    [SerializeField] Transform[] runAwayPoints;
    [Tooltip("Where the lock click is heard from.")]
    [SerializeField] Transform lockSoundPoint;
    [Tooltip("Rapid running footsteps heard later somewhere else outside; nobody ever sees him here.")]
    [SerializeField] Transform[] farRunPoints;

    [Header("Entrance door")]
    [SerializeField] DoorInteractable entranceDoor;
    [SerializeField] AudioClip lockClip;
    [Tooltip("Subtitle on the first attempt to open the locked door.")]
    public LocalizedString lockedAttemptLine;

    [Header("Footsteps")]
    [SerializeField] AudioClip[] stepClips;
    [Tooltip("Outside footsteps use their own 3D sources with a gentle falloff so they carry through the glass.")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] float hearMinDistance = 4f;
    [SerializeField] float hearMaxDistance = 45f;
    [SerializeField] float walkStepLength = 0.8f;
    [SerializeField] float runStepLength = 1.9f;
    [Range(0f, 1f)] [SerializeField] float shuffleVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] float walkVolume = 1f;
    [Range(0f, 1f)] [SerializeField] float runVolume = 1f;
    [SerializeField] Vector2 walkPitch = new Vector2(0.88f, 0.98f);
    [SerializeField] Vector2 runPitch = new Vector2(1.08f, 1.2f);

    [Header("Timing (seconds)")]
    [SerializeField] float startDelay = 1f;
    [Tooltip("How long he stands across the road before heading for the door.")]
    [SerializeField] float watchSeconds = 5f;
    [Tooltip("When started by the fuse the delays are shorter, so the player does not wait around.")]
    [SerializeField] float fuseStartDelay = 0.3f;
    [SerializeField] float fuseWatchSeconds = 3f;
    [SerializeField] float walkSpeed = 1.6f;
    [Tooltip("How long he stares through the glass before the lock engages.")]
    [SerializeField] float stareSeconds = 4f;
    [Tooltip("Pause after the click before he runs, so the player can try the door.")]
    [SerializeField] float afterLockSeconds = 2.5f;
    [SerializeField] float runSpeed = 7.5f;
    [SerializeField] float farFootstepsDelay = 4.5f;
    [SerializeField] float farRunSpeed = 7f;
    [SerializeField] float farStepInterval = 0.26f;

    [Header("Events")]
    public UnityEvent onFootstepsBegin;
    public UnityEvent onActorAtDoor;
    public UnityEvent onDoorLocked;
    public UnityEvent onActorGone;
    public UnityEvent onSequenceFinished;

    public bool Started { get; private set; }
    public bool Finished { get; private set; }
    public bool EntranceLocked { get; private set; }

    bool lockedLineShown;
    bool fastStart;
    Coroutine runCR;
    AudioSource[] pool;
    int poolIndex;

    void Awake()
    {
        pool = new AudioSource[8];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("FootstepEmitter " + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = hearMinDistance;
            src.maxDistance = hearMaxDistance;
            src.dopplerLevel = 0f;
            src.spread = 0f;
            src.outputAudioMixerGroup = sfxGroup;
            pool[i] = src;
        }
    }

    void Start()
    {
        if (actor != null) actor.gameObject.SetActive(false);

        if (startOnSequenceStart && StoreSequenceManager.instance != null)
            StoreSequenceManager.instance.OnSequenceStart.AddListener(Begin);

        if (FuseHuntManager.instance != null)
        {
            if (startOnFuse == FuseTrigger.FuseRevealed) FuseHuntManager.instance.onFuseRevealed.AddListener(BeginFromFuse);
            else if (startOnFuse == FuseTrigger.FusePickedUp) FuseHuntManager.instance.onFusePickedUp.AddListener(BeginFromFuse);
        }
    }

    public void BeginFromFuse()
    {
        if (Started) return;
        fastStart = true;
        Debug.Log("[Footsteps] triggered by the fuse");
        Begin();
    }

    [ContextMenu("Start footsteps sequence now")]
    public void Begin()
    {
        if (Started) return;
        Started = true;
        Debug.Log("[Footsteps] phase 2 starts");
        runCR = StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        float delay = fastStart ? fuseStartDelay : startDelay;
        float watch = fastStart ? fuseWatchSeconds : watchSeconds;
        yield return new WaitForSeconds(delay);

        // 1. the Runner stands across the road
        if (actor != null && watchPoint != null)
        {
            actor.transform.position = Ground(watchPoint.position);
            actor.FaceInstant(watchPoint.forward);
            actor.Idle();
            actor.gameObject.SetActive(true);
        }
        onFootstepsBegin?.Invoke();
        Debug.Log("[Footsteps] he is standing across the road, faint shuffling begins");

        float t = 0f, next = 0.6f;
        while (t < watch)
        {
            t += Time.deltaTime;
            if (t >= next)
            {
                PlayStep(actor != null ? actor.transform.position : watchPoint.position, shuffleVolume, walkPitch);
                next = t + Random.Range(1.6f, 2.8f);
            }
            yield return null;
        }

        // 2. he walks to the door, footsteps getting closer
        if (actor != null) actor.Walk();
        var route = new System.Collections.Generic.List<Transform>();
        if (approachPoints != null) route.AddRange(approachPoints);
        if (doorStandPoint != null) route.Add(doorStandPoint);
        Debug.Log("[Footsteps] approaching the entrance");
        yield return Co_MoveAlong(route.ToArray(), walkSpeed, walkStepLength, walkVolume, walkPitch);

        // 3. he stares through the glass
        if (actor != null)
        {
            actor.Idle();
            if (doorStandPoint != null) actor.FaceInstant(doorStandPoint.forward);
        }
        onActorAtDoor?.Invoke();
        Debug.Log("[Footsteps] at the door, staring in");
        yield return new WaitForSeconds(stareSeconds);

        // ... and the lock engages
        LockEntrance();
        yield return new WaitForSeconds(afterLockSeconds);

        // 4. he runs off into the fog
        if (actor != null) actor.Run();
        Debug.Log("[Footsteps] running away");
        yield return Co_MoveAlong(runAwayPoints, runSpeed, runStepLength, runVolume, runPitch);
        if (actor != null) actor.gameObject.SetActive(false);
        onActorGone?.Invoke();

        // 5. later: rapid running footsteps somewhere else outside
        yield return new WaitForSeconds(farFootstepsDelay);
        Debug.Log("[Footsteps] distant running footsteps");
        yield return Co_FarFootsteps();

        Finished = true;
        Debug.Log("[Footsteps] phase 2 finished");
        onSequenceFinished?.Invoke();
    }

    /// <summary>Skips the rest of the scene (the player is not watching it): the Runner disappears and the door locks right now.</summary>
    public void AbortAndLock()
    {
        if (Finished) return;
        Started = true;
        if (runCR != null) StopCoroutine(runCR);
        runCR = null;
        if (actor != null) actor.gameObject.SetActive(false);
        Debug.Log("[Footsteps] scene skipped - the player is heading for the fuse box");
        LockEntrance();
        Finished = true;
        onSequenceFinished?.Invoke();
    }

    public void LockEntrance()
    {
        if (EntranceLocked) return;
        EntranceLocked = true;

        if (entranceDoor != null)
        {
            entranceDoor.hasDoorLock = true;
            entranceDoor.OnInteractionFailed.AddListener(OnLockedDoorTried);
        }

        PlayLockClick();
        Debug.Log("[Footsteps] entrance door is now locked");
        onDoorLocked?.Invoke();
    }

    void OnLockedDoorTried()
    {
        if (lockedLineShown) return;
        lockedLineShown = true;
        if (lockedAttemptLine != null && !lockedAttemptLine.IsEmpty)
            SubtitleManager.ShowSubtitle(lockedAttemptLine, false);
    }

    // the click must be audible from anywhere in the store, so add a 2D layer that fades in with distance
    void PlayLockClick()
    {
        if (lockClip == null) return;
        Vector3 pos = lockSoundPoint != null ? lockSoundPoint.position : (entranceDoor != null ? entranceDoor.transform.position : transform.position);
        PlayWorld(lockClip, pos, 1f, 1f);

        if (Player.instance != null)
        {
            float d = Vector3.Distance(Player.instance.transform.position, pos);
            float far = Mathf.Clamp01((d - 3f) / 10f);
            if (far > 0.01f && SoundEffectManager.instance != null)
                SoundEffectManager.instance.PlaySFX(lockClip, 0.9f * far);
        }
    }

    IEnumerator Co_MoveAlong(Transform[] points, float speed, float stepLength, float volume, Vector2 pitch)
    {
        if (actor == null || points == null) yield break;
        float sinceStep = 0f;
        foreach (var p in points)
        {
            if (p == null) continue;
            Vector3 target = p.position;
            while (true)
            {
                Vector3 pos = actor.transform.position;
                Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                float dist = flat.magnitude;
                if (dist < 0.1f) break;

                float step = Mathf.Min(dist, speed * Time.deltaTime);
                Vector3 next = pos + flat.normalized * step;
                actor.transform.position = Ground(next);
                actor.FaceDirection(flat, Time.deltaTime);

                sinceStep += step;
                if (sinceStep >= stepLength)
                {
                    sinceStep = 0f;
                    PlayStep(actor.transform.position, volume, pitch);
                }
                yield return null;
            }
        }
    }

    IEnumerator Co_FarFootsteps()
    {
        if (farRunPoints == null || farRunPoints.Length == 0) yield break;
        Vector3 pos = farRunPoints[0].position;
        float timer = 0f;
        for (int i = 1; i < farRunPoints.Length; i++)
        {
            Vector3 target = farRunPoints[i].position;
            while (Vector3.Distance(pos, target) > 0.2f)
            {
                pos = Vector3.MoveTowards(pos, target, farRunSpeed * Time.deltaTime);
                timer += Time.deltaTime;
                if (timer >= farStepInterval)
                {
                    timer = 0f;
                    PlayStep(pos, runVolume, runPitch);
                }
                yield return null;
            }
        }
    }

    void PlayStep(Vector3 position, float volume, Vector2 pitch)
    {
        if (stepClips == null || stepClips.Length == 0) return;
        PlayWorld(stepClips[Random.Range(0, stepClips.Length)], position, volume, Random.Range(pitch.x, pitch.y));
    }

    void PlayWorld(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        if (clip == null || pool == null) return;
        var src = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;
        src.transform.position = position;
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.Play();
    }

    static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            p.y = hit.point.y;
        return p;
    }
}
