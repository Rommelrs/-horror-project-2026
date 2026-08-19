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

    public CinemachineVirtualCamera aimCam;
    public bool isAiming = false;
    [SerializeField] Image aimCrosshairImage;

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private const float _threshold = 0.01f;
    public bool LockCameraPosition = false;
    public bool aimingOverrideForTesting = false;

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

    [Header("Weapon Spread")]
    [Tooltip("How inaccurate the weapon is. 0 = perfect accuracy.")]
    public float bulletSpread = 0.02f;
    public BulletSpreadThreshold []bulletSpreadThresholds;

    [Header("Bullet Pierce")]
    public int maxBulletPierceCount = 1;
    public float bulletPierceDelay = 0.35f;

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

        HandleAiming();
        HandleReloading();   // Process reload first to set isReloading flag
        HandleShooting();    // Then check shooting (which checks !isReloading)
    }

    bool HasEnoughAmmo()
    {
        if(currentAmmo > 0)
            return true;

        return false;
    }

    void UpdateAmmoCount()
    {
        if(ammoTxt != null)
            ammoTxt.text = currentAmmo.ToString();
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
                    currentAmmo--;
                    if (currentAmmo <= 0) currentAmmo = 0;
                    UpdateAmmoCount();

                    //Trigger Shoot Animation
                    firstPersonAnimRoot.SetTrigger("Shoot");

                    ////Play SFX
                    //audioSource.PlayOneShot(pistolShootClip);

                    //Play Shoot SFX
                    SoundEffectManager.instance.PlaySFX(pistolShootClip, 0.8f, true);

                    // Alert nearby enemies to the gunshot sound
                    AlertEnemiesInRadius();

                    //Shooting in First Person View
                    muzzleFlash.Play();
                    cartridgeEjection.Play();

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

                    RaycastHit []hits = Physics.RaycastAll(rayOrigin, gunEnd.forward, weaponRange, hitLayer, QueryTriggerInteraction.Ignore);
                    if (hits != null && hits.Length > 0)
                    {
                        HandleHits(hits);
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
                    reloadCoroutine = StartCoroutine(Co_ResetReloadTrigger(firstPersonAnimator, processesReloadTime));
                }
                else
                {
                    //Third Person Reload
                    isReloadingInFirstPerson = false; // Not in first-person
                    float reloadTimeMultiplier = 1.0f / (float)reloadDurationMultiplier;

                    animator.SetFloat("ReloadTimeMultiplier", reloadTimeMultiplier);
                    animator.SetBool("Reload", true);
                    
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
        // Wait for the exact calculated reload duration (base duration * stability multiplier)
        yield return new WaitForSeconds(duration);
        
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
            
        currentAmmo--;
        if (currentAmmo <= 0) currentAmmo = 0;
        UpdateAmmoCount();

        //Play Shoot SFX
        SoundEffectManager.instance.PlaySFX(pistolShootClip, 0.8f, true);

                    // Alert nearby enemies to the gunshot sound
                    AlertEnemiesInRadius();

        //Shooting in Third Person View
        muzzleFlashTP.Play();
        cartridgeEjectionTP.Play();

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
        RaycastHit [] hits = Physics.RaycastAll(rayOrigin, ApplyBulletSpread(gunEndTP, currentBulletSpread), weaponRange, hitLayer, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            HandleHits(hits);
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

                if (hitProjectile)
                {
                    currentDamage = damage * 4;
                    damageInfo[i].isHittingWeakpoint = true;
                }

                if ((damageInfo[i].hitbox != null && damageInfo[i].hitbox.damageType == DamageType.Weakpoint) || hitProjectile)
                {
                    //Weakpoint Damage
                    if (HitstopManager.instance != null)
                        HitstopManager.instance.FreezeTime(hitstopDuration);

                    weakPointAudioSource.clip = weakPointHitSoundEffects[Random.Range(0, weakPointHitSoundEffects.Length)];
                    weakPointAudioSource.Play();

                    // Play bonus sound unaffected by hitstop
                    if (weakPointBonusSounds != null && weakPointBonusSounds.Length > 0)
                    {
                        GameObject tempAudio = new GameObject("TempWeakpointBonusAudio");
                        AudioSource bonusSource = tempAudio.AddComponent<AudioSource>();
                        AudioClip randomClip = weakPointBonusSounds[Random.Range(0, weakPointBonusSounds.Length)];
                        bonusSource.clip = randomClip;
                        bonusSource.volume = 5f;
                        bonusSource.spatialBlend = 0f;
                        bonusSource.ignoreListenerPause = true;
                        bonusSource.Play();
                        Destroy(tempAudio, randomClip.length + 0.1f);
                    }

                    if (cameraShaker != null)
                        cameraShaker.ApplyShake(shakeIntensity, shakeDuration);

                    // Reset fire cooldown instantly on weakpoint hit
                    nextFire = 0f;

                    if(damageInfo[i].hitbox!= null && damageInfo[i].hitbox.damageType == DamageType.Weakpoint)
                        Destroy(damageInfo[i].hitbox.gameObject);
                }
                else if (damageInfo[i].hitbox != null && damageInfo[i].hitbox.damageType == DamageType.Headshot)
                {
                    //Headshot Damage
                    //Play SFX
                    AudioSource.PlayClipAtPoint(headshotSFX, damageInfo[i].hit.point, 0.8f);
                }
                else
                {
                    //Normal Damage
                    AudioSource.PlayClipAtPoint(
                        hitSFX[Random.Range(0, hitSFX.Length)],
                        damageInfo[i].hit.point,
                        0.8f);
                }

                damagedEnemies.Add(damageInfo[i].enemy);
                damageInfo[i].enemy.health.isDamageByWeakpointHit = damageInfo[i].isHittingWeakpoint;
                damageInfo[i].enemy.health.SetLastHitPoint(damageInfo[i].hit.point);
                damageInfo[i].enemy.health.Damage(Mathf.RoundToInt(currentDamage));
            }
        }

        yield return null;
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







