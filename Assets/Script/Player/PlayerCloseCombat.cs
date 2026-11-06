using Cinemachine;
using UnityEngine;

/// <summary>
/// Khalid's side of the Questioner fight:
///  - Grabbed: the Questioner holds him, screams into his face, slowly bites his neck, and Khalid shoves him off - all on a
///    close over-the-shoulder camera that shakes with the scream and the bite. The only way out is a pipe strike timed into
///    the grab lunge (QuestionerCombatState).
///  - The Question (scream): slows him 30% for a moment; for ~6 s his aim sways (bullet spread + the rendered camera, so
///    the ADS view too), the screen blurs and smears like an echo and a high ringing fills his ears.
///  - Knockback: the Questioner's hits shove him back a step.
/// Khalid's reaction states (Grabbed / Bitten) play on the "CloseCombat" animator layer when it has them.
/// </summary>
public class PlayerCloseCombat : MonoBehaviour
{
    public static PlayerCloseCombat instance;

    [Header("Grab camera (close over Khalid's shoulder, the Questioner's face beyond it)")]
    [Tooltip("Behind his head (m), out to the side (m, + = his right shoulder), above his head (m).")]
    [SerializeField] float grabCamBack = 0.28f;
    [SerializeField] float grabCamSide = -0.8f;
    [SerializeField] float grabCamUp = 0.14f;
    [Tooltip("How far it creeps in while he's held (m).")]
    [SerializeField] float grabCamPushIn = 0.14f;
    [SerializeField] float grabCamFov = 50f;
    [SerializeField] float grabCamBlend = 0.3f;
    [Tooltip("Shake at full strength: degrees of rotation, metres of position.")]
    [SerializeField] float shakeDegrees = 2.2f;
    [SerializeField] float shakeMetres = 0.025f;
    [Tooltip("The shove that ends the grab: his movement is held and the camera stays this long (s).")]
    [SerializeField] float pushOffTime = 0.6f;

    [Header("Scream sway")]
    [Tooltip("Aim sway amplitude while shaken, in bullet-spread units (~0.08 = 4.5 deg).")]
    [SerializeField] float swayAmount = 0.08f;
    [SerializeField] float swayCameraDegrees = 2.5f;

    [Header("Scream blur / echo (screen) + ringing")]
    public Shader screamEchoShader;
    public AudioClip ringingSFX;
    [Range(0f, 1f)] [SerializeField] float ringingVolume = 0.35f;

    [Header("Animator (CloseCombat layer)")]
    [SerializeField] string layerName = "CloseCombat";
    [SerializeField] float layerBlend = 0.12f;

    [Header("Audio")]
    public AudioClip[] bittenSFX;
    public AudioClip[] pushOffSFX;

    public static float SpeedMultiplier => instance != null ? instance.CurrentSpeedMultiplier : 1f;
    /// <summary>Weight of the CloseCombat reaction layer (full body): the melee pipe's arm re-attach steps aside under it.</summary>
    public static float LayerWeight => instance != null ? instance.layerWeight : 0f;
    public static Vector2 AimSway => instance != null ? instance.currentSway : Vector2.zero;
    /// <summary>A Questioner hit's shove while it's on (PlayerMeleeWeapon moves him with it when the pipe is up); zero otherwise.</summary>
    public static Vector3 KnockVelocity => instance != null ? instance.knockVelocity : Vector3.zero;

    public bool IsGrabbed => grabber != null;
    public bool IsKnockedBack => Time.time < knockEnd;
    public bool IsPushingOff => Time.time < pushOffEnd;
    public bool CanBeGrabbed => player != null && !player.IsDead() && grabber == null && !player.isScared && !player.isKnockedDown;

    Player player;
    Animator animator;
    int layer = -1;
    float layerWeight, layerTarget, reactionEnd;
    float slowUntil, slowAmount;
    float swayUntil, swayStart;
    Vector2 currentSway;
    Vector3 knockDir, knockVelocity;
    float knockStart, knockEnd, knockDist;
    CinemachineVirtualCamera grabCam;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    bool blendSaved, grabCamOn;
    float grabCamStart, restoreBlendAt = -1f, stopCamAt = -1f;
    Transform khalidHead, grabberHead;
    QuestionerEnemy grabber, camTarget;
    float pushOffEnd;
    float shakeLevel, shakeUntil, shakeKick;
    ScreamEchoEffect echo;
    AudioSource ringSource;
    float ringStart, ringEnd;

