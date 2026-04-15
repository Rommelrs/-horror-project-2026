using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitstopManager : MonoBehaviour
{
    public static HitstopManager instance;

    Coroutine freezeTimeCR;
    bool busy = false;

    private void Awake()
    {
        instance = this;
    }

    public void FreezeTime(float duration)
    {
        if (DebugManager.instance != null && !DebugManager.instance.hitstopIsEnabled)
            return;

        if (busy) return;

        busy = true;
        if (freezeTimeCR != null) StopCoroutine(freezeTimeCR);
        freezeTimeCR = StartCoroutine(Co_FreezeTime(duration));
    }

    IEnumerator Co_FreezeTime(float duration)
    {
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1;
        busy = false;
    }
}
