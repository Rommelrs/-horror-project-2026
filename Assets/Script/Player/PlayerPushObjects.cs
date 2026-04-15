using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPushObjects : MonoBehaviour
{
    public float pushForce = 5f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;

        // No Rigidbody or kinematic → ignore
        if (rb == null || rb.isKinematic)
            return;

        // Don't push objects downwards
        if (hit.moveDirection.y < -0.3f)
            return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);

        Box box = hit.collider.GetComponent<Box>();
        if (box != null) box.Collide();
    }
}
