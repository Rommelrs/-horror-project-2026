using UnityEngine;
using UnityEngine.UI;

public class LowHealthEffect : MonoBehaviour
{
    public static bool isLowHealth = false;

    [SerializeField] Image[] vignetteOverlays;
    [SerializeField] Health playerHealth;
    [SerializeField] float healthThreshold = 50f;
    [SerializeField] float effectSmoothness = 5f;
    [Tooltip("Multiplies the vignette's alpha (1 = full only at 0 health).")]
    [SerializeField] float strength = 1.3f;

    [Header("Edges")]
    [Tooltip("At the first sign of damage the overlay is scaled up this much, pushing the blood band off the edges; it closes in to 1 as health runs out.")]
    [SerializeField] float startScale = 1.12f;

    [Header("Heartbeat")]
    [Tooltip("The blood vignette throbs like a heartbeat, faster the lower the health.")]
    [SerializeField] bool heartbeat = false;
    [SerializeField] float minBpm = 60f;
    [SerializeField] float maxBpm = 115f;
    [Tooltip("How much of the vignette's alpha each beat adds / takes away.")]
    [SerializeField] float beatStrength = 0.25f;
    [Tooltip("The overlay swells this much on a beat (1 = no swell).")]
    [SerializeField] float beatScale = 1.04f;

    [Header("Hit flash")]
    [Tooltip("Extra alpha when hit while already hurt.")]
    [SerializeField] float hitFlash = 0.35f;
    [SerializeField] float hitFlashFade = 0.6f;

    [Header("Healing items reminder")]
    [Tooltip("Dropping below this health slides in the healing items you have (HealingItemsHUD). 0 = off.")]
    [SerializeField] float remindBelow = 35f;

    float targetWeight = 0f;
    float currentWeight = 0f;
    float currentVelocity = 0f;
    float beatPhase;
    float flash;
    bool reminded;
    Health hookedHealth;

    private void OnDisable()
    {
        Unhook();
    }

    void Hook()
    {
        if (hookedHealth == playerHealth) return;
        Unhook();
        hookedHealth = playerHealth;
        if (hookedHealth != null) hookedHealth.OnDamageTaken += OnDamageTaken;
    }

    void Unhook()
    {
        if (hookedHealth != null) hookedHealth.OnDamageTaken -= OnDamageTaken;
        hookedHealth = null;
    }

    void OnDamageTaken(int damage)
    {
        if (playerHealth != null && playerHealth.GetHealthValue() < healthThreshold)
            flash = 1f;
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("LowHealthEffect: playerHealth is not assigned!");
            return;
        }

        if (vignetteOverlays == null || vignetteOverlays.Length == 0)
        {
            Debug.LogWarning("LowHealthEffect: vignetteOverlays is not assigned!");
            return;
        }

        Hook();

        // Calculate target weight based on health
        float currentHealth = playerHealth.GetHealthValue();

        if (currentHealth <= 0)
        {
            targetWeight = 0f;
        }
        else if (currentHealth < healthThreshold)
        {
            // Effect gets stronger as health gets lower
            // At healthThreshold, weight = 0. At 0 health, weight = 1
            targetWeight = 1f - (currentHealth / healthThreshold);
        }
        else
        {
            targetWeight = 0f;
        }

        // Smooth the transition
        if (Mathf.Abs(currentWeight - targetWeight) > 0.001f)
        {
            currentWeight = Mathf.SmoothDamp(currentWeight, targetWeight, ref currentVelocity, effectSmoothness);
            currentWeight = Mathf.Clamp01(currentWeight);
        }
        else
        {
            currentWeight = targetWeight;
        }

        // Update static flag
        isLowHealth = currentWeight > 0.1f;

        // Heartbeat: a double thump (lub-dub) per beat, quicker as health drops
        float beat = 0f;
        if (heartbeat && currentWeight > 0f && !GameManager.IsPaused)
        {
            float bpm = Mathf.Lerp(minBpm, maxBpm, targetWeight);
            beatPhase = Mathf.Repeat(beatPhase + Time.unscaledDeltaTime * bpm / 60f, 1f);
            beat = Thump(beatPhase, 0f) + 0.6f * Thump(beatPhase, 0.22f);
        }

        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, hitFlashFade));

        float alpha = currentWeight > 0f
            ? Mathf.Clamp01(currentWeight * strength * (1f - beatStrength * 0.5f + beatStrength * beat) + flash * hitFlash)
            : 0f;
        // the band hugs the screen edges and closes in as health drops (plus the optional beat swell)
        float scale = Mathf.Lerp(startScale, 1f, currentWeight) + (beatScale - 1f) * beat * currentWeight;

        // Remind the player what they can heal with, once per drop
        if (remindBelow > 0f)
        {
            if (!reminded && currentHealth > 0 && currentHealth < remindBelow)
            {
                reminded = true;
                if (HealingItemsHUD.instance != null)
                    HealingItemsHUD.instance.ShowReminder();
            }
            else if (currentHealth >= remindBelow)
                reminded = false;
        }

        // Update all vignette overlays
        for (int i = 0; i < vignetteOverlays.Length; i++)
        {
            if (vignetteOverlays[i] != null)
            {
                Color c = vignetteOverlays[i].color;
                c.a = alpha;
                vignetteOverlays[i].color = c;
                vignetteOverlays[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

    }

    // one sharp rise and slower fall starting at 'start' in the beat
    static float Thump(float phase, float start)
    {
        float t = (phase - start) / 0.18f;
        if (t < 0f || t > 1f) return 0f;
        return t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
    }
}
