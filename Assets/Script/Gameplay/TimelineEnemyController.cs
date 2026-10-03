using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineEnemyController : MonoBehaviour
{
    [SerializeField] PlayableDirector playableDirector;
    [SerializeField] float timelinePlayDelay = 1f;
    [SerializeField] Enemy []enemies;


    // A timeline must only run once: a second PlayTimeline() (e.g. from another trigger) would re-enable and
    // pause the enemies in the middle of the fight and stack up duplicate 'stopped' handlers.
    bool hasPlayed;

    [ContextMenu("Run Test Function")]
    public void PlayTimeline()
    {
        if (hasPlayed)
            return;

        hasPlayed = true;
        StartCoroutine(Co_PlayTimeline());
    }

    IEnumerator Co_PlayTimeline()
    {
        yield return new WaitForSeconds(timelinePlayDelay);

        //Enable Enemies
        foreach (Enemy enemy in enemies)
            enemy.gameObject.SetActive(true);

        StopEnemyState();

        playableDirector.stopped -= TimelineFinish;
        playableDirector.stopped += TimelineFinish;
        playableDirector.Play();
    }

    void TimelineFinish(PlayableDirector pd)
    {
        playableDirector.stopped -= TimelineFinish;
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
