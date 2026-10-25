using CartoonFX;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using ToolBox.Pools;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.InputSystem;

public class PlayerWeaponSystem : MonoBehaviour
{
    [Header("Enabler")]
    public bool weaponIsEnabled = false;

    [Header("Sound Alert")]
    [SerializeField] float gunSoundRadius = 20f;

    [Header("Input")]
    [SerializeField] InputActionReference shootInput;
    [SerializeField] InputActionReference aimInput;
    [SerializeField] InputActionReference lookInput;
    [SerializeField] InputActionReference moveInput;
    [SerializeField] InputActionReference reloadInput;

    /// <summary>Raised each time the player actually fires a shot (aiming, ammo available).</summary>
    public event System.Action OnShotFired;

    /// <summary>Current look input (mouse delta / right stick), same scaling the aim camera uses.</summary>
    public Vector2 ReadLookInput()
    {
        return lookInput != null ? lookInput.action.ReadValue<Vector2>() : Vector2.zero;
    }

    public CinemachineVirtualCamera aimCam;
    public bool isAiming = false;
    [SerializeField] Image aimCrosshairImage;

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private const float _threshold = 0.01f;
    public bool LockCameraPosition = false;
    public bool aimingOverrideForTesting = false;
    /// <summary>Set true to block HandleAiming (e.g. downed state handles ADS externally).</summary>
    [HideInInspector] public bool blockAimingSystem = false;
    /// <summary>Set true to grant infinite ammo (ammo is never decremented). Restored to false on stand up.</summary>
    [HideInInspector] public bool infiniteAmmo = false;

    [Header("Audio")]
    [SerializeField] AudioClip pistolShootClip;
    [SerializeField] AudioClip pistolDryFireClip;
    [SerializeField] AudioClip pullOutGunClip;
    [SerializeField] AudioSource pistolReloadAudioSource;
    [SerializeField] AudioClip outOfAmmoClip;
    [SerializeField] AudioClip []hitSFX;
    [SerializeField] AudioClip headshotSFX;

    [Header("WeakPoint")]
    [SerializeField] AudioSource weakPointAudioSource;
    [SerializeField] AudioClip []weakPointHitSoundEffects;
    [SerializeField] AudioClip[] weakPointBonusSounds; // Random sound always plays alongside weakpoint hits
    [Tooltip("Volume of the bonus sound that plays on every weak point hit (it was a fixed 5). Lower = quieter. The main hit sound's own volume is on the WeakpointAudioSource.")]
    [SerializeField] float weakPointBonusVolume = 2.5f;
    [SerializeField] float weakPointMinPitch = 0.5f;
    [SerializeField] float weakPointMaxPitch = 1f;

    [Header("Camera Shake")]
    [SerializeField] CameraShaker cameraShaker;
    [SerializeField] float shakeIntensity = 1f;
    [SerializeField] float shakeDuration = 0.15f;

    [Header("Cinemachine")]
    public float Sensitivity = 1f;
    public GameObject CinemachineCameraTarget;
    public float TopClamp = 70.0f;
    public float BottomClamp = -30.0f;
    public float CameraAngleOverride = 0.0f;
    public float firstPersonSwitchDelay = 0.5f;
    public float thirdPersonSwitchDelay = 0.5f;

    public Animator animator;
    public Animator firstPersonAnimator;
    public Animator firstPersonAnimRoot;
    public Renderer[] thirdPersonRenderers;
    public GameObject[] thirdPersonObjects;
    public GameObject[] firstPersonObjects;

    [Header("Shooting")]
    public int damage = 50;
    public float fireRate = 0.25f;
    public float weaponRange = 20f;
    public float hitstopDuration = 0.1f;
    public float weakpointHitEnemyKnockbackMultiplier = 1.5f;

    public LayerMask hitLayer;
    public GameObject sandHitEffect;
    public GameObject metalHitEffect;
    public GameObject fleshHitEffect;
    public GameObject fleshHitBigEffect;

    [Header("FirstPerson")]
    public Transform gunEnd;
    public ParticleSystem muzzleFlash;
    public ParticleSystem cartridgeEjection;
   
    [Header("ThirdPerson")]
    public Transform gunEndTP;
    public ParticleSystem muzzleFlashTP;
    public ParticleSystem cartridgeEjectionTP;

    [Header("Weapon Ammo")]
    public Item weaponAmmoItem;
    public TMP_Text ammoTxt;
    public int currentAmmo;
    public int maxAmmoCapacity;

    [Header("Reloading")]
    [Tooltip("Should match the FULL reload animation length including exit transition time")]
    public float reloadDuration = 1.8f; // Increased to cover full animation + blend out
    public bool isReloading = false;
    private bool isReloadingInFirstPerson = false; // Track if reload started in first-person
    public ReloadTimeThreshold []reloadTimeThresholds;
    private Coroutine reloadCoroutine = null;

    // ---- Reload skill check support (see ReloadSkillCheck) ----
    // The reload timer runs on "progress" instead of a fixed wait, so its speed can be raised mid-reload.
    float reloadSpeedScale = 1f;            // 1 = normal speed
    float reloadBaseAnimMultiplier = 1f;    // animation speed the stability slowdown asked for
    float reloadTotalTime;
    float reloadTimeRemaining;
    Animator reloadAnimator;                // the animator currently playing the reload
    bool reloadAnimatorIsFirstPerson;

    public float ReloadSpeedScale => reloadSpeedScale;
    public float ReloadTotalTime => reloadTotalTime;
    public float ReloadTimeRemaining => reloadTimeRemaining;
    public float ReloadProgress01 => reloadTotalTime > 0f ? Mathf.Clamp01(1f - reloadTimeRemaining / reloadTotalTime) : 0f;
    public bool ReloadPressedThisFrame => reloadInput != null && reloadInput.action.WasPressedThisFrame();

