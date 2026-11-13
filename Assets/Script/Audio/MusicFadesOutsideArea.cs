using UnityEngine;

/// <summary>
/// Area music (an encounter's track): fades out while the player is outside the area and back in when he returns, so it
/// never follows him through a door or down a street, whichever way he leaves. Put it on the music's AudioSource. The area is
/// one or more boxes in world space (shown in the scene view when selected).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicFadesOutsideArea : MonoBehaviour
{
    [Tooltip("Boxes (world space) that make up the area. Inside any of them = inside.")]
    [SerializeField] Bounds[] area = new Bounds[0];
    [Tooltip("Seconds outside before the music starts fading (a step over the edge doesn't cut it).")]
    [SerializeField] float graceTime = 1f;
    [SerializeField] float fadeOutTime = 2f;
    [SerializeField] float fadeInTime = 1.5f;

    AudioSource source;
    float fullVolume;
    float outsideFor;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        fullVolume = source.volume;
    }

    void OnEnable() { outsideFor = 0f; }

    public bool Contains(Vector3 p)
    {
        foreach (var b in area) if (b.Contains(p)) return true;
        return false;
    }

    void Update()
    {
        var player = Player.instance;
        if (player == null || area.Length == 0) return;

        bool inside = Contains(player.transform.position);
        outsideFor = inside ? 0f : outsideFor + Time.deltaTime;
        bool fadeOut = outsideFor > graceTime;

        float target = fadeOut ? 0f : fullVolume;
        float rate = fullVolume / Mathf.Max(0.01f, fadeOut ? fadeOutTime : fadeInTime);
        source.volume = Mathf.MoveTowards(source.volume, target, rate * Time.deltaTime);

        if (fadeOut && source.volume <= 0f && source.isPlaying) source.Pause();
        else if (!fadeOut && !source.isPlaying && source.volume < fullVolume) source.UnPause();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        foreach (var b in area) Gizmos.DrawWireCube(b.center, b.size);
    }
}
