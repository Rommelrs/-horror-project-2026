using UnityEngine;

public class RotateX : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 90f; // Degrees per second
    [SerializeField] RotationAxis axis = RotationAxis.X;

    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    void Update()
    {
        Vector3 rotationVector = Vector3.zero;
        
        switch (axis)
        {
            case RotationAxis.X:
                rotationVector = Vector3.right;
                break;
            case RotationAxis.Y:
                rotationVector = Vector3.up;
                break;
            case RotationAxis.Z:
                rotationVector = Vector3.forward;
                break;
        }
        
        transform.Rotate(rotationVector, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