    /// <summary>0 = shaky hands, 1 = fully steady (the calming inhaler counts as steady).</summary>
    public float StabilityFraction
    {
        get
        {
            if (playerStability == null || playerStability.calmingInhalerIsActive) return 1f;
            return Mathf.Clamp01(playerStability.stability / (float)Mathf.Max(1, playerStability.maxStability));
        }
    }

    /// <summary>Speeds up the reload that is currently in progress (timer and animation): 2 = twice as fast, 5 = five times.</summary>
    public void SetReloadSpeedScale(float scale)
    {
        if (!isReloading) return;

        reloadSpeedScale = Mathf.Max(1f, scale);
        float animSpeed = reloadBaseAnimMultiplier * reloadSpeedScale;

        if (reloadAnimator != null)
            reloadAnimator.SetFloat("ReloadTimeMultiplier", animSpeed);
        if (reloadAnimatorIsFirstPerson && firstPersonAnimRoot != null)
            firstPersonAnimRoot.SetFloat("ReloadTimeMultiplier", animSpeed);
    }

    [Header("Weapon Spread")]
    [Tooltip("How inaccurate the weapon is. 0 = perfect accuracy.")]
    public float bulletSpread = 0.02f;
    public BulletSpreadThreshold []bulletSpreadThresholds;

    [Header("Bullet Pierce")]
    public int maxBulletPierceCount = 1;
    public float bulletPierceDelay = 0.35f;

    [Header("Pellets (shotgun)")]
    [Tooltip("1 = a single bullet (pistol). More = a shotgun cone: 'damage' is the whole shot at point blank, split between the pellets. Set by PlayerWeaponSwitcher for the gun that is out.")]
    public int pelletsPerShot = 1;
    [Tooltip("Radius of the pellet cone (same units as bulletSpread).")]
    public float pelletSpread = 0.06f;
    [Tooltip("Pellets do full damage up to this distance, then fall off towards pelletMinDamageFraction at weaponRange.")]
    public float pelletFalloffStart = 6f;
    public float pelletMinDamageFraction = 0.3f;
    [Tooltip("Pellets are traced from this far behind the muzzle (about the hands / eyes), so an enemy already touching the barrels - the muzzle inside its collider - is still hit point blank.")]
    public float pelletTraceBack = 0.9f;
    [Tooltip("How hard a full point-blank blast shoves an enemy back, as a multiple of its normal hit knockback. Scales down with how much of the shot landed.")]
    public float pelletKnockback = 4f;

    // Per-gun overrides set by PlayerWeaponSwitcher (null / defaults = the pistol)
    [HideInInspector] public AudioClip shootClipOverride;
    [HideInInspector] public float shootClipVolume = 0.8f;
    [HideInInspector] public bool ejectCartridge = true;
    [HideInInspector] public float shotKick = 0f;           // camera shake on every shot (0 = none)
    AudioClip ShootClip => shootClipOverride != null ? shootClipOverride : pistolShootClip;

    [System.Serializable]
    public struct BulletSpreadThreshold
    {
        public float maxStability;
        public float minStability;
        public float bulletSpreadMultiplier;
    }

    [System.Serializable]
    public struct ReloadTimeThreshold
    {
        public float maxStability;
        public float minStability;
        public float reloadDurationMultiplier;
    }

    [Header("Auto Rotate Settings")]
    public float autoRotateDuration = 1f;
    public float rotationSmoothness = 2f;
    private bool isAutoRotating = false;
    private float targetYaw;
    Coroutine autoRotateCR;

    public GameObject damageImpactTextPrefab;

    float stabilityDecreaseCooldownPeriod = 40f;
    float nextStabilityDecreaseTime = 0f;
    private float nextFire;

    PlayerEventHandler playerEvent; // Third-person event handler
    PlayerEventHandler firstPersonEventHandler; // First-person event handler
    Player player;
    AudioSource audioSource;
    Inventory inventory;
    PlayerStability playerStability;

    Coroutine enableFPRendererCR;
    Coroutine enableTPRendererCR;

    private void Awake()
    {
        player = GetComponent<Player>();
        
        // Get ALL PlayerEventHandler components (third-person and first-person)
        PlayerEventHandler[] allEventHandlers = GetComponentsInChildren<PlayerEventHandler>();
        
        // Assign the first one to playerEvent (likely third-person)
        if (allEventHandlers.Length > 0)
            playerEvent = allEventHandlers[0];
        
        // Assign the second one to firstPersonEventHandler if it exists
        if (allEventHandlers.Length > 1)
            firstPersonEventHandler = allEventHandlers[1];
        
        audioSource = GetComponent<AudioSource>();
        inventory = GetComponent<Inventory>();
        playerStability = GetComponent<PlayerStability>();
    }

    private void Start()
    {
        // Load saved mouse sensitivity
        if (PlayerPrefs.HasKey("MouseSensitivity"))
            Sensitivity = PlayerPrefs.GetFloat("MouseSensitivity");

        // Subscribe to third-person event handler
        if (playerEvent != null)
        {
            playerEvent.OnShoot += ThirdPersonShoot;
            playerEvent.OnReloadComplete.AddListener(OnReloadComplete);
            playerEvent.OnReloadAnimationEnd += OnReloadAnimationEnd;
        }
        
        // Subscribe to first-person event handler
        if (firstPersonEventHandler != null)
        {
            firstPersonEventHandler.OnReloadComplete.AddListener(OnReloadComplete);
            firstPersonEventHandler.OnReloadAnimationEnd += OnReloadAnimationEnd;
        }

        UpdateAmmoCount();
    }

