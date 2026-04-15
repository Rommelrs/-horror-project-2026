using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class MessageList : MonoBehaviour
{
    [SerializeField] TMP_Text messageTxt;
    [SerializeField] float lifetime = 4f;

    CanvasGroup canvasGroup;
    DG.Tweening.Sequence sequence;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(string message)
    {
        //Update Message Text
        messageTxt.text = message;

        canvasGroup.alpha = 0f;

        //Stop Previous animation
        if (sequence != null)
            sequence.Kill();

        //Start Animation
        sequence = DOTween.Sequence();
        sequence.Append(canvasGroup.DOFade(1f, 0.5f).SetEase(Ease.Linear));
        sequence.Append(canvasGroup.DOFade(1f, 0.5f).SetEase(Ease.Linear).SetDelay(lifetime).OnComplete(OnCompleteAnimation));
        sequence.ForceInit();
    }

    void OnCompleteAnimation()
    {
        //Stop Animation
        if (sequence != null)
            sequence.Kill();

        //Destory
        Destroy(this.gameObject);
    }
}
