using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A bear trap (the bag bearer's lesson, user 2026-11-25): stepping on it snaps it round Khalid's leg. He can't walk, only turn and use
/// what is in his hands. Hold Interact to pry it open; any hit he takes while he is at it and he has to start again - so the bags the
/// bag bearer throws have to be struck down with the hammer first.
/// </summary>
public class BearTrap : MonoBehaviour
{
    [SerializeField] Transform jawA, jawB;
    [SerializeField] float openAngle = 0f, shutAngle = 78f;
    [Tooltip("Holding the key this long opens it (s).")]
    [SerializeField] float pryTime = 3.6f;
    [Tooltip("Let go of the key: the progress goes back at this rate (per s).")]
    [SerializeField] float slipBack = 0.25f;
    [SerializeField] int damage = 10;
    [SerializeField] AudioClip snapClip, pryClip, openClip;
    [Tooltip("Switched on when it snaps (the bag bearer in front of it). He stays where he is.")]
    [SerializeField] GameObject enemy;
    [Tooltip("The hammer prompts stay until this many bags have been struck.")]
    [SerializeField] int bagsToLearn = 2;

    static BearTrap holder;
    /// <summary>Khalid's leg is in one.</summary>
    public static bool HoldsPlayer => holder != null;

    enum State { Set, Shut, Open }
    State state;
    float progress;
    int hitsAtSnap;
    Health health;
    InteractPromptUI pry, raise, strike;
    int shown = -1;
    CanvasGroup bar;
    RectTransform fill;
    Image fillImg;
    float flash;
    AudioSource sfx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { holder = null; }

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0.8f; sfx.minDistance = 2f; sfx.maxDistance = 25f; sfx.rolloffMode = AudioRolloffMode.Linear;
        var main = SoundEffectManager.instance != null ? SoundEffectManager.instance.GetComponent<AudioSource>() : null;
        if (main != null) sfx.outputAudioMixerGroup = main.outputAudioMixerGroup;
        Pose(openAngle);
    }

    void Pose(float a)
    {
        if (jawA != null) jawA.localRotation = Quaternion.Euler(-a, 0f, 0f);
        if (jawB != null) jawB.localRotation = Quaternion.Euler(a, 0f, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (state != State.Set || !other.CompareTag("Player") || holder != null) return;
        var p = Player.instance;
        if (p == null || p.IsDead()) return;
        StartCoroutine(Co_Snap(p));
    }

    IEnumerator Co_Snap(Player p)
    {
        state = State.Shut;
        holder = this;
        // his leg is in the middle of it
        Vector3 at = transform.position; at.y = p.transform.position.y;
        if (p.controller != null) p.controller.Move(at - p.transform.position);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.06f) { Pose(Mathf.Lerp(openAngle, shutAngle, t)); yield return null; }
        Pose(shutAngle);
        if (snapClip != null) sfx.PlayOneShot(snapClip);
        health = p.health;
        if (health != null) { health.Damage(damage); health.OnDamageTaken += OnHit; }
        if (enemy != null)
        {
            enemy.SetActive(true);
            var bb = enemy.GetComponent<BagBearerEnemy>();
            if (bb != null) { yield return null; bb.SetStandStill(true); bb.ChangeEnemyType(EnemyType.Aggressive); }
        }
        hitsAtSnap = PlayerMeleeWeapon.instance != null ? PlayerMeleeWeapon.instance.ProjectileHits : 0;
        pry = PromptCopy.Make("Hold ", "InteractPromptUI", " to pry the trap open");
        raise = PromptCopy.Make("Hold ", "AimPromptUI", " to raise the hammer", 110f);
        strike = PromptCopy.Make("Press ", "ShootPromptUI", " to strike the bags", 110f);
        if (pry != null) pry.ShowPrompt();
        MakeBar();
    }

    void OnHit(int amount)
    {
        if (state != State.Shut) return;
        if (progress > 0.02f) flash = 0.5f;
        progress = 0f;
    }

    void Update()
    {
        if (state != State.Shut || holder != this) return;
        var p = Player.instance;
        if (p == null || p.IsDead()) { Release(false); return; }
        if (GameManager.IsPaused) return;

        bool busy = PlayerMeleeWeapon.Attacking || PlayerMeleeWeapon.Ready || (p.playerWeaponSystem != null && p.playerWeaponSystem.isAiming) || p.isGrabbed;
        var a = InteractionHandler.InteractAction;
        bool prying = !busy && a != null && a.IsPressed();
        if (prying)
        {
            progress += Time.deltaTime / Mathf.Max(0.2f, pryTime);
            if (pryClip != null && !sfx.isPlaying) sfx.PlayOneShot(pryClip, 0.35f);
        }
        else progress = Mathf.Max(0f, progress - slipBack * Time.deltaTime);
        // the jaws give a little with it
        Pose(Mathf.Lerp(shutAngle, shutAngle * 0.45f, progress) + (prying ? Mathf.Sin(Time.time * 38f) * 1.2f : 0f));

        flash = Mathf.Max(0f, flash - Time.deltaTime);
        if (bar != null)
        {
            bar.alpha = Mathf.MoveTowards(bar.alpha, progress > 0.001f || prying || flash > 0f ? 1f : 0f, Time.deltaTime * 6f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            fillImg.color = flash > 0f ? new Color(0.75f, 0.08f, 0.05f) : new Color(0.86f, 0.55f, 0.2f);
        }
        UpdateHammerPrompts();
        if (progress >= 1f) Release(true);
    }

    void UpdateHammerPrompts()
    {
        int hits = PlayerMeleeWeapon.instance != null ? PlayerMeleeWeapon.instance.ProjectileHits - hitsAtSnap : 99;
        int want = !PlayerMeleeWeapon.Equipped || hits >= bagsToLearn || enemy == null || !enemy.activeInHierarchy ? 0 : PlayerMeleeWeapon.Ready ? 2 : 1;
        if (want == shown) return;
        shown = want;
        if (raise != null) { if (want == 1) raise.ShowPrompt(); else raise.HidePrompt(); }
        if (strike != null) { if (want == 2) strike.ShowPrompt(); else strike.HidePrompt(); }
    }

    void Release(bool opened)
    {
        state = State.Open;
        if (holder == this) holder = null;
        if (health != null) health.OnDamageTaken -= OnHit;
        if (enemy != null) { var bb = enemy.GetComponent<BagBearerEnemy>(); if (bb != null) bb.SetStandStill(false); }
        if (opened && openClip != null) sfx.PlayOneShot(openClip);
        Pose(6f);
        if (pry != null) pry.HidePrompt();
        if (raise != null) raise.HidePrompt();
        if (strike != null) strike.HidePrompt();
        if (bar != null) Destroy(bar.gameObject);
        foreach (var c in GetComponents<Collider>()) c.enabled = false;
    }

    void OnDisable() { if (holder == this) Release(false); }

    void MakeBar()
    {
        var go = new GameObject("Bear Trap Bar", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 880;
        var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
        bar = go.AddComponent<CanvasGroup>(); bar.alpha = 0f; bar.blocksRaycasts = false;
        Image Box(Transform parent, Vector2 size, Color c)
        {
            var rt = new GameObject("Box", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.anchoredPosition = new Vector2(0f, 178f); rt.sizeDelta = size;
            var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false;
            return im;
        }
        Box(go.transform, new Vector2(464f, 22f), new Color(0.78f, 0.7f, 0.55f, 0.55f));
        var track = Box(go.transform, new Vector2(460f, 18f), new Color(0.05f, 0.04f, 0.03f, 0.85f));
        fillImg = Box(track.transform, Vector2.zero, new Color(0.86f, 0.55f, 0.2f));
        fill = fillImg.rectTransform;
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
    }
}
