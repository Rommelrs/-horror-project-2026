using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class BagBearerProjectileTest : MonoBehaviour
{
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] Transform spawnPoint;
    [SerializeField] float shootFrequency = 2f;
    [SerializeField] float positionOffset = 0.8f;
    [SerializeField] Transform target;

    float time = 0f;


    private void Update()
    {
        time += Time.deltaTime;
        if(time >= shootFrequency)
        {
            Shoot();
            time = 0f;
        }
    }

    void Shoot()
    {
        //Spawn a projectile and throw at the player
        Vector3 direction = transform.position - target.position;
        Vector3 targetPosition = target.position + direction.normalized * positionOffset;

        GameObject projectileObj = projectilePrefab.gameObject.Reuse(spawnPoint.position, Quaternion.identity);
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        projectile.ShootProjectile(targetPosition, this);
    }
}