    private void OnDestroy()
    {
        // Unsubscribe from third-person event handler
        if (playerEvent != null)
        {
            playerEvent.OnShoot -= ThirdPersonShoot;
            playerEvent.OnReloadComplete.RemoveListener(OnReloadComplete);
            playerEvent.OnReloadAnimationEnd -= OnReloadAnimationEnd;
        }
        
        // Unsubscribe from first-person event handler
        if (firstPersonEventHandler != null)
        {
            firstPersonEventHandler.OnReloadComplete.RemoveListener(OnReloadComplete);
            firstPersonEventHandler.OnReloadAnimationEnd -= OnReloadAnimationEnd;
        }
    }

    private void Update()
    {
        //Check if weapon is Enabled
        if (!weaponIsEnabled)
            return;

        //Check if player is dead
        if (player.IsDead())
            return;

        //Check if game is over ow won
        if (LevelManager.instance != null && (LevelManager.instance.isGameOver || LevelManager.instance.isGameWon))
            return;

        if (GameManager.IsPaused)
            return;

        //Check If EyePeak Mode is activated
        if (EyePeakHandler.instance && EyePeakHandler.instance.IsEyePeakActivated())
        {
            //Reset
            if (aimCrosshairImage != null)
                aimCrosshairImage.DOKill();

            //Disable Crosshair
            if (aimCrosshairImage.color.a > 0)
            {
                Color col = aimCrosshairImage.color;
                col.a = 0f;
                aimCrosshairImage.color = col;
            }
        }

        if (!blockAimingSystem) HandleAiming();
        HandleReloading();
        HandleShooting();
    }

    bool HasEnoughAmmo()
    {
        if (infiniteAmmo) return true;
        if(currentAmmo > 0)
            return true;

        return false;
    }

    void UpdateAmmoCount()
    {
        if(ammoTxt != null)
            ammoTxt.text = currentAmmo.ToString();
    }

    /// <summary>Spare rounds for this weapon in the player's inventory (what a reload draws from).</summary>
    public int GetReserveAmmo()
    {
        if (inventory != null && weaponAmmoItem != null && inventory.HasWeaponAmmo(weaponAmmoItem, out int reserve))
            return reserve;

        return 0;
    }

    void HandleAiming()
    {
        // Block aim/transition during third-person reload
        if (isReloading && !isReloadingInFirstPerson)
        {
            return;
        }
        
        if (aimInput.action.ReadValue<float>() > 0.1f || aimingOverrideForTesting)
        {
            if (isAiming == false)
            {
                isAiming = true;
                aimCam.Priority = 2;

                //Reset
                if (aimCrosshairImage != null)
                {
                    aimCrosshairImage.DOKill();

                    Color aimColor = aimCrosshairImage.color;
                    aimColor.a = 0f;
                    aimCrosshairImage.color = aimColor;

                    aimCrosshairImage.DOFade(1f, 0.25f).SetEase(Ease.Linear);
                }

                //Play SFX
                if (SoundEffectManager.instance != null)
                    SoundEffectManager.instance.PlaySFX(pullOutGunClip);

                if (isReloading)
                {
                    // Cancel active reload coroutine
                    if (reloadCoroutine != null)
                    {
                        StopCoroutine(reloadCoroutine);
                        reloadCoroutine = null;
                    }
                    
                    isReloading = false;

                    if(pistolReloadAudioSource.isPlaying)
                        pistolReloadAudioSource.Stop();

                    animator.SetBool("Reload", false);
                    firstPersonAnimator.SetBool("Reload", false);
                }

                if (enableTPRendererCR != null) StopCoroutine(enableTPRendererCR);

                if(enableFPRendererCR != null) StopCoroutine(enableFPRendererCR);
                enableFPRendererCR = StartCoroutine(Co_EnableFPRenderer());       

                _cinemachineTargetPitch = 0.0f;
                _cinemachineTargetYaw = transform.eulerAngles.y;
            }

            animator.SetBool("Aiming", true);
            firstPersonAnimator.SetBool("Aiming", true);

            CameraRotation();
        }
        else
        {
            // Block aim exit if reloading in first-person
            if (isReloading && isReloadingInFirstPerson)
            {
                // Keep aiming active during first-person reload
                return;
            }
            
            if (isAiming)
            {
                isAiming = false;
                aimCam.Priority = 0;

                //Reset
                if (aimCrosshairImage != null)
                {
                    aimCrosshairImage.DOKill();

                    Color aimColor = aimCrosshairImage.color;
                    aimColor.a = 1f;
                    aimCrosshairImage.color = aimColor;

                    aimCrosshairImage.DOFade(0f, 0.15f).SetEase(Ease.Linear);
                }

                if (pistolReloadAudioSource.isPlaying)
                    pistolReloadAudioSource.Stop();

                firstPersonAnimator.SetBool("Reload", false);

                if (enableFPRendererCR != null) StopCoroutine(enableFPRendererCR);

                if (enableTPRendererCR != null) StopCoroutine(enableTPRendererCR);
                enableTPRendererCR = StartCoroutine(Co_EnableTPRenderer());
            }

            //animator.SetBool("Aiming", false);
            firstPersonAnimator.SetBool("Aiming", true);
        }
    }

    IEnumerator Co_EnableFPRenderer()
    {
        yield return new WaitForSeconds(firstPersonSwitchDelay);

        foreach (var r in thirdPersonRenderers)
        {
            r.enabled = false;
        }

        foreach (var o in firstPersonObjects)
        {
            o.gameObject.SetActive(true);
        }

        foreach (var o in thirdPersonObjects)
        {
            o.gameObject.SetActive(false);
        }
    }

    IEnumerator Co_EnableTPRenderer()
    {
        yield return new WaitForSeconds(thirdPersonSwitchDelay);

        foreach (var r in thirdPersonRenderers)
        {
            r.enabled = true;
        }

        foreach (var o in firstPersonObjects)
        {
            o.gameObject.SetActive(false);
        }

        foreach (var o in thirdPersonObjects)
        {
            o.gameObject.SetActive(true);
        }

        animator.SetBool("Aiming", false);
    }

