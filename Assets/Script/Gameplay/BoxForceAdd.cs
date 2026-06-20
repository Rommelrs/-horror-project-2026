using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxForceAdd : MonoBehaviour
{
    [Header("References")]
    public Rigidbody []boxRbs;
    //public AudioClip boxExplodeClip;
    public Transform explodePoint;

    [Header("Forces")]
    public float explosionForce = 8f;
    public float upwardForce = 4f;
    public float torqueForce = 10f;

    [Header("Collision Settings")]
    public bool triggerOnCollision = false;
    public string[] triggerTags; // Tags that can trigger the explosion (e.g., "Player", "Enemy")
    public bool destroyAfterTrigger = false;

    private bool hasTriggered = false;


    public void AddForceToBoxes()
    {
        if (hasTriggered)
            return;

        hasTriggered = true;

        for (int i = 0; i < boxRbs.Length; i++)
        {
            Rigidbody boxRb = boxRbs[i];

            boxRb.isKinematic = false;

            Vector3 forceDir = Vector3.up * upwardForce + Random.insideUnitSphere;
            boxRb.AddForce(forceDir * explosionForce, ForceMode.Impulse);
            boxRb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
        }

        //Play SFX
        //SoundEffectManager.instance.PlaySFXAtPosition(boxExplodeClip, transform.position);

        if (destroyAfterTrigger)
        {
            Destroy(gameObject, 0.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnCollision || hasTriggered)
            return;

        // Check if the colliding object has one of the trigger tags
        if (triggerTags != null && triggerTags.Length > 0)
        {
            foreach (string tag in triggerTags)
            {
                if (other.CompareTag(tag))
                {
                    AddForceToBoxes();
                    return;
                }
            }
        }
        else
        {
            // If no tags specified, trigger on any collision
            AddForceToBoxes();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!triggerOnCollision || hasTriggered)
            return;

        // Check if the colliding object has one of the trigger tags
        if (triggerTags != null && triggerTags.Length > 0)
        {
            foreach (string tag in triggerTags)
            {
                if (collision.gameObject.CompareTag(tag))
                {
                    AddForceToBoxes();
                    return;
                }
            }
        }
        else
        {
            // If no tags specified, trigger on any collision
            AddForceToBoxes();
        }
    }
}
