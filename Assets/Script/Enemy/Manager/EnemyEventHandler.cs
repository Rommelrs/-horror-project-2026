using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyEventHandler : MonoBehaviour
{
    public Action OnAttack;
    public Action OnFootstep;

    public UnityEvent customEventOne;
    public UnityEvent customEventTwo;

    public void TriggerAttack()
    {
        OnAttack?.Invoke();
    }

    public void PlayFootstepLeft()
    {
        OnFootstep?.Invoke();
    }

    public void PlayFootstepRight()
    {
        OnFootstep?.Invoke();
    }

    public void TriggerCustomEventOne()
    {
        customEventOne?.Invoke();
    }

    public void TriggerCustomEventTwo()
    {
        customEventTwo?.Invoke();
    }
}
