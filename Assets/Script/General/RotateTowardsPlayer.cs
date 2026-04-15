using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateTowardsPlayer : MonoBehaviour
{
    public float rotationSpeed = 20f;

    private void FixedUpdate()
    {
        if (Player.instance == null)
            return;

        //Apply Rotation
        transform.rotation = Quaternion.Lerp(transform.rotation, Player.instance.transform.rotation, rotationSpeed * Time.fixedUnscaledDeltaTime);
    }
}
