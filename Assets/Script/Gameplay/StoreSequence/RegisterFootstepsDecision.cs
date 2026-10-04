using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

/// <summary>
/// Phase 7 - the cash register decision.
/// While the player is typing the code, footsteps start to approach from behind. Their choice:
///  - keep typing (finish faster, accept the risk): the steps keep coming closer;
///  - or leave the register and turn around: the steps stop dead and nothing is there. When they come back to the
///    register the steps carry on from where they left off.
/// The steps are never endless: after a fixed number of them (or if the player just stands there and waits), they come
/// closer for a few more steps, stop just outside the player's view and stay silent. A few seconds later something small
/// shifts or falls somewhere else in the store, and the event is over - the register works normally again.
/// The register menu pauses the game (timeScale 0), so everything here runs on unscaled time.
/// </summary>
public class RegisterFootstepsDecision : MonoBehaviour
{
    [Header("Hooks")]
    [SerializeField] CashRegisterInteractable register;
    [SerializeField] Fusebox fusebox;
    [Tooltip("Provides the 'something falls somewhere else' at the end.")]
    [SerializeField] FuseHuntDirector director;

    [Header("Start")]
    [Tooltip("Seconds after the player interacts with the register before the first step is heard (the screen is still fading to black).")]
    [SerializeField] float startAfterMenuSeconds = 0.4f;

    [Header("Footsteps")]
    [SerializeField] AudioClip[] stepClips;
    [Tooltip("How many steps in total, ever. After this the event always ends.")]
    [SerializeField] int totalSteps = 14;
    [SerializeField] Vector2 stepInterval = new Vector2(0.7f, 0.95f);
    [SerializeField] float startDistance = 6.5f;
    [Tooltip("Where the steps stop, behind the player.")]
    [SerializeField] float endDistance = 2.3f;
    [SerializeField, Range(0f, 1f)] float volume = 0.8f;
    [SerializeField] Vector2 pitch = new Vector2(0.82f, 0.9f);
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Interruptions")]
    [Tooltip("When the player returns to the register, the steps resume after this long.")]
    [SerializeField] float resumeDelay = 2f;
    [Tooltip("Footsteps are only ever heard while the player is at the register. Turn this on to let them finish on their own if the player walks away and waits (they would then be heard away from the register).")]
    [SerializeField] bool finishWhileAwayFromRegister = false;
    [Tooltip("Only used when 'Finish While Away From Register' is on.")]
    [SerializeField] float waitBeforeEnding = 7f;

    [Header("Ending")]
    [Tooltip("At most this many more steps once the ending starts.")]
    [SerializeField] int endingMaxSteps = 5;
    [SerializeField] Vector2 silenceBeforeObject = new Vector2(4f, 6f);

    [Header("Events")]
    public UnityEvent onStarted;
    public UnityEvent onPaused;
    public UnityEvent onResumed;
    public UnityEvent onEnded;

    public enum State { Idle, Approaching, Paused, Ending, Done }
    public State Current { get; private set; }
    public int StepsTaken { get; private set; }

    float menuTime, stepTimer, pauseTimer;
    float sideAngle;
    bool codeDone;
    Camera cam;
    Transform playerT;
    AudioSource[] pool;
    int poolIdx;
    Coroutine endingCR;

