using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    public HealthBar staminaBar;
    public float staminaReplanishRate = 5f; // Amount of stamina restored per second
    public float staminaReplanishDelay = 1f; // Delay before stamina starts replenishing

    public AudioSource audioSource;
    public AudioClip slowBreathingSFX;
    public AudioClip fastBreathingSFX;
    public AnimationCurve volumeCurve;
    public float audioSmoothness = 5f;
    public float animationSmoothness = 5f;
    public float fastBreathingVolume = 0.6f;

    bool playingSlowBreathing;

    float stamina = 100f;
    public float Stamina
    {
        get { return stamina; }
        set
        {
            // Clamp the stamina value between 0 and 100
            stamina = Mathf.Clamp(value, 0f, 100f);
        }
    }
    float staminaLastUsedTime = 0f;

    bool prolongStaminaIsActive = false;
    float staminaDecreaseFactor;
    Coroutine prolongStaminaCR;

    private void Update()
    {
        //Replanish stamina if the player has not used it for a while
        if (Time.time - staminaLastUsedTime > staminaReplanishDelay)
        {
            RestoreStamina(staminaReplanishRate * Time.deltaTime);
        }

        if (stamina >= 80f || GameManager.IsPaused)
        {
            // Stop Breathing Sound
            playingSlowBreathing = false;
            audioSource.volume = Mathf.Lerp(audioSource.volume, 0f, audioSmoothness * Time.unscaledDeltaTime);

            if(audioSource.volume <= 0f)
                audioSource.Stop();

            //Reset Heavy Breathing animation
            Player.instance.animator.SetLayerWeight(4, Mathf.Lerp(Player.instance.animator.GetLayerWeight(4), 0f, animationSmoothness * Time.unscaledDeltaTime));
        }
        else
        {
            //Play Breathing Sound
            if (stamina <= 1f)
            {
                //Play Heavy Breathing
                if (playingSlowBreathing)
                {
                    playingSlowBreathing = false;

                    audioSource.clip = fastBreathingSFX;
                    audioSource.loop = true;
                    audioSource.Play();
                }

                //Set Volume
                audioSource.volume = Mathf.Lerp(audioSource.volume, fastBreathingVolume, audioSmoothness * Time.unscaledDeltaTime);

                //Reset Heavy Breathing animation
                Player.instance.animator.SetLayerWeight(4, Mathf.Lerp(Player.instance.animator.GetLayerWeight(4), 1f, animationSmoothness * Time.unscaledDeltaTime));

            }
            else
            {
                //Start Slow Breathing
                if (!playingSlowBreathing)
                {
                    playingSlowBreathing = true;

                    audioSource.clip = slowBreathingSFX;
                    audioSource.loop = true;
                    audioSource.Play();
                }

                //Set Volume
                float value = volumeCurve.Evaluate((float)stamina / 100f);
                audioSource.volume = Mathf.Lerp(audioSource.volume, value, audioSmoothness * Time.unscaledDeltaTime);

                //Reset Heavy Breathing animation
                Player.instance.animator.SetLayerWeight(4, Mathf.Lerp(Player.instance.animator.GetLayerWeight(4), 0f, animationSmoothness * Time.unscaledDeltaTime));
            }
        }
    }

    // Decrease stamina by the amount
    public void UseStamina(float amount)
    {
        if (prolongStaminaIsActive)
        {
            float newDecreaseAmount = amount - (staminaDecreaseFactor / 100f * amount);
            Stamina -= newDecreaseAmount;
        }
        else
        {
            Stamina -= amount;
        }

      
        staminaLastUsedTime = Time.time;
        staminaBar.SetHealth(Mathf.RoundToInt(Stamina), 100, true);
    }

    // Restore stamina by the amount
    void RestoreStamina(float amount)
    {
        Stamina += amount;
        staminaBar.SetHealth(Mathf.RoundToInt(Stamina), 100, false);
    }

    public void ProlongStamina(float duration, float staminaDecreaseFactorInPercentage = 80f)
    {
        staminaDecreaseFactor = staminaDecreaseFactorInPercentage;
        if (prolongStaminaCR != null) StopCoroutine(prolongStaminaCR);
        prolongStaminaCR = StartCoroutine(Co_ProlongStamina(duration));
    }

    IEnumerator Co_ProlongStamina(float duration)
    {
        prolongStaminaIsActive = true;
        yield return new WaitForSeconds(duration);
        prolongStaminaIsActive = false;
    }
}
