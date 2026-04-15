using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerMovementLimiter : MonoBehaviour
{
    public bool movementLimitActive = false;
    public float limitMoveSpeed = 1f;
    public float defaultSlowDuration = 5f;
    [SerializeField] CanvasGroup []mudGUIObjects;

    Sequence mudSequence;
    Coroutine limitMovementCR;

    public void LimitMovement(float duration = -1f)
    {
        if (duration < 0) duration = defaultSlowDuration;
        if (limitMovementCR != null) StopCoroutine(limitMovementCR);
        limitMovementCR = StartCoroutine(Co_LimitMovement(duration));
    }

    IEnumerator Co_LimitMovement(float duration)
    {
        movementLimitActive = true;

        if (mudSequence != null)
            mudSequence.Kill();

        //Active Mud GUI
        int mudToActivate = Random.Range(0, mudGUIObjects.Length);
        for (int i = 0; i < mudGUIObjects.Length; i++)
        {
            if (i == mudToActivate)
            {
                mudGUIObjects[i].gameObject.SetActive(true);

                mudGUIObjects[i].alpha = 0f;

                mudSequence.Append(mudGUIObjects[i].DOFade(1f, 0.2f).SetEase(Ease.InSine));
                mudSequence.ForceInit();
            }
            else
                mudGUIObjects[i].gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(duration);

        movementLimitActive = false;

        if (mudSequence != null)
            mudSequence.Kill();

        for (int i = 0; i < mudGUIObjects.Length; i++)
        {
            if (mudGUIObjects[i].gameObject.activeInHierarchy)
            {
                mudSequence.Append(mudGUIObjects[i].DOFade(0f, 1f).SetEase(Ease.InSine).OnComplete(() => DisableObject(mudGUIObjects[i].gameObject)));
            }
        }

        mudSequence.ForceInit();
    }

    void DisableObject(GameObject obj)
    {
        obj.SetActive(false);
    }
}
