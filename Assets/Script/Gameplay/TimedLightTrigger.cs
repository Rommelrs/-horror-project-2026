using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TimedLightTrigger : MonoBehaviour
{
    [Header("Lights To Turn Off After Duration")]
    [Tooltip("These lights will be turned OFF after the player has been in the area for the duration.")]
    [SerializeField] GameObject[] lightsToTurnOff;

    [Header("Settings")]
    [Tooltip("How many seconds after the player enters before lights turn off.")]
    [SerializeField] float duration = 3f;
    [Tooltip("If true, timer resets when player leaves the trigger area.")]
    [SerializeField] bool resetOnExit = true;

    [Header("Events")]
    public UnityEvent OnLightsOff;

    bool playerInside = false;
    Coroutine timerCR;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;
        if (timerCR != null) StopCoroutine(timerCR);
        timerCR = StartCoroutine(Co_Countdown());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = false;
        if (resetOnExit && timerCR != null)
        {
            StopCoroutine(timerCR);
            timerCR = null;
        }
    }

    IEnumerator Co_Countdown()
    {
        yield return new WaitForSeconds(duration);

        if (!playerInside && resetOnExit)
        {
            timerCR = null;
            yield break;
        }

        // Turn lights off
        foreach (var obj in lightsToTurnOff)
            if (obj != null) obj.SetActive(false);

        OnLightsOff?.Invoke();
        timerCR = null;
    }
}
