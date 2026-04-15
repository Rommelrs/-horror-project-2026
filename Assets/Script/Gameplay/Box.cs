using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Box : MonoBehaviour
{
    float collideMinCooldown = 0.3f;
    [SerializeField] AudioClip[] collideClips;

    float nextCollide = 0;

    private void OnCollisionEnter(Collision collision)
    {
        if (collideClips == null || collideClips.Length <= 0)
            return;

        Collide();
    }

    public void Collide()
    {
        if (Time.time < nextCollide)
            return;

        nextCollide = Time.time + collideMinCooldown;
        SoundEffectManager.instance.PlaySFXAtPosition(collideClips[Random.Range(0, collideClips.Length)], transform.position);
    }
}
