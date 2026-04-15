using UnityEngine;

public static class DebugArrow
{
    public static void DrawArrow(Vector3 start, Vector3 end, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f, float duration = 3f)
    {
        // Draw main line
        Debug.DrawLine(start, end, color, duration);

        Vector3 direction = (end - start).normalized;

        // Arrowhead directions
        Vector3 right = Quaternion.LookRotation(direction) *
                        Quaternion.Euler(0, 180 + arrowHeadAngle, 0) *
                        Vector3.forward;

        Vector3 left = Quaternion.LookRotation(direction) *
                       Quaternion.Euler(0, 180 - arrowHeadAngle, 0) *
                       Vector3.forward;

        // Draw arrowhead
        Debug.DrawLine(end, end + right * arrowHeadLength, color, duration);
        Debug.DrawLine(end, end + left * arrowHeadLength, color, duration);
    }
}
