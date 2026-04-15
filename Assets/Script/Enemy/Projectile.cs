using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour, IPoolable
{
    [Header("Projectile Settings")]
    public float minLaunchAngle = 35f;  // Minimum launch angle for flat/downward shots
    public float maxLaunchAngle = 75f;  // Maximum launch angle for very high shots
    public float gravity = -30f;

    [Space(10)]
    public GameObject hitDamagePrefab; // Ground effect
    public GameObject hitWallPrefab;    // Wall effect (optional - uses ground effect if null)
    public GameObject destroyParticlePrefab;
    [SerializeField] AudioClip[] hitImpactSoundEffects;
    [Range(0f, 90f)] public float wallAngleThreshold = 45f; // Surface angles above this are walls

    [Header("Drop On Destroy - Shot by Bullet")]
    [SerializeField] private DropItem[] dropsOnBulletHit;

    [Header("Drop On Destroy - Ground Impact")]
    [SerializeField] private DropItem[] dropsOnGroundImpact;

    [Header("Drop Settings")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] private float dropForce = 3f;
    [SerializeField] private float dropUpwardForce = 2f;

    [System.Serializable]
    public class DropItem
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float dropChance = 100f;
    }

    bool canCollide = true;
    private Rigidbody rb;
    Enemy owner;

    public Enemy GetOwner()
    {
        return owner;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // We'll apply gravity manually
    }

    private void FixedUpdate()
    {
        rb.velocity += Vector3.up * gravity * Time.fixedDeltaTime;
    }

    private void LateUpdate()
    {
        // Rotate arrow to face velocity
        if (rb.velocity.sqrMagnitude > 0.1f)
        {
            transform.forward = rb.velocity.normalized;
        }
    }

    public void ShootProjectile(Vector3 targetPosition, Enemy owner)
    {
        this.owner = owner;
        Vector3 velocity = CalculateLaunchVelocity(targetPosition);

        if (velocity == Vector3.zero)
        {
            return;
        }

        rb.velocity = velocity;
    }

    public void ShootProjectile(Vector3 targetPosition, BagBearerProjectileTest owner)
    {
        this.owner = null;
        Vector3 velocity = CalculateLaunchVelocity(targetPosition);

        if (velocity == Vector3.zero)
        {
            return;
        }

        rb.velocity = velocity;
    }

    // Shoot at a fixed position without an owner
    public void ShootProjectileAtPosition(Vector3 targetPosition)
    {
        this.owner = null;
        Vector3 velocity = CalculateLaunchVelocity(targetPosition);

        if (velocity == Vector3.zero)
        {
            return;
        }

        rb.velocity = velocity;
    }

    private Vector3 CalculateLaunchVelocity(Vector3 target)
    {
        Vector3 start = transform.position;

        // Horizontal direction (XZ only)
        Vector3 toTargetXZ = new Vector3(
            target.x - start.x,
            0f,
            target.z - start.z
        );

        float distance = toTargetXZ.magnitude;
        float heightDifference = target.y - start.y;
        float gravityAbs = Mathf.Abs(gravity);

        // Dynamically calculate the best launch angle
        float bestAngle = CalculateBestAngle(distance, heightDifference);
        float angleRad = bestAngle * Mathf.Deg2Rad;

        // v² = g d² / (2 cos²(a) (d tan(a) - h))
        float velocitySquared =
            (gravityAbs * distance * distance) /
            (2f * Mathf.Cos(angleRad) * Mathf.Cos(angleRad) *
            (distance * Mathf.Tan(angleRad) - heightDifference));

        // Prevent NaN / invalid shots
        if (velocitySquared <= 0f)
            return Vector3.zero;

        float speed = Mathf.Sqrt(velocitySquared);

        // Build velocity vector
        Vector3 velocity =
            toTargetXZ.normalized * speed * Mathf.Cos(angleRad);

        velocity.y = speed * Mathf.Sin(angleRad);

        return velocity;
    }

    private float CalculateBestAngle(float distance, float heightDifference)
    {
        // For targets below or at same height, use minimum angle
        if (heightDifference <= 0f)
            return minLaunchAngle;

        // Calculate the angle needed based on height-to-distance ratio
        // Higher ratio = steeper angle needed
        float heightRatio = heightDifference / Mathf.Max(distance, 0.1f);

        // Map height ratio to angle range
        // ratio 0.0 → minLaunchAngle (35°)
        // ratio 1.0+ → maxLaunchAngle (75°)
        float normalizedRatio = Mathf.Clamp01(heightRatio);
        float angle = Mathf.Lerp(minLaunchAngle, maxLaunchAngle, normalizedRatio);

        return angle;
    }

    public void DestoryProjectile(int damage)
    {
        canCollide = false;

        //Spawn Particle
        GameObject hitParticleObj = destroyParticlePrefab.Reuse(this.transform.position, Quaternion.identity);
        ParticleSystem hitParticleSys = hitParticleObj.GetComponent<ParticleSystem>();
        hitParticleSys.Play();

        //Play SFX
        SoundEffectManager.instance.PlaySFXAtPosition(hitImpactSoundEffects[Random.Range(0, hitImpactSoundEffects.Length)], transform.position);

        //Drop item (shot by bullet)
        SpawnDrops(dropsOnBulletHit);

        //Send back to pool
        this.gameObject.Release();
    }

    private void SpawnDrops(DropItem[] drops)
    {
        if (drops == null || drops.Length == 0) return;

        foreach (var drop in drops)
        {
            if (drop.prefab != null && Random.Range(0f, 100f) <= drop.dropChance)
            {
                GameObject droppedItem = Instantiate(drop.prefab, transform.position + dropOffset, Quaternion.identity);
                
                // Apply force if has rigidbody
                Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                    Vector3 force = randomDir * dropForce + Vector3.up * dropUpwardForce;
                    rb.AddForce(force, ForceMode.Impulse);
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!canCollide)
            return;

        canCollide = false;
        
        
        // Check if we hit a lock
        Lock lockObject = collision.collider.GetComponentInParent<Lock>();
        if (lockObject != null)
        {
            lockObject.LockDamaged();
            
            // Still spawn particle effect at impact point
            Vector3 lockHitPosition = collision.transform.position;
            if (collision.contactCount > 0)
            {
                lockHitPosition = collision.GetContact(0).point;
            }
            
            GameObject lockHitParticle = hitDamagePrefab.Reuse(lockHitPosition, Quaternion.identity);
            ParticleSystem lockHitParticleSys = lockHitParticle.GetComponent<ParticleSystem>();
            lockHitParticleSys.Play();
            
            //Play SFX
            if (hitImpactSoundEffects.Length > 0)
                SoundEffectManager.instance.PlaySFXAtPosition(hitImpactSoundEffects[Random.Range(0, hitImpactSoundEffects.Length)], transform.position);
            
            //Send back to pool
            this.gameObject.Release();
            return;
        }

        Vector3 spawnPosition = collision.transform.position;
        Vector3 surfaceNormal = Vector3.up;
        bool isWall = false;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            spawnPosition = contact.point;
            surfaceNormal = contact.normal;
            
            // Determine if surface is a wall based on normal angle
            float angle = Vector3.Angle(surfaceNormal, Vector3.up);
            isWall = angle > wallAngleThreshold;
        }

        if (collision.gameObject.CompareTag("Player"))
        {           
            spawnPosition = collision.transform.position;
            isWall = false; // Always use ground effect for player
        }

        //Spawn Particle - choose effect based on surface
        GameObject effectToSpawn = isWall && hitWallPrefab != null ? hitWallPrefab : hitDamagePrefab;
        
        // Rotate effect to align with surface
        Quaternion effectRotation = Quaternion.identity;
        if (isWall)
        {
            // Make effect face away from wall
            effectRotation = Quaternion.LookRotation(surfaceNormal);
        }
        
        GameObject hitParticleObj = effectToSpawn.Reuse(spawnPosition, effectRotation);
        ParticleSystem hitParticleSys = hitParticleObj.GetComponent<ParticleSystem>();
        hitParticleSys.Play();

        //Play SFX
        SoundEffectManager.instance.PlaySFXAtPosition(hitImpactSoundEffects[Random.Range(0, hitImpactSoundEffects.Length)], transform.position);

        //Drop item (ground impact)
        SpawnDrops(dropsOnGroundImpact);

        //Send back to pool
        this.gameObject.Release();
    }

    public void OnPool()
    {
        canCollide = true;
    }

    public void OnDepool()
    {
        
    }
}
