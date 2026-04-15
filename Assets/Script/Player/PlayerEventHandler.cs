using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerEventHandler : MonoBehaviour
{
    public Action OnAttack;
    public Action OnHeavyAttack;
    public Action OnStartSlash;
    public Action OnEndSlash;
    public Action OnFootstep;
    public Action OnShoot;
    public UnityEvent OnReloadComplete; public Action OnReloadAnimationEnd;

    //Trigger Player Events
    public void TriggerAttack()
    {
        OnAttack?.Invoke();
    }

    public void TriggerHeavyAttack()
    {
        OnHeavyAttack?.Invoke();
    }

    public void StartSlash()
    {
        OnStartSlash?.Invoke();
    }

    public void EndSlash()
    {
        OnEndSlash?.Invoke();
    }

    public void Footstep()
    {
        OnFootstep?.Invoke();
    }

    public void Shoot()
    {
        OnShoot?.Invoke();
    }

    public void Reload()
    {
        OnReloadComplete?.Invoke();
    } public void ReloadAnimationEnd() { OnReloadAnimationEnd?.Invoke(); }
}