    void Awake()
    {
        pool = new AudioSource[4];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("RegisterStep " + i);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1.5f; a.maxDistance = 16f; a.dopplerLevel = 0f; a.outputAudioMixerGroup = sfxGroup;
            a.ignoreListenerPause = true;
            pool[i] = a;
        }
    }

    void Start()
    {
        if (register != null) register.OnCashRegisterOpenedSuccessfully.AddListener(OnCodeAccepted);
    }

    void OnDestroy()
    {
        if (register != null) register.OnCashRegisterOpenedSuccessfully.RemoveListener(OnCodeAccepted);
    }

    void OnCodeAccepted()
    {
        codeDone = true;
        if (Current == State.Approaching || Current == State.Paused || Current == State.Ending)
        {
            // the player was faster than the steps
            if (endingCR != null) StopCoroutine(endingCR);
            Finish("code entered before the steps arrived");
        }
    }

    // ---------------------------------------------------------------- loop (unscaled time)

    void Update()
    {
        if (Current == State.Done || register == null || codeDone) return;
        float dt = Time.unscaledDeltaTime;
        bool menuOpen = register.CashRegisterMenuIsActive();

        switch (Current)
        {
            case State.Idle:
                if (menuOpen)
                {
                    menuTime += dt;
                    if (menuTime >= startAfterMenuSeconds && fusebox != null && fusebox.hasEnergy && DreadEvents.TryAcquireIgnoringGap()) Begin();
                }
                else menuTime = 0f;
                break;

            case State.Approaching:
                if (!menuOpen) { Current = State.Paused; pauseTimer = 0f; Debug.Log("[RegisterFootsteps] player left the register - the steps stop"); onPaused?.Invoke(); break; }
                stepTimer -= dt;
                if (stepTimer <= 0f) TakeStep();
                break;

            case State.Paused:
                if (menuOpen)
                {
                    Current = State.Approaching;
                    stepTimer = resumeDelay;
                    Debug.Log("[RegisterFootsteps] back at the register - the steps will carry on");
                    onResumed?.Invoke();
                }
                else if (finishWhileAwayFromRegister)
                {
                    pauseTimer += dt;
                    if (pauseTimer >= waitBeforeEnding) StartEnding("the player waited");
                }
                break;
        }
    }

    void Begin()
    {
        Current = State.Approaching;
        StepsTaken = 0;
        stepTimer = 0f;   // first step right away
        sideAngle = Random.Range(-25f, 25f);
        Debug.Log("[RegisterFootsteps] footsteps begin behind the player");
        onStarted?.Invoke();
    }

    void TakeStep()
    {
        float t = (StepsTaken + 1f) / Mathf.Max(1, totalSteps);
        PlayStep(Mathf.Lerp(startDistance, endDistance, t));
        StepsTaken++;
        stepTimer = Random.Range(stepInterval.x, stepInterval.y);
        if (StepsTaken >= totalSteps) StartEnding("all steps taken");
    }

    // ---------------------------------------------------------------- ending

    void StartEnding(string why)
    {
        if (Current == State.Ending || Current == State.Done) return;
        Current = State.Ending;
        Debug.Log("[RegisterFootsteps] ending: " + why);
        endingCR = StartCoroutine(Co_Ending());
    }

    IEnumerator Co_Ending()
    {
        // a few more steps, then they stop just outside the player's view
        int remaining = Mathf.Max(0, totalSteps - StepsTaken);
        int n = Mathf.Min(endingMaxSteps, remaining);
        float fromDist = Mathf.Lerp(startDistance, endDistance, Mathf.Clamp01(StepsTaken / (float)Mathf.Max(1, totalSteps)));
        for (int i = 1; i <= n; i++)
        {
            // steps are only heard at the register: if the player left, hold the next step until they are back
            while (!finishWhileAwayFromRegister && register != null && !register.CashRegisterMenuIsActive()) yield return null;
            PlayStep(Mathf.Lerp(fromDist, endDistance, i / (float)n));
            StepsTaken++;
            yield return new WaitForSecondsRealtime(Random.Range(stepInterval.x, stepInterval.y));
        }
        Debug.Log("[RegisterFootsteps] the steps stopped just out of view - silence");

        // silence, then something small elsewhere
        yield return new WaitForSecondsRealtime(Random.Range(silenceBeforeObject.x, silenceBeforeObject.y));
        if (director != null) yield return director.ItemFallRoutine();
        else yield return new WaitForSecondsRealtime(1f);
        Finish("something fell elsewhere");
    }

    void Finish(string why)
    {
        if (Current == State.Done) return;
        Current = State.Done;
        DreadEvents.Release(10f);
        Debug.Log("[RegisterFootsteps] event over (" + why + ")");
        onEnded?.Invoke();
    }

    // ---------------------------------------------------------------- geometry + audio

    void PlayStep(float distance)
    {
        if (stepClips == null || stepClips.Length == 0) return;
        if (cam == null) cam = Camera.main;
        if (playerT == null) { var p = FindObjectOfType<Player>(); if (p != null) playerT = p.transform; }
        if (playerT == null) return;

        Vector3 fwd = cam != null ? cam.transform.forward : playerT.forward;
        fwd.y = 0f; fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;

        // always behind the player (so never inside their view), drifting a little as it walks
        sideAngle += Random.Range(-4f, 4f);
        sideAngle = Mathf.Clamp(sideAngle, -35f, 35f);
        Vector3 dir = Quaternion.Euler(0f, 180f + sideAngle, 0f) * fwd;
        Vector3 pos = playerT.position + dir * distance;
        pos.y = playerT.position.y + 0.05f;

        var a = pool[poolIdx]; poolIdx = (poolIdx + 1) % pool.Length;
        a.transform.position = pos;
        a.pitch = Random.Range(pitch.x, pitch.y);
        a.volume = volume * Random.Range(0.85f, 1f);
        a.clip = stepClips[Random.Range(0, stepClips.Length)];
        a.Play();
    }

    [ContextMenu("Test: reset")]
    void ResetEvent() { Current = State.Idle; StepsTaken = 0; menuTime = 0f; codeDone = false; }
}