    /// <summary>
    /// Syncs the internal camera yaw tracker to the player's current Y rotation.
    /// Call after externally rotating the player so the camera doesn't snap on the next aim input.
    /// </summary>
    public void SyncCameraYaw()
    {
        _cinemachineTargetYaw = transform.eulerAngles.y;
    }

    public void ExitOutOfAiming()
    {
        if (isAiming)
        {
            isAiming = false;
            aimCam.Priority = 0;

            //Reset
            if (aimCrosshairImage != null)
                aimCrosshairImage.DOKill();

            Color aimColor = aimCrosshairImage.color;
            aimColor.a = 1f;
            aimCrosshairImage.color = aimColor;

            aimCrosshairImage.DOFade(0f, 0.15f).SetEase(Ease.Linear);

            if (pistolReloadAudioSource.isPlaying)
                pistolReloadAudioSource.Stop();

            firstPersonAnimator.SetBool("Reload", false);

            if (enableFPRendererCR != null) StopCoroutine(enableFPRendererCR);

            foreach (var r in thirdPersonRenderers)
            {
                r.enabled = true;
            }

            foreach (var o in firstPersonObjects)
            {
                o.gameObject.SetActive(false);
            }

            foreach (var o in thirdPersonObjects)
            {
                o.gameObject.SetActive(true);
            }
        }

        animator.SetBool("Aiming", false);
        firstPersonAnimator.SetBool("Aiming", false);
    }

    void HandleShooting()
    {
        //Handle Shooting - block if reloading
        if (shootInput.action.WasPressedThisFrame() && Time.time > nextFire && !isReloading)
        {
            if (HasEnoughAmmo())
            {
                nextFire = Time.time + fireRate;

                if (isAiming)
                {
                    if (!infiniteAmmo)
                    {
                        currentAmmo--;
                        if (currentAmmo <= 0) currentAmmo = 0;
                        UpdateAmmoCount();
                    }

                    //Trigger Shoot Animation
                    firstPersonAnimRoot.SetTrigger("Shoot");
                    OnShotFired?.Invoke();

                    ////Play SFX
                    //audioSource.PlayOneShot(pistolShootClip);

                    //Play Shoot SFX
                    SoundEffectManager.instance.PlaySFX(ShootClip, shootClipVolume, true);

                    // Alert nearby enemies to the gunshot sound
                    AlertEnemiesInRadius();

                    //Shooting in First Person View
                    muzzleFlash.Play();
                    if (ejectCartridge) cartridgeEjection.Play();
                    if (shotKick > 0f && cameraShaker != null) cameraShaker.ApplyShake(shotKick, shakeDuration);

                    float currentBulletSpread = bulletSpread;

                    if (!playerStability.calmingInhalerIsActive)
                    {
                        foreach (BulletSpreadThreshold bulletSpreadThreshold in bulletSpreadThresholds)
                        {
                            if (playerStability.stability >= bulletSpreadThreshold.minStability && playerStability.stability < bulletSpreadThreshold.maxStability)
                            {
                                currentBulletSpread *= bulletSpreadThreshold.bulletSpreadMultiplier;
                                break;
                            }
                        }
                    }

                    Vector3 rayOrigin = gunEnd.position;
                    //Vector3 direction = ApplyBulletSpread(gunEnd, 0);
                    Vector3 camPosition = Camera.main.transform.position;
                    //camPosition.y = rayOrigin.y;
                    Vector3 direction = gunEnd.transform.position - camPosition;

                    //Debug Dray Ray
                    Debug.DrawLine(rayOrigin, rayOrigin + (gunEnd.forward * weaponRange), Color.red, 4f);

                    if (pelletsPerShot > 1)
                    {
                        FirePellets(rayOrigin, gunEnd, 0f);
                    }
                    else
                    {
                        RaycastHit []hits = Physics.RaycastAll(rayOrigin, gunEnd.forward, weaponRange, hitLayer, QueryTriggerInteraction.Ignore);
                        if (hits != null && hits.Length > 0)
                        {
                            HandleHits(hits);
                        }
                    }
                }
                else
                {
                    //Trigger Shoot Animation
                    animator.SetTrigger("Shoot");
                }
            }
            else
            {
                //Does not have enough Ammo

                //Play Dry Fire SFX
                audioSource.PlayOneShot(pistolDryFireClip);
            }
        }
    }

    Vector3 ApplyBulletSpread(Transform gunEndTransform, float bulletSpread)
    {
        // Random small offset
        float spreadX = Random.Range(-bulletSpread, bulletSpread);
        float spreadY = Random.Range(-bulletSpread, bulletSpread);

        // Apply spread to forward direction
        Vector3 direction = gunEndTransform.forward + (gunEndTransform.right * spreadX) + (gunEndTransform.up * spreadY);
        return direction.normalized;
    }

