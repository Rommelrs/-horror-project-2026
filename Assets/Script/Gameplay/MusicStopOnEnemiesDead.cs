using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MusicStopOnEnemiesDead : MonoBehaviour
{
    [Header("Enemies to Track")]
    [SerializeField] List<Enemy> enemies = new List<Enemy>();

    [Header("Audio")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] float fadeOutDuration = 2f;

    [Header("Events")]
    public UnityEvent OnAllEnemiesDead;

    int enemiesAlive;

    private void Start()
    {
        enemiesAlive = enemies.Count;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null)
                enemy.OnEnemyDied.AddListener(OnEnemyKilled);
        }
    }

    private void OnDestroy()
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null)
                enemy.OnEnemyDied.RemoveListener(OnEnemyKilled);
        }
    }

    void OnEnemyKilled()
    {
        enemiesAlive--;

        if (enemiesAlive <= 0)
        {
            OnAllEnemiesDead?.Invoke();

            if (musicSource != null)
                StartCoroutine(Co_FadeOutMusic());
        }
    }

    IEnumerator Co_FadeOutMusic()
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        musicSource.volume = 0f;
        musicSource.Stop();
    }
}
