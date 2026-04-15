using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] GameObject rootObject;
    [SerializeField] float fillDuration = 0.5f;
    [SerializeField] Image fillOne;
    [SerializeField] Image fillTwo;
    [SerializeField] bool lookAtCamera = true;

    private void Update()
    {
        // Rotate the health bar to face the camera
        if (lookAtCamera)
            transform.LookAt(Camera.main.transform);
    }

    //Enable or disable the health bar
    public void EnableHealthBar(bool value)
    {
        if (rootObject != null)
            rootObject.SetActive(value);
    }

    //Set the health bar to a specific value
    public void SetHealth(int health, int maxHealth, bool animate = false)
    {
        if (animate)
        {
            DG.Tweening.Sequence sequence = DOTween.Sequence();

            sequence.Append(fillOne.DOFillAmount((float)health / (float)maxHealth, fillDuration / 2f));
            sequence.Append(fillTwo.DOFillAmount((float)health / (float)maxHealth, fillDuration));

            sequence.ForceInit();
        }
        else
        {
            fillOne.fillAmount = (float)health / (float)maxHealth;
            fillTwo.fillAmount = (float)health / (float)maxHealth;
        }
    }
}
