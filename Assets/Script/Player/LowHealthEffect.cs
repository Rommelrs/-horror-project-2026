using UnityEngine;
using UnityEngine.UI;

public class LowHealthEffect : MonoBehaviour
{
    public static bool isLowHealth = false;
    
    [SerializeField] Image[] vignetteOverlays;
    [SerializeField] Health playerHealth;
    [SerializeField] float healthThreshold = 50f;
    [SerializeField] float effectSmoothness = 5f;

    float targetWeight = 0f;
    float currentWeight = 0f;
    float currentVelocity = 0f;

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
        
        // Update all vignette overlays
        for (int i = 0; i < vignetteOverlays.Length; i++)
        {
            if (vignetteOverlays[i] != null)
            {
                Color c = vignetteOverlays[i].color;
                c.a = currentWeight;
                vignetteOverlays[i].color = c;
            }
        }
        
    }
}
