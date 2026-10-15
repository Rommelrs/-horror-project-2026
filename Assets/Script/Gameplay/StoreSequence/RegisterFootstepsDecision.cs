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
/// Runner cameo: the moment the steps begin the Runner is standing in the player's view (the menu hides him). When the player
/// leaves the register he is there for a split second, then gone instantly: nothing is there when they look again.
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

    [Header("Runner cameo")]
    [SerializeField] bool runnerCameo = true;
    [Tooltip("The Runner stand-in (the same one the footsteps-outside scene uses). Found automatically if left empty.")]
    [SerializeField] RunnerPuppet runner;
    [Tooltip("How far in front of the player's camera he stands (metres). Of all the spots with a clear view and room to stand, the most central one at about the middle distance is used.")]
    [SerializeField] Vector2 cameoDistance = new Vector2(3f, 9f);
    [Tooltip("He appears this many seconds after the register was opened, measured from the moment the player interacted. The register screen fades to black for about 1.25 s first, and he must not show up while the world is still visible.")]
    [SerializeField, Min(0f)] float cameoDelayAfterMenu = 1.6f;
    [Tooltip("How long he is visible after the player leaves the register, then he is gone instantly. About 0.12 is a blink the player barely registers; he is always shown for at least 2 frames so he is never skipped.")]
    [SerializeField, Min(0f)] float cameoVisibleSeconds = 0.12f;

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
    bool cameoActive, cameoShownToPlayer, cameoUsed, cameoPending;
    float cameoSpawnAt;
    float cameoHideAt, cameoShownAt;
    int cameoShownFrame;

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
        if (runner == null) runner = FindObjectOfType<RunnerPuppet>(true);
    }

    void OnDestroy()
    {
        if (register != null) register.OnCashRegisterOpenedSuccessfully.RemoveListener(OnCodeAccepted);
    }

    void OnCodeAccepted()
    {
        codeDone = true;
        HideCameo();
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
        // the cameo appears once the register screen has faded to black (not while the world is still fading out)
        if (cameoPending && Time.unscaledTime >= cameoSpawnAt)
        {
            cameoPending = false;
            if (register != null && register.CashRegisterMenuIsActive() && !codeDone) SpawnCameo();
        }

        // the cameo ends on its own clock, whatever else is going on
        if (cameoActive && cameoShownToPlayer && Time.unscaledTime >= cameoHideAt && Time.frameCount >= cameoShownFrame + 2) HideCameo();

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
                if (!menuOpen)
                {
                    Current = State.Paused; pauseTimer = 0f;
                    Debug.Log("[RegisterFootsteps] player left the register - the steps stop");
                    ShowCameoToPlayer();
                    onPaused?.Invoke();
                    break;
                }
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
        if (runnerCameo && !cameoUsed)
        {
            cameoPending = true;
            cameoSpawnAt = Time.unscaledTime + Mathf.Max(0f, cameoDelayAfterMenu - menuTime);   // menuTime = how long the register has been open
        }
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
        HideCameo();
        DreadEvents.Release(10f);
        Debug.Log("[RegisterFootsteps] event over (" + why + ")");
        onEnded?.Invoke();
    }

    // ---------------------------------------------------------------- Runner cameo

    // The steps begin: he is suddenly just there, standing in the player's view, while the register screen hides the world.
    void SpawnCameo()
    {
        if (!runnerCameo || cameoUsed || runner == null) return;
        if (!TryFindCameoSpot(out Vector3 spot, out Vector3 look)) { Debug.Log("[RegisterFootsteps] runner cameo: no free spot in view, skipped"); return; }
        runner.Appear(spot, look);
        cameoActive = true;
        cameoShownToPlayer = false;
        Debug.Log("[RegisterFootsteps] runner cameo: he is standing at " + spot + " (hidden behind the register screen, t=" + Time.unscaledTime.ToString("F2") + ")");
    }

    // The player leaves the register: he is in view for a split second, then gone.
    void ShowCameoToPlayer()
    {
        if (!runnerCameo || cameoUsed || cameoShownToPlayer || runner == null) return;
        cameoPending = false;          // the player is leaving: no waiting for the screen to fade any more
        // look for a spot in the view the player has right now (the camera may have moved on, or there was no room earlier);
        // if there is none, fall back to wherever he was put when the steps began
        bool found = TryFindCameoSpot(out Vector3 spot, out Vector3 look);
        if (found) runner.Appear(spot, look);
        else if (!cameoActive) { Debug.Log("[RegisterFootsteps] runner cameo: no free spot in view, skipped"); return; }
        cameoActive = true;
        cameoShownToPlayer = true;
        cameoUsed = true;
        cameoShownAt = Time.unscaledTime;
        cameoShownFrame = Time.frameCount;
        cameoHideAt = cameoShownAt + cameoVisibleSeconds;
        Debug.Log("[RegisterFootsteps] runner cameo: the player sees him for " + cameoVisibleSeconds + " s");
    }

    void HideCameo()
    {
        cameoPending = false;          // also cancels one that has not appeared yet
        if (!cameoActive) return;
        bool wasShown = cameoShownToPlayer;
        cameoActive = false;
        cameoShownToPlayer = false;
        if (runner != null) runner.Vanish();
        Debug.Log("[RegisterFootsteps] runner cameo: gone" + (wasShown ? " (was visible " + (Time.unscaledTime - cameoShownAt).ToString("F3") + " s, " + (Time.frameCount - cameoShownFrame) + " frames)" : ""));
    }

    // A spot in front of the player's camera: on the floor, in view, with a clear line of sight and room to stand.
    bool TryFindCameoSpot(out Vector3 spot, out Vector3 look)
    {
        spot = look = Vector3.zero;
        if (cam == null) cam = Camera.main;
        if (playerT == null) { var p = FindObjectOfType<Player>(); if (p != null) playerT = p.transform; }
        if (cam == null || playerT == null) return false;

        // (RainBlocker is a big solid box over the whole interior that only the weather system cares about)
        int mask = ~LayerMask.GetMask("TransparentFX", "Ignore Raycast", "Water", "UI", "Player", "Enemy", "Hitbox", "Inspection", "Projectile", "RainBlocker");
        Vector3 camPos = cam.transform.position;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 0.01f ? playerT.forward : fwd.normalized;

        // every spot on a fan in front of the camera, the most central and mid-range ones tried first
        float mid = (cameoDistance.x + cameoDistance.y) * 0.5f;
        var tries = new System.Collections.Generic.List<Vector3>();       // (distance, yaw, cost)
        for (float d = cameoDistance.x; d <= cameoDistance.y + 0.01f; d += 0.75f)
            for (float yaw = -64f; yaw <= 64f; yaw += 8f)
                tries.Add(new Vector3(d, yaw, Mathf.Abs(yaw) / 64f + Mathf.Abs(d - mid) / Mathf.Max(1f, cameoDistance.y - cameoDistance.x)));
        tries.Sort((x, y) => x.z.CompareTo(y.z));

        foreach (Vector3 t in tries)
        {
            Vector3 p = camPos + Quaternion.Euler(0f, t.y, 0f) * fwd * t.x;
            Vector3 from = new Vector3(p.x, playerT.position.y + 1.5f, p.z);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.7f) continue;
            Vector3 pos = hit.point;
            if (Mathf.Abs(pos.y - playerT.position.y) > 0.6f) continue;                                                                      // the same floor as the player
            if (Physics.CheckCapsule(pos + Vector3.up * 0.4f, pos + Vector3.up * 1.6f, 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;   // no room (shelf, wall...)
            Vector3 chest = pos + Vector3.up * 1.4f;
            if (Physics.Linecast(camPos, chest, mask, QueryTriggerInteraction.Ignore)) continue;                                              // hidden behind something
            Vector3 vp = cam.WorldToViewportPoint(chest);
            if (vp.z <= 0f || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.08f || vp.y > 0.95f) continue;                                         // outside the picture
            spot = pos;
            look = new Vector3(camPos.x, pos.y, camPos.z);
            return true;
        }
        return false;
    }

    [ContextMenu("Test: runner cameo (spawn, then show as if the player just left)")]
    void TestCameo()
    {
        cameoUsed = false;
        if (runner == null) runner = FindObjectOfType<RunnerPuppet>(true);
        SpawnCameo();
        ShowCameoToPlayer();
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