    float CurrentSpeedMultiplier
    {
        get
        {
            if (IsGrabbed || IsKnockedBack || IsPushingOff) return 0f;
            return Time.time < slowUntil ? 1f - slowAmount : 1f;
        }
    }

    void Awake()
    {
        instance = this;
        player = GetComponent<Player>();
        animator = player != null ? player.animator : GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (layer < 0 && animator != null) layer = animator.GetLayerIndex(layerName);
        if (player == null) return;

        if (player.IsDead() && IsGrabbed) EndGrab(false);
        UpdateKnockback();
        UpdateSway();
        UpdateRinging();

        if (layer >= 0)
        {
            layerWeight = Mathf.MoveTowards(layerWeight, layerTarget, Time.deltaTime / Mathf.Max(0.01f, layerBlend));
            animator.SetLayerWeight(layer, layerWeight);
            if (layerTarget > 0f && !IsGrabbed && Time.time > reactionEnd) layerTarget = 0f;
        }
    }

    // ---------------- knockback ----------------

    void UpdateKnockback()
    {
        // a Questioner's hit shoving him back (decelerating over knockTime)
        if (IsKnockedBack)
        {
            float u = Mathf.Clamp01((Time.time - knockStart) / Mathf.Max(0.01f, knockEnd - knockStart));
            knockVelocity = knockDir * (knockDist / (knockEnd - knockStart) * 2f * (1f - u));
            if (!PlayerMeleeWeapon.Ready && player.controller != null && player.controller.enabled)
                player.controller.Move(knockVelocity * Time.deltaTime);
        }
        else knockVelocity = Vector3.zero;
    }

    /// <summary>An enemy hit shoves him back `distance` m over `time` s (his own movement is held meanwhile).</summary>
    public void Knockback(Vector3 dir, float distance, float time)
    {
        if (IsGrabbed || player.IsDead() || player.isKnockedDown) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        knockDir = dir.normalized;
        knockDist = distance;
        knockStart = Time.time;
        knockEnd = Time.time + Mathf.Max(0.05f, time);
    }

    // ---------------- grabbed ----------------

    public void BeginGrab(QuestionerEnemy q)
    {
        grabber = q;
        player.isGrabbed = true;
        if (player.controller != null) player.controller.Move(Vector3.zero);
        PlayReaction("Grabbed", 99f);
        PlayerMeleeWeapon.SetHiddenForGrab(true);
        StartGrabCam(q);
    }

    /// <summary>Keeps him where the Questioner holds him (in front of the split mouth), facing it.</summary>
    public void HoldAt(Vector3 position, Transform holder)
    {
        if (grabber == null) return;
        position.y = transform.position.y;
        Vector3 face = holder.position - position;
        face.y = 0f;
        var cc = player.controller;
        Vector3 delta = position - transform.position;
        if (cc != null && cc.enabled) cc.Move(Vector3.ClampMagnitude(delta, 8f * Time.deltaTime));
        else transform.position = Vector3.MoveTowards(transform.position, position, 8f * Time.deltaTime);
        if (face.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 720f * Time.deltaTime);
    }

    /// <summary>Let go. pushedOff = Khalid shoves him away: the shove plays and the camera holds on it a moment.</summary>
    public void EndGrab(bool pushedOff)
    {
        if (grabber == null) return;
        grabber = null;
        player.isGrabbed = false;
        if (pushedOff && !player.IsDead())
        {
            pushOffEnd = Time.time + pushOffTime;
            PlayReaction("PushOff", pushOffTime + 0.15f);
            Play(pushOffSFX, 0.9f);
            Shake(0.5f, 0.25f);
            stopCamAt = pushOffEnd - 0.1f;
        }
        else
        {
            // the holder is gone (killed, knocked down, despawned): the held pose ends now (its reaction runs "until released")
            StopGrabCam();
            PlayerMeleeWeapon.SetHiddenForGrab(false);
            reactionEnd = 0f;
            layerTarget = 0f;
        }
        if (player.playerWeaponSystem != null) player.playerWeaponSystem.SyncCameraYaw();
    }

    public void OnBitten()
    {
        Play(bittenSFX, 1f);
        PlayReaction("BittenHold", 99f);
        shakeKick = 1f;
        Shake(0.45f, 0.9f);
    }

