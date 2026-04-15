using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ToolBox.Pools;

public class Lock : MonoBehaviour
{
    public DoorInteractable connectedDoor;
    public AudioClip destoryClip;
    [SerializeField] private GameObject visualsObject; // The mesh/model to hide immediately
    [SerializeField] private GameObject destroyParticleEffect; // Particle effect when lock is destroyed
    
    private SaveableInteractable saveableInteractable;
    private bool isDamaged = false;
    
    private void Awake()
    {
        saveableInteractable = GetComponent<SaveableInteractable>();
    }
    
    private void Start()
    {
        // Check if already destroyed in a previous save
        if (saveableInteractable != null && saveableInteractable.WasAlreadyUsed())
        {
            // Silently destroy this lock without playing sounds
            if (connectedDoor != null)
            {
                connectedDoor.hasDoorLock = false;
                connectedDoor.doorLock = null;
            }
            Destroy(gameObject);
        }
    }

    public void LockDamaged()
    {
        
        if (isDamaged)
        {
            return; // Prevent multiple calls
        }
        isDamaged = true;
        
        // Mark as used in save system
        if (saveableInteractable != null)
        {
            saveableInteractable.MarkAsUsed();
        }
        
        if (SoundEffectManager.instance != null && destoryClip != null)
        {
            SoundEffectManager.instance.PlaySFXAtPosition(destoryClip, transform.position);
        }
        
        // Spawn particle effect
        if (destroyParticleEffect != null)
        {
            GameObject particle = destroyParticleEffect.Reuse(transform.position, Quaternion.identity);
            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
            else
            {
            }
        }
        else
        {
        }

        //Remove Lock from the door
        if (connectedDoor != null)
        {
            connectedDoor.hasDoorLock = false;
            connectedDoor.doorLock = null;
        }
        else
        {
        }
        
        // Hide the visual mesh immediately
        if (visualsObject != null)
        {
            visualsObject.SetActive(false);
        }
        else
        {
            // Fallback: disable renderers if visualsObject not assigned
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }
        }
        
        // Disable collider so it can't be hit again
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        //Destroy Lock Object after delay (allows particles to finish)
        Destroy(gameObject, 3f);
    }

    // Called when combination lock is solved
    public void UnlockDoor()
    {
        // Mark as used in save system
        if (saveableInteractable != null)
            saveableInteractable.MarkAsUsed();
        
        //Remove Lock from the door
        if (connectedDoor != null)
        {
            connectedDoor.hasDoorLock = false;
            connectedDoor.doorLock = null;
        }
    }
}
