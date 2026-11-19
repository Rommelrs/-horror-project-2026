using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Block 3, the alley to the whispering gate (the black sliding gate at its east end). Once Adel has run into Block 3 (the
/// pole), crossing the line across the alley plays a short cutscene: the gate stands half open and Adel runs through the
/// gap and is gone. The enemies in the alley are frozen while it plays (no free hits on a held player). The gate stays open
/// until Khalid gets near it - then it slides shut in front of him. Trying the locked door after that: "That door was
/// open... I saw him go through..." Built by Tools > Cutscenes > Build Adel Door Sequence (AdelDoorSequenceBuilder).
/// </summary>
[DefaultExecutionOrder(150)]
public class AdelDoorSequence : MonoBehaviour
{
    public const string SeenFlag = "AdelDoorSeen";

    [SerializeField] string requiredFlag = AdelPoleSequence.DoneFlag;
    [Tooltip("The line across the alley (world box, no collider).")]
    [SerializeField] Vector3 lineCenter = new Vector3(-32.2f, 3f, -71.5f);
    [SerializeField] Vector3 lineSize = new Vector3(2.4f, 8f, 10.4f);

    [Header("The gate")]
    [SerializeField] Transform gate;
    [Tooltip("Where the gate sits half open (world offset from its closed spot): slid into the north wall, a gap at the south side.")]
    [SerializeField] Vector3 gateOpenOffset = new Vector3(0f, 0f, -3.4f);
    [SerializeField] float slamDistance = 7f;
    [SerializeField] DoorInteractable door;
    [SerializeField] AudioClip gateOpenClip, gateCloseClip;

    [Header("Adel")]
    [SerializeField] Animator adel;
    [SerializeField] List<Vector3> adelPath = new List<Vector3>();
    [SerializeField] float adelSpeed = 3.6f;

    [Header("Cameras / lines")]
    [SerializeField] Vector3 gapPoint = new Vector3(-16.1f, 1.4f, -68.1f);
    [SerializeField] Vector3 closeShot = new Vector3(-25.5f, 1.55f, -69.6f);
    [SerializeField] LocalizedString doorLine;
    [SerializeField] float freezeRadius = 40f;

    enum Phase { Waiting, Playing, GateOpen, Done }
    Phase phase = Phase.Waiting;
    Vector3 gateClosed;
    bool debugArmed;
    AudioSource sfx;
    CinemachineVirtualCamera cam;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    readonly List<(Enemy e, float animSpeed, bool agentStopped)> frozen = new List<(Enemy, float, bool)>();
    readonly List<Behaviour> disabled = new List<Behaviour>();
    bool pausedMovement, madeInvincible;

    public static bool IsPlaying { get; private set; }
    public string DebugState => phase + (Armed ? " armed" : "") + (StoryFlags.Has(SeenFlag) ? " seen" : "");

    bool Armed => debugArmed || (!StoryFlags.Has(SeenFlag) && (string.IsNullOrEmpty(requiredFlag) || StoryFlags.Has(requiredFlag)));

    void Awake()
    {
        if (gate != null) gateClosed = gate.position;
        var go = new GameObject("Adel Door Audio");
        go.transform.SetParent(transform, false);
        go.transform.position = gapPoint;
        sfx = go.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0.8f; sfx.rolloffMode = AudioRolloffMode.Linear; sfx.minDistance = 6f; sfx.maxDistance = 45f;
        var cgo = new GameObject("Adel door cam");
        cgo.transform.SetParent(transform, false);
        cam = cgo.AddComponent<CinemachineVirtualCamera>();
        cam.Priority = 0;
        cam.m_Lens.NearClipPlane = 0.08f; cam.m_Lens.FarClipPlane = 80f;
        if (adel != null) { adel.applyRootMotion = false; adel.cullingMode = AnimatorCullingMode.AlwaysAnimate; adel.gameObject.SetActive(false); }
    }

    void Start()
    {
        if (StoryFlags.Has(SeenFlag)) phase = Phase.Done;
    }

    void Update()
    {
        var p = Player.instance;
        if (p == null) return;
        switch (phase)
        {
            case Phase.Waiting:
                if (Armed && new Bounds(lineCenter, lineSize).Contains(p.transform.position) && CanStart(p)) StartCoroutine(Co_Play());
                break;
            case Phase.GateOpen:
                // shut in front of him as he gets near
                Vector3 d = p.transform.position - gapPoint; d.y = 0f;
                if (d.magnitude < slamDistance) StartCoroutine(Co_Slam());
                break;
        }
    }

    bool CanStart(Player p)
    {
        if (GameManager.IsPaused || CheckpointManager.IsLoadingCheckpoint) return false;
        if (p.pauseMovement || p.isGrabbed || p.IsDead()) return false;
        if (AdelPoleSequence.IsPlaying || KidnapSequence.IsPlaying || BridgeRunnerSequence.IsPlaying) return false;
        return true;
    }

    IEnumerator Co_Play()
    {
        var player = Player.instance;
        phase = Phase.Playing; IsPlaying = true;
        FreezeEnemies(true);
        Hold(player);
        // he stops and looks down the alley; the gate stands half open
        Vector3 toGap = gapPoint - player.transform.position; toGap.y = 0f;
        if (toGap.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(toGap);
        if (gate != null) gate.position = gateClosed + gateOpenOffset;

        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) { savedBlend = brain.m_DefaultBlend; brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f); }
        // one shot from beside the alley's south wall (over his shoulder the frozen enemies stood in the way): Adel runs past
        // it out of the dust, through the gap, and is gone
        Shot(closeShot, gapPoint, 52f);
        yield return Wait(0.25f);
        StartCoroutine(Co_AdelRun());
        // through the gap (the second last point) and a moment on - cut away while he is still running, so he never
        // vanishes on screen; he runs on deep into the fog and is gone
        while (adel != null && adel.gameObject.activeSelf && adelReached < adelPath.Count - 2) yield return null;
        yield return Wait(0.8f);

        StoryFlags.Set(SeenFlag);
        cam.Priority = 0;            // a cut back (a blend swept past the frozen enemies up close)
        yield return null;
        if (brain != null) brain.m_DefaultBlend = savedBlend;
        Release(player);
        FreezeEnemies(false);
        IsPlaying = false;
        phase = Phase.GateOpen;
    }

    int adelReached;

    IEnumerator Co_AdelRun()
    {
        adelReached = 0;
        if (adel == null || adelPath.Count < 2) yield break;
        adel.gameObject.SetActive(true);
        adel.transform.position = Ground(adelPath[0]);
        adel.Play("Run", 0, 0f);
        for (int i = 1; i < adelPath.Count; i++)
        {
            Vector3 to = Ground(adelPath[i]);
            while (true)
            {
                Vector3 pos = adel.transform.position, d = to - pos; d.y = 0f;
                if (d.magnitude < 0.05f) break;
                Vector3 step = d.normalized * Mathf.Min(d.magnitude, adelSpeed * Time.deltaTime);
                adel.transform.position = Ground(pos + step);
                adel.transform.rotation = Quaternion.Slerp(adel.transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-12f * Time.deltaTime));
                yield return null;
            }
            adelReached = i;
        }
        adel.gameObject.SetActive(false);
    }

    IEnumerator Co_Slam()
    {
        phase = Phase.Done;
        if (gateCloseClip != null) { sfx.transform.position = gapPoint; sfx.PlayOneShot(gateCloseClip, 1f); }
        if (gate == null) yield break;
        Vector3 from = gate.position;
        for (float e = 0f; e < 0.35f; e += Time.deltaTime)
        {
            float u = e / 0.35f;
            gate.position = Vector3.Lerp(from, gateClosed, u * u);
            yield return null;
        }
        gate.position = gateClosed;
    }

    /// <summary>The whispering door's OnInteractionFailed (it's locked): after the gate shut on him, Khalid says what he saw.</summary>
    public void OnDoorTried()
    {
        if (!StoryFlags.Has(SeenFlag) || doorLine == null || doorLine.IsEmpty) return;
        if (door != null && !door.hasDoorLock) return;
        if (Player.instance != null) Player.instance.SetLastInteractionTime(Time.unscaledTime);
        SubtitleManager.ShowSubtitle(doorLine, false);
    }

    void Shot(Vector3 pos, Vector3 target, float fov)
    {
        cam.m_Lens.FieldOfView = fov;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
        cam.PreviousStateIsValid = false;
        cam.Priority = 80;
    }

    // the alley's enemies stand still (AI, walk and animation) while Khalid can't move
    void FreezeEnemies(bool on)
    {
        if (on)
        {
            frozen.Clear();
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(e.transform.position, gapPoint) > freezeRadius) continue;
                var an = e.GetComponentInChildren<Animator>();
                bool stopped = e.agent != null && e.agent.isOnNavMesh && e.agent.isStopped;
                frozen.Add((e, an != null ? an.speed : 1f, stopped));
                e.PauseEnemyState(true);
                if (e.agent != null && e.agent.isOnNavMesh) { e.agent.isStopped = true; e.agent.velocity = Vector3.zero; }
                if (an != null) an.speed = 0f;
            }
        }
        else
        {
            foreach (var (e, animSpeed, stopped) in frozen)
            {
                if (e == null) continue;
                e.PauseEnemyState(false);
                if (e.agent != null && e.agent.isOnNavMesh) e.agent.isStopped = stopped;
                var an = e.GetComponentInChildren<Animator>();
                if (an != null) an.speed = animSpeed;
            }
            frozen.Clear();
        }
    }

    void Hold(Player player)
    {
        if (!player.pauseMovement) { player.pauseMovement = true; pausedMovement = true; }
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.SetExternalMoveInput(Vector2.zero);
        foreach (var b in new Behaviour[] { player.playerWeaponSystem, player.GetComponent<PlayerRoll>(), player.GetComponent<PlayerMeleeAttack>(), player.GetComponent<DoorInteractionHandler>() })
            if (b != null && b.enabled) { b.enabled = false; disabled.Add(b); }
        if (player.health != null && !player.health.Invincible) { player.health.Invincible = true; madeInvincible = true; }
        if (player.animator != null) { player.animator.SetBool("Turning", false); player.animator.SetFloat("x", 0f); player.animator.SetFloat("y", 0f); }
    }

    void Release(Player player)
    {
        if (player != null)
        {
            if (pausedMovement) player.pauseMovement = false;
            if (madeInvincible && player.health != null) player.health.Invincible = false;
        }
        pausedMovement = false; madeInvincible = false;
        foreach (var b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
    }

    void OnDisable()
    {
        if (phase == Phase.Playing)
        {
            Release(Player.instance);
            FreezeEnemies(false);
            if (cam != null) cam.Priority = 0;
            if (brain != null) brain.m_DefaultBlend = savedBlend;
            IsPlaying = false;
        }
    }

    IEnumerator Wait(float s) { for (float e = 0f; e < s; e += Time.deltaTime) yield return null; }

    Vector3 Ground(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (adel != null && h.collider.transform.IsChildOf(adel.transform)) continue;
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
            if (gate != null && h.collider.transform == gate) continue;
            if (h.collider.GetComponentInParent<Enemy>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        if (best > float.NegativeInfinity) p.y = best;
        return p;
    }

    /// <summary>Debug: armed, Khalid a few steps short of the line (walk east).</summary>
    public void DebugArmAndJump()
    {
        var p = Player.instance;
        if (!Application.isPlaying || p == null || phase == Phase.Playing) return;
        StopAllCoroutines();
        debugArmed = true;
        phase = Phase.Waiting;
        if (gate != null) gate.position = gateClosed;
        if (adel != null) adel.gameObject.SetActive(false);
        Vector3 at = Ground(new Vector3(lineCenter.x - 4.5f, 2f, lineCenter.z));
        if (p.controller != null) p.controller.enabled = false;
        p.transform.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.Euler(0f, 90f, 0f));
        if (p.controller != null) p.controller.enabled = true;
        p.OnPlayerTelported?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(lineCenter, lineSize);
        Gizmos.color = Color.yellow;
        for (int i = 1; i < adelPath.Count; i++) Gizmos.DrawLine(adelPath[i - 1], adelPath[i]);
    }
}