    void HandleReloading()
    {
        //Handle Reloading - prevent spamming by checking if reload is already in progress
        if (reloadInput.action.WasPressedThisFrame() && !isReloading && currentAmmo < maxAmmoCapacity)
        {
            if (inventory.HasWeaponAmmo(weaponAmmoItem, out int ammoCount))
            {
                isReloading = true;

                float processesReloadTime = reloadDuration;
                float reloadDurationMultiplier = 1.0f;
                if (!playerStability.calmingInhalerIsActive)
                {
                    foreach (ReloadTimeThreshold reloadThreshold in reloadTimeThresholds)
                    {
                        if (playerStability.stability >= reloadThreshold.minStability && playerStability.stability < reloadThreshold.maxStability)
                        {
                            reloadDurationMultiplier = reloadThreshold.reloadDurationMultiplier;
                            processesReloadTime *= reloadThreshold.reloadDurationMultiplier;
                            break;
                        }
                    }
                }

                //Play Reload SFX after all checks pass
                pistolReloadAudioSource.Play();
                
                if (isAiming)
                {
                    //First Person Reload
                    isReloadingInFirstPerson = true; // Flag that reload started in first-person
                    float reloadTimeMultiplier = 1.0f / (float)reloadDurationMultiplier;

                    firstPersonAnimator.SetFloat("ReloadTimeMultiplier", reloadTimeMultiplier);
                    firstPersonAnimator.SetBool("Reload", true);

                    firstPersonAnimRoot.SetFloat("ReloadTimeMultiplier", reloadTimeMultiplier);
                    firstPersonAnimRoot.SetTrigger("Reload");
                    reloadBaseAnimMultiplier = reloadTimeMultiplier;
                    reloadAnimator = firstPersonAnimator;
                    reloadAnimatorIsFirstPerson = true;
                    reloadCoroutine = StartCoroutine(Co_ResetReloadTrigger(firstPersonAnimator, processesReloadTime));
                }
                else
                {
                    //Third Person Reload
                    isReloadingInFirstPerson = false; // Not in first-person
                    float reloadTimeMultiplier = 1.0f / (float)reloadDurationMultiplier;

                    animator.SetFloat("ReloadTimeMultiplier", reloadTimeMultiplier);
                    animator.SetBool("Reload", true);

                    reloadBaseAnimMultiplier = reloadTimeMultiplier;
                    reloadAnimator = animator;
                    reloadAnimatorIsFirstPerson = false;
                    reloadCoroutine = StartCoroutine(Co_ResetReloadTrigger(animator, processesReloadTime));
                }
            }
            else
            {
                //Player out of ammo
                audioSource.PlayOneShot(outOfAmmoClip);

                if(Time.time > nextStabilityDecreaseTime)
                {
                    nextStabilityDecreaseTime = Time.time + stabilityDecreaseCooldownPeriod;

                    //Decrease Stability
                    playerStability.DecreaseStability(10);
                }
            }
        }   
    }

    public void OnReloadComplete()
    {
        if (inventory.HasWeaponAmmo(weaponAmmoItem, out int ammoCount))
        {
            if (currentAmmo < maxAmmoCapacity)
            {
                int ammoToAdd = maxAmmoCapacity - currentAmmo;
                ammoToAdd = Mathf.Clamp(ammoToAdd, 0, ammoCount);

                //Remove Item from Inventory
                inventory.RemoveItem(weaponAmmoItem, ammoToAdd);

                currentAmmo += ammoToAdd;
                UpdateAmmoCount();
            }
        }
    }

    IEnumerator Co_ResetReloadTrigger(Animator anim, float duration)
    {
        // Wait for the calculated reload duration (base duration * stability multiplier). It counts down
        // at reloadSpeedScale, so a successful skill check can shorten what is left of it.
        reloadSpeedScale = 1f;
        reloadTotalTime = duration;
        reloadTimeRemaining = duration;
        while (reloadTimeRemaining > 0f)
        {
            reloadTimeRemaining -= Time.deltaTime * reloadSpeedScale;
            yield return null;
        }
        reloadSpeedScale = 1f;
        reloadTotalTime = 0f;

        // Always unlock after duration - no dependency on animation events
        anim.SetBool("Reload", false);
        pistolReloadAudioSource.Stop();
        
        isReloading = false;
        
        if (isReloadingInFirstPerson && aimInput.action.ReadValue<float>() <= 0.1f)
        {
            StartCoroutine(Co_ExitFirstPersonAfterReload());
        }
        else
        {
            isReloadingInFirstPerson = false;
        }
        
        reloadCoroutine = null;
    }
    
    /// <summary>
    /// Called by animation event at the END of reload animation
    /// </summary>
    public void OnReloadAnimationEnd()
    {
        isReloading = false;
        
        // If reload finished in first-person and player released aim early, exit aiming after slight delay
        if (isReloadingInFirstPerson && aimInput.action.ReadValue<float>() <= 0.1f)
        {
            StartCoroutine(Co_ExitFirstPersonAfterReload());
        }
        else
        {
            isReloadingInFirstPerson = false; // Clear the flag immediately if not exiting
        }
    }
    
    IEnumerator Co_ExitFirstPersonAfterReload()
    {
        // Small delay to let first-person animation settle before switching
        yield return new WaitForSeconds(0.2f);
        
        isAiming = false;
        aimCam.Priority = 0;
        
        //Reset crosshair
        if (aimCrosshairImage != null)
        {
            aimCrosshairImage.DOKill();
            Color aimColor = aimCrosshairImage.color;
            aimColor.a = 1f;
            aimCrosshairImage.color = aimColor;
            aimCrosshairImage.DOFade(0f, 0.15f).SetEase(Ease.Linear);
        }
        
        firstPersonAnimator.SetBool("Reload", false);
        
        if (enableFPRendererCR != null) StopCoroutine(enableFPRendererCR);
        if (enableTPRendererCR != null) StopCoroutine(enableTPRendererCR);
        enableTPRendererCR = StartCoroutine(Co_EnableTPRenderer());
        
        isReloadingInFirstPerson = false; // Clear the flag
    }

    //Reset Attack after reset delay
    IEnumerator Co_ResetAttack(float resetDelay)
    {
        yield return new WaitForSeconds(resetDelay);
        player.isAttacking = false;
    }

