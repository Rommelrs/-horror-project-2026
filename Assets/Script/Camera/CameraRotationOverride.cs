using UnityEngine;

/// <summary>
/// Attach to any GameObject. When Active, forces the target transform to a specific
/// world-space Y rotation every LateUpdate, AFTER CameraSystem runs.
/// Assign PositionRoot as the target, enable via CinematicPreSequenceController.
/// </summary>
[DefaultExecutionOrder(100)] // Runs after CameraSystem (default order 0)
public class CameraRotationOverride : MonoBehaviour
{
    [Tooltip("The transform to override (e.g. PositionRoot under CameraSystem).")]
    public Transform target;
    [Tooltip("World-space Y rotation to force on the target.")]
    public float targetYRotation = 180f;
    [Tooltip("Whether the override is currently active.")]
    public bool isActive = false;

    private void LateUpdate()
    {
        if (!isActive || target == null) return;
        Vector3 euler = target.rotation.eulerAngles;
        euler.y = targetYRotation;
        target.rotation = Quaternion.Euler(euler);
    }
}
