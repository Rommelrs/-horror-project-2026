using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class BloodEffectHandler : MonoBehaviour
{
    [SerializeField] PlayerStability playerStability;
    [SerializeField] GameObject bloodEffectPrefab1;
    [SerializeField] GameObject bloodEffectPrefab2;
    [SerializeField] Transform []bloodEffectPoints;
    [SerializeField] Health playerHealth;
    [SerializeField] float spawnFrequencyMin = 4.0f;
    [SerializeField] float spawnFrequencyMax = 7.0f;

    float nextStabilityDecreaseTime = 0f;
    float stabilityDecreaseFrequency = 10f;

    float nextCheckTime;

    private void Start()
    {
        //Set Next Check Time
        nextCheckTime = Time.time + Random.Range(spawnFrequencyMin, spawnFrequencyMax);
    }

    private void Update()
    {
        //Check if Bleeding
        if (IsPlayerBleeding() && Time.time > nextStabilityDecreaseTime)
        {
            //Decrease Stability every frequency
            nextStabilityDecreaseTime = Time.time + stabilityDecreaseFrequency;
            playerStability.DecreaseStability(1);
        }

        //Wait till next check time
        if (Time.time > nextCheckTime)
        {
            //Set Next Check Time
            nextCheckTime = Time.time + Random.Range(spawnFrequencyMin, spawnFrequencyMax);

            //Check if player is dead
            if (playerHealth.IsDead)
                return;

            //Check is player health is less than max health
            if(IsPlayerBleeding())
            {
                //Player Does not have full health then spawn blood effect

                Transform spawnPoint = bloodEffectPoints[Random.Range(0, bloodEffectPoints.Length)];
                GameObject bloodEffectObj = bloodEffectPrefab1.Reuse(spawnPoint.transform.position, spawnPoint.transform.rotation);
                bloodEffectObj.transform.SetParent(spawnPoint);

                GameObject bloodEffectObj2 = bloodEffectPrefab2.Reuse(spawnPoint.transform.position, spawnPoint.transform.rotation);
                bloodEffectObj2.transform.SetParent(spawnPoint);

                //Play Blood Particle Effect
                bloodEffectObj.GetComponent<ParticleSystem>().Play();
            }
        }
    }

    public bool IsPlayerBleeding()
    {
        if (playerHealth.IsDead)
            return false;

        if (playerHealth.GetHealthValue() < 50f)
            return true;

        return false;
    }
}
