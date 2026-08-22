using UnityEngine;

public class MapMarkerPositioner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The map sprite RectTransform (e.g. MapMiddleGroup_2)")]
    [SerializeField] RectTransform mapRect;
    [Tooltip("The marker RectTransform to move (this script's GameObject)")]
    RectTransform markerRect;

    [Header("World Bounds")]
    [Tooltip("World X,Z of the bottom-left corner of the map")]
    [SerializeField] Vector2 worldMin;
    [Tooltip("World X,Z of the top-right corner of the map")]
    [SerializeField] Vector2 worldMax;

    [Header("Axis Flip (toggle if marker moves in wrong direction)")]
    [SerializeField] bool flipX = false;
    [SerializeField] bool flipY = false;

    [Header("Rotation")]
    [Tooltip("Adjust this if the marker arrow does not point forward. Try 0, 90, 180, or 270.")]
    [SerializeField] float rotationOffset = 0f;

    void Awake()
    {
        markerRect = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (Player.instance == null || mapRect == null) return;

        Vector3 playerPos = Player.instance.transform.position;

        float normalizedX = Mathf.InverseLerp(worldMin.x, worldMax.x, playerPos.x);
        float normalizedZ = Mathf.InverseLerp(worldMin.y, worldMax.y, playerPos.z);

        if (flipX) normalizedX = 1f - normalizedX;
        if (flipY) normalizedZ = 1f - normalizedZ;

        float mapWidth  = mapRect.rect.width;
        float mapHeight = mapRect.rect.height;

        markerRect.anchoredPosition = new Vector2(
            (normalizedX - 0.5f) * mapWidth,
            (normalizedZ - 0.5f) * mapHeight
        );

        // Rotate the marker to match the player's world-space facing direction.
        markerRect.localRotation = Quaternion.Euler(
            0f,
            0f,
            -Player.instance.transform.eulerAngles.y + rotationOffset
        );
    }
}
