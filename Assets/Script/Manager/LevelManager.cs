using DG.Tweening.Core.Easing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [SerializeField] Player player;
    [SerializeField] MenuPanelSwitcher menuPanelSwitcher;

    public bool isGameOver = false;
    public bool isGameWon = false;

    Vector3 spawnPosition;
    Vector3 spawnRotation;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        //Unsubscribe to player death event
        if (player != null && player.health != null)
            player.health.OnDeath.RemoveListener(OnPlayerDeath);
    }

    private void Start()
    {
        //Set spawn position and rotation
        if (player != null)
        {
            spawnPosition = player.transform.position;
            spawnRotation = player.transform.eulerAngles;

            //Subscribe to player death event
            if (player.health != null)
                player.health.OnDeath.AddListener(OnPlayerDeath);
        }
    }

    //On Player Death
    void OnPlayerDeath()
    {
        StartCoroutine(Co_OnPlayerDeath());
    }

    //On Player Death wait for short delay and then call GameOver
    IEnumerator Co_OnPlayerDeath()
    {
        yield return new WaitForSeconds(4f);
        GameOver();
    }

    //On Game Over
    void GameOver()
    {
        if (isGameWon)
            return;

        if (isGameOver == false)
        {
            isGameOver = true;
            GameManager.IsPaused = true;
            menuPanelSwitcher.SwitchPanel("GameOverMenu");
        }
    }

    //On Game Won
    public void GameWon()
    {
        if (isGameOver)
            return;

        StartCoroutine(Co_GameWonDelay());
    }

    //Show Game Won Menu after a delay
    IEnumerator Co_GameWonDelay()
    {
        yield return new WaitForSeconds(3f);

        if (isGameWon == false)
        {
            isGameWon = true;
            GameManager.IsPaused = true;
            menuPanelSwitcher.SwitchPanel("GameWonMenu");
        }
    }
}
