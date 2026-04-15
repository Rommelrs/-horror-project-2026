using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chandelier : MonoBehaviour
{
    Health health;
    Rigidbody rb;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        //Subscribe
        health.OnDeath.AddListener(DropCandelier);
    }

    private void OnDestroy()
    {
        //Unsubscribe
        health.OnDeath.RemoveListener(DropCandelier);
    }

    public void DropCandelier()
    {
        //Enable Gravity
        rb.useGravity = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.collider == null) return;

        Enemy enemy = collision.collider.GetComponent<Enemy>();

        if(enemy == null)
            enemy = collision.collider.GetComponentInParent<Enemy>();

        //Damge enemy that collide
        if(enemy != null)
        {
            enemy.health.Damage(1000);
        }
        else
        {
            Destroy(this.gameObject, 1.5f);
        }
    }
}
