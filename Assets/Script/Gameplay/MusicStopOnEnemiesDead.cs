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

    bool done;
    readonly HashSet<Enemy> died = new HashSet<Enemy>();

    private void Start()
    {
        foreach (Enemy enemy in enemies)
            if (enemy != null)
            {
                Enemy e = enemy;
                enemy.OnEnemyDied.AddListener(() => died.Add(e));
            }
    }

    // Checks the living ones every frame instead of counting deaths: an enemy that was already dead in the loaded save or
    // checkpoint is destroyed at load (SaveableEnemy) and never reports a death, which used to leave the music on for good.
    // One that isn't switched on yet is alive (it reads 0 health until it wakes up, so its health can't be trusted then).
    bool Alive(Enemy e) => e != null && !died.Contains(e) && (!e.gameObject.activeInHierarchy || e.health == null || !e.health.IsDead);

    private void Update()
    {
        if (done) return;
        foreach (Enemy enemy in enemies)
            if (Alive(enemy))
                return;

        done = true;
        OnAllEnemiesDead?.Invoke();
        if (musicSource != null && musicSource.isActiveAndEnabled)
            StartCoroutine(Co_FadeOutMusic());
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
