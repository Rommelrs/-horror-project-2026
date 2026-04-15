using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineEnemyController : MonoBehaviour
{
    [SerializeField] PlayableDirector playableDirector;
    [SerializeField] float timelinePlayDelay = 1f;
    [SerializeField] Enemy []enemies;


    [ContextMenu("Run Test Function")]
    public void PlayTimeline()
    {
        StartCoroutine(Co_PlayTimeline());
    }

    IEnumerator Co_PlayTimeline()
    {
        yield return new WaitForSeconds(timelinePlayDelay);

        //Enable Enemies
        foreach (Enemy enemy in enemies)
            enemy.gameObject.SetActive(true);

        StopEnemyState();

        playableDirector.Play();
        playableDirector.stopped += TimelineFinish;
    }

    void TimelineFinish(PlayableDirector pd)
    {
        ResetEnemyState();
    }

    void StopEnemyState()
    {
        foreach (Enemy enemy in enemies)
        {
            enemy.PauseEnemyState(true);
        }
    }

    void ResetEnemyState()
    {
        foreach (Enemy enemy in enemies)
        {
            enemy.PauseEnemyState(false);
            enemy.ResetEnemy();
        }
    }
}