    /// <summary>Grab camera shake: `amount` (0..1) for `seconds`, easing out at the end.</summary>
    public void Shake(float amount, float seconds)
    {
        if (amount >= shakeLevel || Time.time >= shakeUntil) shakeLevel = amount;
        shakeUntil = Mathf.Max(shakeUntil, Time.time + seconds);
    }

    // ---------------- grab camera ----------------

    static Transform FindBone(Transform root, params string[] names)
    {
        foreach (var n in names)
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == n) return t;
        return null;
    }

    void StartGrabCam(QuestionerEnemy q)
    {
        var main = Camera.main;
        if (main == null) return;
        brain = main.GetComponent<CinemachineBrain>();
        if (brain == null) return;
        if (grabCam == null)
        {
            var go = new GameObject("GrabCam");
            grabCam = go.AddComponent<CinemachineVirtualCamera>();
        }
        var lens = grabCam.m_Lens;
        lens.FieldOfView = grabCamFov;
        lens.NearClipPlane = 0.05f;
        grabCam.m_Lens = lens;
        if (khalidHead == null) khalidHead = FindBone(transform, "head.x", "Head");
        grabberHead = FindBone(q.transform, "Head");
        camTarget = q;
        stopCamAt = -1f;
        if (!blendSaved) { savedBlend = brain.m_DefaultBlend; blendSaved = true; }
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, grabCamBlend);
        restoreBlendAt = -1f;
        grabCamStart = Time.time;
        grabCamOn = true;
        PlaceGrabCam();
        grabCam.Priority = 1000;
        grabCam.gameObject.SetActive(true);
    }

    void StopGrabCam()
    {
        stopCamAt = -1f;
        if (!grabCamOn) return;
        grabCamOn = false;
        if (grabCam != null) grabCam.Priority = -100;
        restoreBlendAt = Time.time + grabCamBlend + 0.1f;     // keep the quick blend for the cut back
    }

    void PlaceGrabCam()
    {
        if (grabCam == null || camTarget == null) return;
        // over his shoulder (in his own frame, so the head's struggle doesn't swing it about), looking past his head at the
        // Questioner's face
        Vector3 a = khalidHead != null ? khalidHead.position : transform.position + Vector3.up * 1.65f;
        Vector3 b = grabberHead != null ? grabberHead.position : camTarget.transform.position + Vector3.up * 1.8f;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 headBase = new Vector3(transform.position.x, a.y, transform.position.z);
        float push = Mathf.Clamp01((Time.time - grabCamStart) / 2.5f) * grabCamPushIn;
        Vector3 pos = headBase - fwd * (grabCamBack - push) + right * grabCamSide + Vector3.up * grabCamUp;
        Vector3 look = Vector3.Lerp(a, b, 0.72f) + Vector3.down * 0.06f;
        Quaternion rot = Quaternion.LookRotation(look - pos, Vector3.up);

        // shake: a level that eases out at the end, plus a kick that dies quickly (the bite)
        shakeKick = Mathf.MoveTowards(shakeKick, 0f, Time.deltaTime * 4f);
        float lvl = (Time.time < shakeUntil ? shakeLevel * Mathf.Clamp01((shakeUntil - Time.time) / 0.25f) : 0f) + shakeKick * shakeKick;
        if (lvl > 0.001f)
        {
            float t = Time.time * 23f;
            Vector3 n = new Vector3(Mathf.PerlinNoise(t, 0.1f), Mathf.PerlinNoise(0.4f, t), Mathf.PerlinNoise(t, 0.8f)) - Vector3.one * 0.5f;
            rot = rot * Quaternion.Euler(n.x * 2f * shakeDegrees * lvl, n.y * 2f * shakeDegrees * lvl, n.z * shakeDegrees * lvl);
            pos += (right * n.y + Vector3.up * n.x) * 2f * shakeMetres * lvl;
        }
        grabCam.transform.SetPositionAndRotation(pos, rot);
    }

    void LateUpdate()
    {
        if (stopCamAt > 0f && Time.time >= stopCamAt)
        {
            StopGrabCam();
            PlayerMeleeWeapon.SetHiddenForGrab(false);
            if (Time.time > reactionEnd) layerTarget = 0f;
        }
        if (grabCamOn) PlaceGrabCam();
        if (restoreBlendAt > 0f && Time.time >= restoreBlendAt)
        {
            restoreBlendAt = -1f;
            if (brain != null && blendSaved) brain.m_DefaultBlend = savedBlend;
            blendSaved = false;
            if (grabCam != null) grabCam.gameObject.SetActive(false);
        }
    }

    // ---------------- scream ----------------

    ScreamEchoEffect Echo()
    {
        var cam = Camera.main;
        if (cam == null || screamEchoShader == null) return null;
        if (echo == null || echo.gameObject != cam.gameObject)
        {
            echo = cam.GetComponent<ScreamEchoEffect>();
            if (echo == null) { echo = cam.gameObject.AddComponent<ScreamEchoEffect>(); echo.shader = screamEchoShader; }
        }
        return echo;
    }

    /// <summary>The Question hit him: slowed by `slow` for `slowTime` (0 = not slowed), and for `effectTime` the aim sway, the
    /// screen blur / echo and the ringing. `strength` (0..1) scales the blur / ringing (how close he was).</summary>
    public void ApplyScream(float slow, float slowTime, float effectTime, float strength = 1f)
    {
        if (slowTime > 0f)
        {
            slowAmount = slow;
            slowUntil = Time.time + slowTime;
        }
        swayStart = Time.time;
        swayUntil = Time.time + effectTime;

        float length = effectTime;
        if (Echo() != null) echo.Trigger(length, Mathf.Lerp(0.55f, 1f, strength));

        if (ringingSFX != null)
        {
            if (ringSource == null)
            {
                ringSource = gameObject.AddComponent<AudioSource>();
                ringSource.playOnAwake = false;
                ringSource.spatialBlend = 0f;
                ringSource.loop = false;
                ringSource.ignoreListenerPause = false;
                var main = SoundEffectManager.instance != null ? SoundEffectManager.instance.GetComponent<AudioSource>() : null;
                if (main != null) ringSource.outputAudioMixerGroup = main.outputAudioMixerGroup;
            }
            ringSource.clip = ringingSFX;
            ringSource.volume = ringingVolume * Mathf.Lerp(0.6f, 1f, strength) * (SoundEffectManager.instance != null ? SoundEffectManager.instance.masterVolume : 1f);
            ringSource.Play();
            ringStart = Time.time;
            ringEnd = Time.time + Mathf.Min(length + 0.4f, ringingSFX.length);
        }
    }

    /// <summary>Ends what a scream left on him at once (the ringing, blur, aim sway and slow): a cutscene is taking over.</summary>
    public void ClearScream()
    {
        if (ringSource != null) ringSource.Stop();
        if (echo != null) echo.Stop();
        swayUntil = 0f;
        slowUntil = 0f;
    }

    void UpdateRinging()
    {
        if (ringSource == null || !ringSource.isPlaying) return;
        // fades under its own clip, but cut it short (and quiet) once he's on the ground or paused
        if (GameManager.IsPaused || player.IsDead() || Time.time > ringEnd) ringSource.Stop();
    }

    void UpdateSway()
    {
        float left = swayUntil - Time.time;
        if (left > 0f)
        {
            // full for most of it, easing off over the last 1.5 s
            float env = Mathf.Clamp01(left / 1.5f) * Mathf.Clamp01((Time.time - swayStart) / 0.1f);
            float t = Time.time * 2.3f;
            currentSway = new Vector2(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * swayAmount * env;
        }
        else currentSway = Vector2.zero;
    }

    /// <summary>After the Cinemachine brain has placed the camera (whichever virtual camera is live - the normal follow, the
    /// ADS aim cam or the grab cam): tilt it by the sway. The brain sets it fresh every frame, so this never accumulates.</summary>
    void OnCameraUpdated(CinemachineBrain brain)
    {
        if (currentSway == Vector2.zero || brain == null || brain.OutputCamera == null) return;
        float k = swayCameraDegrees / Mathf.Max(0.001f, swayAmount);
        var t = brain.OutputCamera.transform;
        t.rotation = t.rotation * Quaternion.Euler(-currentSway.y * k, currentSway.x * k, currentSway.x * k * 0.5f);
    }

    // ---------------- animation / audio ----------------

    void PlayReaction(string state, float duration)
    {
        if (animator == null || layer < 0) return;
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(layer, hash)) return;
        animator.CrossFadeInFixedTime(hash, 0.08f, layer, 0f);
        layerTarget = 1f;
        reactionEnd = Time.time + duration;
    }

    void Play(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        var c = clips[Random.Range(0, clips.Length)];
        if (c != null && SoundEffectManager.instance != null) SoundEffectManager.instance.PlaySFX(c, volume, true);
    }
}
