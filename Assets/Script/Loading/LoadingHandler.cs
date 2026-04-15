using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LoadingHandler : MonoBehaviour
{
    public static LoadingHandler instance;

    [Header("UI")]
    [SerializeField] GameObject loadingMenu;
    [SerializeField] Image blackImage;
    [SerializeField] Image loadingBar;
    [SerializeField] TMP_Text loadingTxt;

    Sequence loadingSequence;
    Coroutine loadingCR;

    bool _loading = false;
    public static bool IsLoading()
    {
        if (instance == null)
            return false;

        return instance._loading;
    }

    private void Awake()
    {
        //Singleton to ensure there is no duplicate LoadingHandler
        if (instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    /// Load a scene by name
    public void LoadScene(string sceneName)
    {
        // Save current scene name before loading new scene
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        
        if (loadingCR != null) StopCoroutine(loadingCR);
        loadingCR = StartCoroutine(Co_LoadScene(sceneName));
    }

    /// Coroutine to load a scene asynchronously
    IEnumerator Co_LoadScene(string sceneName)
    {
        _loading = true;
        GameManager.IsPaused = false;
        loadingMenu.SetActive(true);

        //Start Load State
        Color blackImageColor = blackImage.color;
        blackImageColor.a = 0f;
        blackImage.color = blackImageColor;

        Color loadingBarColor = loadingBar.color;
        loadingBarColor.a = 0f;
        loadingBar.color = loadingBarColor;

        Color loadingColor = loadingTxt.color;
        loadingColor.a = 0f;
        loadingTxt.color = loadingColor;

        //Reset Loading Bar Fill Amount
        loadingBar.fillAmount = 0f;

        ////Reset Loading Bar previous tween
        //loadingBar.DOKill();

        ////loading Bar Rotate & Fill
        //loadingBar.transform.DORotate(new Vector3(0, 0, 360), 1f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetRelative();
        //loadingBar.DOFillAmount(1f, 2.2f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetRelative();

        //Reset Loading Sequence
        if (loadingSequence != null)
            loadingSequence.Kill();

        //Loading Start Animation
        loadingSequence.Append(blackImage.DOFade(1f, 0.5f).SetEase(Ease.InSine));
        //loadingSequence.Append(loadingTxt.DOFade(1f, 0.5f).SetEase(Ease.InSine));
        //loadingSequence.Append(loadingBar.DOFade(1f, 0.5f).SetEase(Ease.InSine));

        //loadingSequence.ForceInit();

        yield return new WaitForSeconds(3.5f);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        // Wait until the asynchronous scene fully loads
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return null;

        //End Load State
        //Reset Loading Bar previous tween
        //loadingBar.DOKill();

        //Reset Loading Sequence
        if (loadingSequence != null)
            loadingSequence.Kill();

        //Loading End Animation
        loadingSequence.Append(blackImage.DOFade(0f, 0.5f).SetEase(Ease.InSine));
        //loadingSequence.Append(loadingTxt.DOFade(0f, 0.5f).SetEase(Ease.InSine));
        //loadingSequence.Append(loadingBar.DOFade(0f, 0.5f).SetEase(Ease.InSine));

        loadingSequence.ForceInit();

        yield return new WaitForSeconds(1.6f);
        loadingMenu.SetActive(false);
        GameManager.IsPaused = false;

        _loading = false;
    }
}