    void ThirdPersonShoot()
    {
        // Block shooting if reloading
        if (isReloading)
            return;
            
        if (!infiniteAmmo)
        {
            currentAmmo--;
            if (currentAmmo <= 0) currentAmmo = 0;
            UpdateAmmoCount();
        }

        //Play Shoot SFX
        SoundEffectManager.instance.PlaySFX(ShootClip, shootClipVolume, true);

                    // Alert nearby enemies to the gunshot sound
                    AlertEnemiesInRadius();

        //Shooting in Third Person View
        muzzleFlashTP.Play();
        if (ejectCartridge) cartridgeEjectionTP.Play();
        if (shotKick > 0f && cameraShaker != null) cameraShaker.ApplyShake(shotKick * 0.5f, shakeDuration);

        float currentBulletSpread = bulletSpread;
        if (!playerStability.calmingInhalerIsActive)
        {
            foreach (BulletSpreadThreshold bulletSpreadThreshold in bulletSpreadThresholds)
            {
                if (playerStability.stability >= bulletSpreadThreshold.minStability && playerStability.stability < bulletSpreadThreshold.maxStability)
                {
                    currentBulletSpread *= bulletSpreadThreshold.bulletSpreadMultiplier;
                    break;
                }
            }
        }

        Vector3 rayOrigin = gunEndTP.position;
        if (pelletsPerShot > 1)
        {
            FirePellets(rayOrigin, gunEndTP, currentBulletSpread);
        }
        else
        {
            RaycastHit [] hits = Physics.RaycastAll(rayOrigin, ApplyBulletSpread(gunEndTP, currentBulletSpread), weaponRange, hitLayer, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                HandleHits(hits);
            }
        }

        player.isAttacking = true;
        StartCoroutine(Co_ResetAttack(0.35f));
    }

    DamageInfo[] RearrangeHitsBasedOnDistance(DamageInfo[] damageInfo)
    {
        List<DamageInfo> newHits = new List<DamageInfo>(damageInfo);
        newHits.Sort((a, b) => a.hit.distance.CompareTo(b.hit.distance));
        return newHits.ToArray();
    }

    public struct DamageInfo
    {
        public RaycastHit hit;
        public Hitbox hitbox;
        public Enemy enemy;
        public bool isHittingEnemy;
        public bool isHittingWeakpoint;

        public DamageInfo(RaycastHit hit, Hitbox hitbox = null, Enemy enemy = null, bool isHittingWeakpoint = false)
        {
            this.hit = hit;
            this.hitbox = hitbox;
            this.enemy = enemy;
            this.isHittingEnemy = enemy != null;
            this.isHittingWeakpoint = isHittingWeakpoint;
        }
    }

    void HandleHits(RaycastHit[] hits)
    {
        if (hits == null || hits.Length == 0)
            return;

        Dictionary<Enemy, DamageInfo> enemyHitMap = new Dictionary<Enemy, DamageInfo>();
        List<DamageInfo> worldHits = new List<DamageInfo>(); // non-enemy hits (walls, props, etc.)

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();

            // World hit
            if (enemy == null)
            {
                worldHits.Add(new DamageInfo(hit));
                continue;
            }

            bool isWeakpoint = hitbox != null && hitbox.damageType == DamageType.Weakpoint;
            DamageInfo newInfo = new DamageInfo(hit, hitbox, enemy, isWeakpoint);

            // First hit on this enemy
            if (!enemyHitMap.ContainsKey(enemy))
            {
                enemyHitMap.Add(enemy, newInfo);
            }
            else
            {
                // Override normal hit with weakpoint hit
                if (isWeakpoint && !enemyHitMap[enemy].isHittingWeakpoint)
                {
                    enemyHitMap[enemy] = newInfo;
                }
            }
        }

        // Final results
        List<DamageInfo> finalHits = new List<DamageInfo>();
        finalHits.AddRange(enemyHitMap.Values);
        finalHits.AddRange(worldHits);

