using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening.Core.Easing;
using ToolBox.Pools;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner instance;

    [System.Serializable]
    public struct EnemyWave
    {
        public int enemyCount;
        public float spawnInterval;
        public GameObject enemyPrefab;
    }

    [SerializeField] bool enableWaveAtStart = true;
    [SerializeField] GameObject []enemyPrefabs;
    [SerializeField] Transform []spawnPoints;
    public EnemyWave[] waves;
    public TMP_Text waveText;
    public float waveStartDelay = 2f;

    int waveIndex = 0;
    int killCount = 0;
    int totalKillCount = 0;

    bool wavesEnded = false;

    private void Awake()
    {
        instance = this;
    }

    public void Start()
    {
        if (enableWaveAtStart)
        {
            // Start the first wave
            waveIndex = 0;
            StartEnemyWaves();
        }
    }

    // Get a random spawn point from the list of spawn points
    public Transform GetRandomSpawnPoint()
    {
         return spawnPoints[Random.Range(0, spawnPoints.Length)];
    }

    public GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null)
            return null;

        if (enemyPrefabs.Length <= 0)
            return null;

        return enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
    }

    public void StartEnemyWaves()
    {
        if (wavesEnded) return;

        // If there are more waves to spawn then start the next wave
        if (waveIndex < waves.Length)
        {
            StartCoroutine(SpawnEnemyWave(waves[waveIndex]));

            if (waveText != null)
            {
                if (waveIndex == waves.Length - 1)
                    waveText.text = "Final Wave";
                else
                    waveText.text = "Wave " + (waveIndex + 1);
            }
        }
        else
        {
            wavesEnded = true;

            // If there are no more waves then the game is won
            //GameManager.instance.GameWon();
        }
    }

    public int GetKillCount()
    {
        return totalKillCount;
    }

    public void EnemyKilled()
    {
        // Increment the kill count and check if the wave is complete
        killCount++;
        totalKillCount++;

        //Increase Stability
        Player.instance.playerStability.IncreaseStability(10);

        if (wavesEnded) return;

        if (killCount >= waves[waveIndex].enemyCount)
        {
            waveIndex++;

            killCount = 0;
            StartEnemyWaves();
        }
    }

    public void SpawnEnemyOveride(bool forceWeakpoint = false)
    {
        Transform spawnPoint = GetRandomSpawnPoint();
        GameObject enemyObj = PoolHelper.Reuse(GetRandomEnemyPrefab());
        enemyObj.transform.position = spawnPoint.position;
        enemyObj.transform.rotation = spawnPoint.rotation;

        if (forceWeakpoint)
        {
            EnemyWeakpoint enemyWeakpoint = enemyObj.GetComponent<EnemyWeakpoint>();
            if (enemyWeakpoint != null && enemyWeakpoint.autoShowWeakpointOnStart) enemyWeakpoint.SpawnEnemyWeakpoint();
        }
    }

    // Spawn a wave of enemies
    IEnumerator SpawnEnemyWave(EnemyWave wave)
    {
        yield return new WaitForSeconds(waveStartDelay);

        for (int i = 0; i < wave.enemyCount; i++)
        {
            Transform spawnPoint = GetRandomSpawnPoint();

            GameObject enemyObj = PoolHelper.Reuse(wave.enemyPrefab);
            enemyObj.transform.position = spawnPoint.position;
            enemyObj.transform.rotation = spawnPoint.rotation;

            int ran = Random.Range(0, 101);
            if (ran < 20)
            {
                EnemyWeakpoint enemyWeakpoint = enemyObj.GetComponent<EnemyWeakpoint>();
                if (enemyWeakpoint != null && enemyWeakpoint.autoShowWeakpointOnStart) enemyWeakpoint.SpawnEnemyWeakpoint();
            }

            //GameObject enemyObj = Instantiate(wave.enemyPrefab, spawnPoint.position, spawnPoint.rotation);
            //enemyObj.GetComponent<Enemy>().Initialize(this);
            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }
}
