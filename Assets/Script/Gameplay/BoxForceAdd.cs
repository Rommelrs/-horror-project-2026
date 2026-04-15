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


    public void AddForceToBoxes()
    {
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
    }
}
