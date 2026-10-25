using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shotgun's crosshair: a ring round the aim dot, sized and placed every frame to the real pellet cone
/// (PlayerWeaponSystem.pelletSpread, traced along the gun's aim) projected through the camera - every pellet lands inside
/// it. Shown only while a pellet gun is out, fading in and out with the aim dot (PlayerWeaponSystem fades that Image).
/// Sits on the crosshair dot; the ring is a child Image. Added by Tools > Weapons > Build Shotgun On Player.
/// </summary>
public class SpreadCrosshair : MonoBehaviour
{
    public Image dot;
    public Image ring;
    [Tooltip("The ring's line sits at this fraction of the sprite's half-size (ShotgunSpreadRing.png: 124.5 / 128).")]
    public float ringLineRadius = 0.973f;
    [Tooltip("How quickly the ring follows the gun's sway (higher = snappier).")]
    public float follow = 18f;
    [Range(0f, 1f)] public float ringAlpha = 0.85f;

    Canvas canvas;
    Vector2 pos, size;
    bool wasShown;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
    }

    void LateUpdate()
    {
        var weapons = Player.instance != null ? Player.instance.playerWeaponSystem : null;
        var cam = Camera.main;
        bool show = weapons != null && cam != null && weapons.pelletsPerShot > 1 && weapons.isAiming && weapons.gunEnd != null;

        Color c = ring.color;
        c.a = show ? dot.color.a * ringAlpha : 0f;
        ring.color = c;
        if (!show) { wasShown = false; return; }

        // the cone 10 m out along the gun's aim: its centre, and a point on its rim (same maths as FirePellets)
        Transform aim = weapons.gunEnd;
        Vector3 apex = aim.position - aim.forward * weapons.pelletTraceBack;
        const float d = 10f;
        Vector3 centre = apex + aim.forward * d;
        Vector3 rim = centre + cam.transform.up * weapons.pelletSpread * d;
        Vector3 sc = cam.WorldToScreenPoint(centre), sr = cam.WorldToScreenPoint(rim);
        if (sc.z <= 0f) return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 targetPos = (new Vector2(sc.x, sc.y) - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
        float radius = Vector2.Distance(sc, sr) / scale / ringLineRadius;
        Vector2 targetSize = Vector2.one * radius * 2f;
        if (!wasShown) { pos = targetPos; size = targetSize; wasShown = true; }
        float k = 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime);
        pos = Vector2.Lerp(pos, targetPos, k);
        size = Vector2.Lerp(size, targetSize, k);

        // the ring is a child of the dot, which sits at the screen centre
        ring.rectTransform.anchoredPosition = pos - ((RectTransform)transform).anchoredPosition;
        ring.rectTransform.sizeDelta = size;
    }
}
