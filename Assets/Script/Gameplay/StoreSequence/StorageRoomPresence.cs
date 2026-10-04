using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

/// <summary>
/// Phase 5 - the storage room slowly shows that something is in it, without ever being seen.
///   (0) the shelving collapse during the blackout (StoreBlackoutSequence / StorageCollapse)
///   1. a box is dragged across the floor
///   2. something knocks softly against the metal shelving
///   3. several seconds of breathing right behind the door
/// Every step is a single isolated sound. A step needs BOTH progression (the previous step, plus a story milestone or a
/// fallback time) AND proximity (the player walks into range of the storage door), and the player has to actually move:
/// standing still next to the door never triggers anything. Events use the shared DreadEvents lock, so they never overlap
/// the fuse-hunt events.
/// </summary>
public class StorageRoomPresence : MonoBehaviour
{
    [Header("Progression hooks")]
    [SerializeField] StoreBlackoutSequence blackout;
    [SerializeField] Fusebox fusebox;
    [SerializeField] CashRegisterInteractable register;
    [Tooltip("The door that leads to the storage room (locked until later). Trying it also triggers the breathing.")]
    [SerializeField] DoorInteractable storageDoor;

    [Header("Where the sounds come from")]
    [Tooltip("Just behind the storage door, on the room's side.")]
    [SerializeField] Transform behindDoor;
    [Tooltip("Offset of the knocks from 'behind door' (so they come from the shelves, not the door itself).")]
    [SerializeField] Vector3 knockOffset = new Vector3(1.2f, 0f, -0.8f);

    [Header("Step 1 - a box is dragged")]
    [SerializeField] AudioClip[] dragClips;
    [Tooltip("Player must come within this distance of the door (metres).")]
    [SerializeField] float dragRadius = 13f;
    [SerializeField] float dragWalkNeeded = 6f;
    [Tooltip("A spilled box that really slides across the storage floor (optional), and what it slides towards.")]
    [SerializeField] Transform dragBox;
    [SerializeField] Transform dragTowards;
    [SerializeField] float dragDistance = 1.4f;

    [Header("Step 2 - a soft knock against the shelving")]
    [SerializeField] AudioClip[] knockClips;
    [SerializeField] float knockRadius = 9f;
    [SerializeField] float knockWalkNeeded = 6f;
    [SerializeField, Range(0f, 1f)] float knockVolume = 0.6f;

    [Header("Step 3 - breathing behind the door")]
    [SerializeField] AudioClip breathingClip;
    [SerializeField] float breathRadius = 3.6f;
    [SerializeField, Range(0f, 1f)] float breathVolume = 0.9f;

    [Header("Pacing")]
    [Tooltip("Minimum quiet time between two steps.")]
    [SerializeField] float gapBetweenSteps = 25f;
    [Tooltip("Step 1 cannot start sooner than this after the crash (the crash is still ringing).")]
    [SerializeField] float afterCrashSeconds = 10f;
    [Tooltip("Step 2 needs the power to be back or this long after step 1, whichever comes first.")]
    [SerializeField] float knockFallbackSeconds = 75f;
    [Tooltip("Step 3 needs the cash register to have been used or this long after step 2.")]
    [SerializeField] float breathFallbackSeconds = 60f;
    [Tooltip("Nothing starts for this long after the fuse goes in (the power-restore scene).")]
    [SerializeField] float quietAfterPowerRestore = 10f;
    [SerializeField] float movingSpeed = 0.8f;

    [Header("Audio")]
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Events")]
    public UnityEvent onStepStarted;
    public UnityEvent onFinished;

    /// <summary>0 = nothing yet, 1 = drag done, 2 = knock done, 3 = breathing done.</summary>
    public int Step { get; private set; }

