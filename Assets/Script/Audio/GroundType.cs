using UnityEngine;

public enum SurfaceType
{
    Default,
    Grass,
    Sand,
    Asphalt,
    Wood,
    Tiles
}

public class GroundType : MonoBehaviour
{
    public SurfaceType surfaceType = SurfaceType.Default;
}