        DamageInfo[] arrangedBasedOnDistance = RearrangeHitsBasedOnDistance(finalHits.ToArray());
        StartCoroutine(Co_HandleHits(arrangedBasedOnDistance)); ;
    }

    IEnumerator Co_HandleHits(DamageInfo[] damageInfo)
    {
        int hitCount = 0;
        List<Enemy> damagedEnemies = new List<Enemy>();

        bool hitProjectile = false;

        // Piercing "super shot": once the bullet has scored a weakpoint hit, every enemy
        // further along its path also counts as weakpoint-hit, even if the bullet just clips
        // their body rather than their own weakpoint.
        bool pierceWeakpointActive = false;

        for (int i = 0; i < damageInfo.Length; i++)
        {
            if (hitCount >= maxBulletPierceCount)
                yield break;

            if (damageInfo[i].hit.collider == null)
                continue;

            //Delay BETWEEN pierce hits
            if (hitCount > 0 && damageInfo[i].isHittingEnemy && !damagedEnemies.Contains(damageInfo[i].enemy))
            {
                yield return new WaitForSeconds(bulletPierceDelay);
            }

            //Debug
            Debug.DrawLine(damageInfo[i].hit.point, damageInfo[i].hit.point + damageInfo[i].hit.normal * 0.5f, Color.blue, 3f);

            //ENVIRONMENT HIT â€” bullet stops
            if (!damageInfo[i].isHittingEnemy)
            {
                //Damage Lock if hit - check FIRST before projectile check
                Lock lockObject = damageInfo[i].hit.collider.GetComponentInParent<Lock>();

                // Puzzle locks (e.g. the combination lock) can't be shot open: treat them as ordinary scenery
                if (lockObject != null && !lockObject.IsShootable)
                    lockObject = null;

                if(lockObject != null)
                {
                    SpawnDecal(damageInfo[i].hit, metalHitEffect, false);
                    lockObject.LockDamaged();
                    yield break;
                }

                Projectile projectile = damageInfo[i].hit.collider.GetComponent<Projectile>();
                if (projectile != null)
                {
                    hitProjectile = true;

                    //Player Hits projectile
                    float currentDamage = damage * 4f;
                    projectile.DestoryProjectile(Mathf.RoundToInt(currentDamage));

                    continue;
                }
                if(lockObject != null)
                {
                    SpawnDecal(damageInfo[i].hit, metalHitEffect, false);
                    lockObject.LockDamaged();
                    yield break;
                }

                // Check if world object has a Health component (e.g. ShootableButton)
                Health worldHealth = damageInfo[i].hit.collider.GetComponentInParent<Health>();
                if (worldHealth != null)
                {
                    worldHealth.SetLastHitPoint(damageInfo[i].hit.point);
                    worldHealth.Damage(damage);
                    hitCount++;
                    yield break;
                }

                //If no lock, continue checking other hits instead of stopping immediately
                continue;
            }

            // ---------------- DAMAGE LOGIC ----------------
            if (damageInfo[i].isHittingEnemy && !damagedEnemies.Contains(damageInfo[i].enemy))
            {
                float currentDamage = damage * (damageInfo[i].hitbox != null ? damageInfo[i].hitbox.damageMultiplier : 1f);

                if (hitProjectile || pierceWeakpointActive)
                {
                    currentDamage = damage * 4;
                    damageInfo[i].isHittingWeakpoint = true;
                }

                DamageEnemy(damageInfo[i], currentDamage);
                damagedEnemies.Add(damageInfo[i].enemy);

                if (damageInfo[i].isHittingWeakpoint)
                    pierceWeakpointActive = true;
            }
        }

        yield return null;
    }

    /// <summary>Hit feedback (weakpoint hitstop + sounds / headshot / normal) and the damage itself, for one enemy hit.</summary>
    void DamageEnemy(DamageInfo info, float currentDamage)
    {
        if (info.isHittingWeakpoint)
        {
            //Weakpoint Damage
            if (HitstopManager.instance != null)
                HitstopManager.instance.FreezeTime(hitstopDuration);

            weakPointAudioSource.clip = weakPointHitSoundEffects[Random.Range(0, weakPointHitSoundEffects.Length)];
            weakPointAudioSource.Play();

            // Play bonus sound unaffected by hitstop
            if (weakPointBonusSounds != null && weakPointBonusSounds.Length > 0)
            {
                AudioClip randomClip = weakPointBonusSounds[Random.Range(0, weakPointBonusSounds.Length)];
                if (SoundEffectManager.instance != null)
                {
                    SoundEffectManager.instance.PlaySFX(randomClip, weakPointBonusVolume);
                }
                else
                {
                    GameObject tempAudio = new GameObject("TempWeakpointBonusAudio");
                    AudioSource bonusSource = tempAudio.AddComponent<AudioSource>();
                    bonusSource.clip = randomClip;
                    bonusSource.volume = weakPointBonusVolume;
                    bonusSource.spatialBlend = 0f;
                    bonusSource.ignoreListenerPause = true;
                    bonusSource.Play();
                    Destroy(tempAudio, randomClip.length + 0.1f);
                }
            }

            if (cameraShaker != null)
                cameraShaker.ApplyShake(shakeIntensity, shakeDuration);

            // Reset fire cooldown instantly on weakpoint hit
            nextFire = 0f;

            if(info.hitbox!= null && info.hitbox.damageType == DamageType.Weakpoint)
                Destroy(info.hitbox.gameObject);
        }
        else if (info.hitbox != null && info.hitbox.damageType == DamageType.Headshot)
        {
            //Headshot Damage
            //Play SFX
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.PlaySFXAtPosition(headshotSFX, info.hit.point, 0.8f);
            else
                AudioSource.PlayClipAtPoint(headshotSFX, info.hit.point, 0.8f);
        }
        else
        {
            //Normal Damage
            AudioClip normalHitClip = hitSFX[Random.Range(0, hitSFX.Length)];
            if (SoundEffectManager.instance != null)
                SoundEffectManager.instance.PlaySFXAtPosition(normalHitClip, info.hit.point, 0.8f);
            else
                AudioSource.PlayClipAtPoint(normalHitClip, info.hit.point, 0.8f);
        }

        info.enemy.health.isDamageByWeakpointHit = info.isHittingWeakpoint;
        info.enemy.health.SetLastHitPoint(info.hit.point);
        info.enemy.health.Damage(Mathf.RoundToInt(currentDamage));
    }

    /// <summary>
    /// A shotgun shot: pelletsPerShot rays in a cone round aim.forward, each stopping at the first thing it hits. Pellets
    /// are summed per target, so an enemy takes one hit (one sound, one damage number) worth all the pellets that struck
    /// it, with the strongest hitbox any of them found (weakpoint over headshot over body). Pellet damage falls off with
    /// distance; world hits kick up dust. Enemies are shoved back harder the more of the blast hit them.
    /// </summary>
    void FirePellets(Vector3 muzzle, Transform aim, float shakySpread)
    {
        Vector3 origin = muzzle - aim.forward * pelletTraceBack;
        float pelletDamage = damage / (float)pelletsPerShot;
        var enemies = new Dictionary<Enemy, DamageInfo>();
        var enemyDamage = new Dictionary<Enemy, float>();
        var worldTargets = new Dictionary<Health, RaycastHit>();
        var worldDamage = new Dictionary<Health, float>();
        bool lockHit = false;

        for (int p = 0; p < pelletsPerShot; p++)
        {
            Vector2 c = Random.insideUnitCircle * (pelletSpread + shakySpread);
            Vector3 dir = (aim.forward + aim.right * c.x + aim.up * c.y).normalized;
            if (!FirstHitNotPlayer(origin, dir, weaponRange + pelletTraceBack, out RaycastHit hit))
                continue;
            Debug.DrawLine(origin, hit.point, Color.yellow, 2f);

            float falloff = Mathf.Lerp(1f, pelletMinDamageFraction, Mathf.InverseLerp(pelletFalloffStart, weaponRange, hit.distance - pelletTraceBack));
            float d = pelletDamage * falloff;
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
                bool weak = hitbox != null && hitbox.damageType == DamageType.Weakpoint;
                d *= hitbox != null ? hitbox.damageMultiplier : 1f;
                var info = new DamageInfo(hit, hitbox, enemy, weak);
                if (enemies.TryGetValue(enemy, out DamageInfo best))
                {
                    // keep the strongest hitbox found on this enemy
                    int Rank(DamageInfo x) => x.isHittingWeakpoint ? 2 : (x.hitbox != null && x.hitbox.damageType == DamageType.Headshot ? 1 : 0);
                    if (Rank(info) > Rank(best)) enemies[enemy] = info;
                    enemyDamage[enemy] += d;
                }
                else
                {
                    enemies[enemy] = info;
                    enemyDamage[enemy] = d;
                }
                continue;
            }

            Lock lockObject = hit.collider.GetComponentInParent<Lock>();
            if (lockObject != null && lockObject.IsShootable)
            {
                if (!lockHit) { SpawnDecal(hit, metalHitEffect, false); lockObject.LockDamaged(); }
                lockHit = true;
                continue;
            }

            Health worldHealth = hit.collider.GetComponentInParent<Health>();
            if (worldHealth != null)
            {
                if (!worldTargets.ContainsKey(worldHealth)) { worldTargets[worldHealth] = hit; worldDamage[worldHealth] = 0f; }
                worldDamage[worldHealth] += d;
                continue;
            }

            if (sandHitEffect != null) SpawnDecal(hit, sandHitEffect, false);
        }

        foreach (var kv in enemies)
        {
            // the more of the blast landed, the harder it shoves him back
            kv.Key.pendingKnockbackScale = 1f + (pelletKnockback - 1f) * Mathf.Clamp01(enemyDamage[kv.Key] / damage);
            DamageEnemy(kv.Value, enemyDamage[kv.Key]);
            kv.Key.pendingKnockbackScale = 1f;      // enemy types that ignore it must not carry it to the next hit
        }
        foreach (var kv in worldTargets)
        {
            kv.Key.SetLastHitPoint(kv.Value.point);
            kv.Key.Damage(Mathf.Max(1, Mathf.RoundToInt(worldDamage[kv.Key])));
        }
    }

    // the nearest hit along the ray that is not the player themself (the pellet trace starts back at the player)
    bool FirstHitNotPlayer(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
    {
        best = default;
        float bestDistance = float.MaxValue;
        foreach (var h in Physics.RaycastAll(origin, dir, range, hitLayer, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= bestDistance || h.collider.transform.IsChildOf(transform)) continue;
            best = h; bestDistance = h.distance;
        }
        return bestDistance < float.MaxValue;
    }

    void SpawnDecal(RaycastHit hit, GameObject prefab, bool setParent = true)
    {
        if (hit.collider == null)
            return;

        GameObject spawnedDecal = GameObject.Instantiate(prefab, hit.point, Quaternion.LookRotation(hit.normal));
        if(setParent) spawnedDecal.transform.SetParent(hit.collider.transform);
    }

    public void AutoRotate()
    {
        if (!isAutoRotating)
        {
            targetYaw = _cinemachineTargetYaw + 180f;
            if (autoRotateCR != null) StopCoroutine(autoRotateCR);
            autoRotateCR = StartCoroutine(Co_SmoothAutoRotate());
        }
    }

    IEnumerator Co_SmoothAutoRotate()
    {
        isAutoRotating = true;
        float startYaw = _cinemachineTargetYaw;
        float elapsed = 0f;
        float duration = autoRotateDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _cinemachineTargetYaw = Mathf.LerpAngle(startYaw, targetYaw, t);

            // Smoothly update player rotation while rotating
            Quaternion rotation = Quaternion.Euler(transform.eulerAngles.x, _cinemachineTargetYaw, transform.eulerAngles.z);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * rotationSmoothness);

            yield return null;
        }

        _cinemachineTargetYaw = targetYaw;
        isAutoRotating = false;
    }

    public bool eyePeakAimingRotationEnabled = false;
    public float restrictYawAngleMin;
    public float restrictYawAngleMax;
    public float restrictPitchAngleMin;
    public float restrictPitchAngleMax;
    public float eyePeakRotationSensitivity= 20f;


    void CameraRotation()
    {
        if (eyePeakAimingRotationEnabled)
        {
            Vector2 moveInputValue = moveInput.action.ReadValue<Vector2>();

            // if there is an input and camera position is not fixed
            if (moveInputValue.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                _cinemachineTargetYaw += moveInputValue.x * Time.deltaTime * eyePeakRotationSensitivity;
                _cinemachineTargetPitch += -moveInputValue.y * Time.deltaTime * eyePeakRotationSensitivity;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, restrictYawAngleMin, restrictYawAngleMax);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, restrictPitchAngleMin, restrictPitchAngleMax);
        }
        else
        {

            Vector2 lookInputValue = lookInput.action.ReadValue<Vector2>();

            // if there is an input and camera position is not fixed
            if (lookInputValue.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                _cinemachineTargetYaw += lookInputValue.x * Time.deltaTime * Sensitivity;
                _cinemachineTargetPitch += lookInputValue.y * Time.deltaTime * Sensitivity;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
        }

        


        //Update player Yaw rotation
        if (!isAutoRotating)
        {
            Quaternion rotation = Quaternion.Euler(transform.eulerAngles.x, _cinemachineTargetYaw, transform.eulerAngles.z);
            transform.rotation = rotation;
        }

        // Cinemachine will follow this target
        CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, 0.0f, 0.0f);       
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void AlertEnemiesInRadius()
    {
        Vector3 soundOrigin = transform.position;
        Collider[] colliders = Physics.OverlapSphere(soundOrigin, gunSoundRadius);

        foreach (Collider col in colliders)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.AlertToSound(soundOrigin);
            }
        }
    }
}