    bool crashed, powerBack, registerUsed;
    float crashAt, stepDoneAt, unlockedAt;
    float walked;
    float moveSpeed;
    Vector3 lastPos;
    bool haveLast;
    float approachSample, approachDist;
    bool approaching;
    bool busy;
    Transform playerT;
    AudioSource src;
    AudioLowPassFilter lowPass;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.dopplerLevel = 0f;
        src.outputAudioMixerGroup = sfxGroup;
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
    }

    void Start()
    {
        if (blackout != null) blackout.onCrash.AddListener(OnCrash);
        if (fusebox != null) fusebox.onEnergyRestored.AddListener(OnPowerBack);
        if (register != null) register.OnCashRegisterOpenedSuccessfully.AddListener(OnRegisterUsed);
        if (storageDoor != null) storageDoor.OnInteractionFailed.AddListener(OnDoorTried);
    }

    void OnDestroy()
    {
        if (blackout != null) blackout.onCrash.RemoveListener(OnCrash);
        if (fusebox != null) fusebox.onEnergyRestored.RemoveListener(OnPowerBack);
        if (register != null) register.OnCashRegisterOpenedSuccessfully.RemoveListener(OnRegisterUsed);
        if (storageDoor != null) storageDoor.OnInteractionFailed.RemoveListener(OnDoorTried);
    }

    void OnCrash() { crashed = true; crashAt = Time.time; Debug.Log("[StoragePresence] the shelf has fallen - the room is listening"); }
    void OnPowerBack() { powerBack = true; DreadEvents.HoldFor(quietAfterPowerRestore); }
    void OnRegisterUsed() { registerUsed = true; }
    void OnDoorTried()
    {
        // trying the locked door when the breathing is due
        if (!busy && Step == 2 && StepUnlocked(3) && DreadEvents.TryAcquire()) StartCoroutine(Co_Breathing());
    }

    // ---------------------------------------------------------------- state

    bool StepUnlocked(int step)
    {
        if (!crashed) return false;
        switch (step)
        {
            case 1: return Time.time >= crashAt + afterCrashSeconds;
            case 2: return Step >= 1 && Time.time >= stepDoneAt + gapBetweenSteps && (powerBack || Time.time >= stepDoneAt + knockFallbackSeconds);
            case 3: return Step >= 2 && Time.time >= stepDoneAt + gapBetweenSteps && (registerUsed || Time.time >= stepDoneAt + breathFallbackSeconds);
        }
        return false;
    }

    Vector3 DoorPos { get { return behindDoor != null ? behindDoor.position : transform.position; } }

    void Update()
    {
        if (Step >= 3 || busy) return;
        if (playerT == null) { var p = FindObjectOfType<Player>(); if (p != null) playerT = p.transform; else return; }

        // how the player is moving
        Vector3 pp = playerT.position; pp.y = 0f;
        if (haveLast)
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float step = (pp - lastPos).magnitude;
            moveSpeed = Mathf.Lerp(moveSpeed, step / dt, 0.15f);
            if (moveSpeed > movingSpeed) walked += step;
        }
        lastPos = pp; haveLast = true;

        Vector3 dp = DoorPos; dp.y = 0f;
        float d = Vector3.Distance(pp, dp);
        approachSample += Time.deltaTime;
        if (approachSample >= 0.6f) { approaching = d < approachDist - 0.35f; approachDist = d; approachSample = 0f; }

        int next = Step + 1;
        if (!StepUnlocked(next)) { walked = 0f; return; }   // walking only counts once the step is unlocked

        if (GameManager.IsPaused || Time.timeScale <= 0f) return;
        var pl = Player.instance;
        if (pl != null && pl.pauseMovement) return;
        bool moving = moveSpeed > movingSpeed;
        if (!moving) return;

        bool go = false;
        switch (next)
        {
            case 1: go = d < dragRadius && walked >= dragWalkNeeded; break;
            case 2: go = d < knockRadius && walked >= knockWalkNeeded; break;
            case 3: go = d < breathRadius && approaching; break;
        }
        if (!go || !DreadEvents.TryAcquire()) return;

        if (next == 1) StartCoroutine(Co_Drag());
        else if (next == 2) StartCoroutine(Co_Knock());
        else StartCoroutine(Co_Breathing());
    }

    void FinishStep(int step, float gap)
    {
        Step = step;
        stepDoneAt = Time.time;
        walked = 0f;
        busy = false;
        DreadEvents.Release(gap);
        onStepStarted?.Invoke();
        if (step >= 3) onFinished?.Invoke();
        Debug.Log("[StoragePresence] step " + step + " done");
    }

    // ---------------------------------------------------------------- the three sounds

    IEnumerator Co_Drag()
    {
        busy = true;
        Debug.Log("[StoragePresence] a box is dragged");
        var clip = Pick(dragClips);
        float len = clip != null ? clip.length : 3.5f;
        Play(clip, DoorPos, 0.9f, 2f, 16f, 2600f);
        if (dragBox != null) StartCoroutine(Co_SlideBox(len));
        yield return new WaitForSeconds(len);
        FinishStep(1, 6f);
    }

    IEnumerator Co_SlideBox(float length)
    {
        Vector3 from = dragBox.position;
        Vector3 dir = dragTowards != null ? dragTowards.position - from : Vector3.back;
        dir.y = 0f; dir = dir.sqrMagnitude < 0.01f ? Vector3.back : dir.normalized;
        Vector3 to = from + dir * dragDistance;
        const int steps = 3;
        float st = length / (steps * 1.7f);
        for (int s = 0; s < steps; s++)
        {
            Vector3 a = Vector3.Lerp(from, to, s / (float)steps), b = Vector3.Lerp(from, to, (s + 1) / (float)steps);
            float t = 0f;
            while (t < st) { t += Time.deltaTime; dragBox.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / st)); yield return null; }
            yield return new WaitForSeconds(st * 0.7f);
        }
        dragBox.position = to;
    }

    IEnumerator Co_Knock()
    {
        busy = true;
        Debug.Log("[StoragePresence] something knocks against the shelving");
        float t = 0f;
        float[] at = { 0f, Random.Range(1.0f, 1.6f), Random.Range(2.7f, 3.5f) };
        for (int i = 0; i < at.Length; i++)
        {
            while (t < at[i]) { t += Time.deltaTime; yield return null; }
            Play(Pick(knockClips), DoorPos + knockOffset, knockVolume * Random.Range(0.8f, 1f), 2f, 14f, 2200f);
        }
        yield return new WaitForSeconds(1.2f);
        FinishStep(2, 6f);
    }

    IEnumerator Co_Breathing()
    {
        busy = true;
        Debug.Log("[StoragePresence] breathing behind the door");
        float len = breathingClip != null ? breathingClip.length : 8f;
        Play(breathingClip, DoorPos, breathVolume, 1f, 7f, 1900f);
        yield return new WaitForSeconds(len);
        FinishStep(3, 8f);
    }

    // ---------------------------------------------------------------- audio

    static AudioClip Pick(AudioClip[] arr) { return arr != null && arr.Length > 0 ? arr[Random.Range(0, arr.Length)] : null; }

    void Play(AudioClip clip, Vector3 pos, float volume, float minDist, float maxDist, float lowPassHz)
    {
        if (clip == null) return;
        // one source per call so overlapping knocks do not cut each other off
        var go = new GameObject("StorageSfx");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip; a.volume = volume; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = minDist; a.maxDistance = maxDist; a.dopplerLevel = 0f; a.outputAudioMixerGroup = sfxGroup;
        var lp = go.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = lowPassHz;
        a.Play();
        Destroy(go, clip.length + 0.5f);
    }

    [ContextMenu("Test: force next step")]
    void ForceNext()
    {
        crashed = true; walked = 999f;
        if (Step == 0) { crashAt = -999f; StartCoroutine(Co_Drag()); }
        else if (Step == 1) StartCoroutine(Co_Knock());
        else if (Step == 2) StartCoroutine(Co_Breathing());
    }
}
