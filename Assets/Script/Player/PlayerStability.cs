using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStability : MonoBehaviour
{
    [SerializeField] UnityEngine.Rendering.PostProcessing.PostProcessVolume volume;
    [SerializeField] Image whiteVignetteOverlay;
    [SerializeField] float maxVignetteAlpha = 1f;
    [SerializeField] float postProcessingSmoothness = 5;
    [SerializeField] Image stabilityFill;
    [SerializeField] TMP_Text stabilityTxt;
    [SerializeField] float stabilityUpdateFrequency = 0.5f;
    [SerializeField] GameObject distortedAudioRevertZoneObj;

    public int stability = 100;
    public int maxStability = 100;

    public float targetPostIntensity = 0;
    public float currentWeightValue = 0f;
    private float currentVelocity = 0f;

    public bool calmingInhalerIsActive = false;
    Coroutine calmingInhalerCR;

    float nextUpdateTime;

    private void Update()
    {
        float processedStabilityValue = (float)stability;
        if (processedStabilityValue <= 25f) processedStabilityValue = 0f;

        //Update Stability UI
        if(stabilityFill != null)
            stabilityFill.fillAmount = 1f - (processedStabilityValue / (float)maxStability);

        //Update Stability Text
        if (stabilityTxt != null && Time.time > nextUpdateTime)
        {
            nextUpdateTime = Time.time + stabilityUpdateFrequency;
            int processedStabilityAmount = stability;

            int ran = Random.Range(-2, 3);
            processedStabilityAmount += ran;

            processedStabilityAmount = Mathf.Clamp(processedStabilityAmount, 0, 100);
            stabilityTxt.text = processedStabilityAmount.ToString() + "%";
        }

        if (calmingInhalerIsActive)
            processedStabilityValue = 100;

        //Update Post Processing Weight
        targetPostIntensity = 1.0f - ((float)processedStabilityValue / (float)maxStability);

        if (Mathf.Abs(currentWeightValue - targetPostIntensity) > 0.001f)
        {
            currentWeightValue = Mathf.SmoothDamp(currentWeightValue, targetPostIntensity, ref currentVelocity, postProcessingSmoothness);
            currentWeightValue = Mathf.Clamp01(currentWeightValue);
        }
        else
            currentWeightValue = targetPostIntensity;

        //Update Volume weight
        if (volume != null)
            volume.weight = currentWeightValue;

        //Update white vignette overlay alpha (hide if low health red vignette is active)
        if (whiteVignetteOverlay != null)
        {
            Color c = whiteVignetteOverlay.color;
            
            float targetAlpha = LowHealthEffect.isLowHealth ? 0f : Mathf.Min(currentWeightValue, maxVignetteAlpha);
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * postProcessingSmoothness);
            
            whiteVignetteOverlay.color = c;
        }
        else
        {
            Debug.LogWarning("PlayerStability: whiteVignetteOverlay is not assigned!");
        }

        if (processedStabilityValue <= 25)
        {
            //Turn on Distorted Audio
            if (!distortedAudioRevertZoneObj.activeSelf)
                distortedAudioRevertZoneObj.SetActive(true);
        }
        else
        {
            //Turn off Distorted Audio
            if (distortedAudioRevertZoneObj.activeSelf)
                distortedAudioRevertZoneObj.SetActive(false);
        }
    }

    //Increase Stability Value
    public void IncreaseStability(int increaseValue)
    {
        stability += increaseValue;
        if(stability > maxStability ) stability = maxStability;
    }

    //Decrease Stability Value
    public void DecreaseStability(int decreaseValue) 
    {
        stability -= decreaseValue;
        if (stability < 0) stability = 0;
    }

    //Activate Calming Inhaler Item Powerup
    public void ActivateCalmingInhaler(float duration)
    {
        if (calmingInhalerCR != null) StopCoroutine(calmingInhalerCR);
        calmingInhalerCR = StartCoroutine(Co_ActivateCalmingInhaler(duration));
    }

    IEnumerator Co_ActivateCalmingInhaler(float duration)
    {
        calmingInhalerIsActive = true;
        yield return new WaitForSeconds(duration);
        calmingInhalerIsActive = false;
    }
}
