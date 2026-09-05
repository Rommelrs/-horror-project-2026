using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any persistent GameObject.
/// Monitors all AudioSources and logs ones that are playing beyond the threshold distance.
/// Press F2 in-game to dump all currently playing sources to console.
/// </summary>
public class AudioDebugger : MonoBehaviour
{
    [Tooltip("Sounds heard beyond this distance will be flagged in console.")]
    [SerializeField] float distanceThreshold = 20f;

    [Tooltip("How often to scan (seconds).")]
    [SerializeField] float scanInterval = 1f;

    float nextScanTime = 0f;
    HashSet<AudioSource> loggedSources = new HashSet<AudioSource>();

    void Update()
    {
        // Manual dump on F2
        if (Input.GetKeyDown(KeyCode.F2))
            DumpAllPlayingSources();

        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;

        if (Player.instance == null) return;
        Vector3 playerPos = Player.instance.transform.position;

        AudioSource[] allSources = FindObjectsOfType<AudioSource>();
        foreach (var src in allSources)
        {
            if (!src.isPlaying) continue;
            if (src.spatialBlend < 0.1f) continue; // skip 2D sounds

            float dist = Vector3.Distance(src.transform.position, playerPos);
            if (dist > distanceThreshold)
            {
                Debug.LogWarning($"[AudioDebugger] FAR SOUND at {dist:F1}m | " +
                    $"GameObject: '{src.gameObject.name}' | " +
                    $"Clip: '{(src.clip != null ? src.clip.name : "N/A")}' | " +
                    $"Volume: {src.volume:F2} | " +
                    $"MaxDist: {src.maxDistance:F1} | " +
                    $"SpatialBlend: {src.spatialBlend:F2}", src.gameObject);
            }
        }
    }

    void DumpAllPlayingSources()
    {
        AudioSource[] allSources = FindObjectsOfType<AudioSource>();
        Vector3 playerPos = Player.instance != null ? Player.instance.transform.position : Vector3.zero;

        Debug.Log($"[AudioDebugger] ===== DUMP ({allSources.Length} total AudioSources) =====");
        int playing = 0;
        foreach (var src in allSources)
        {
            if (!src.isPlaying) continue;
            playing++;
            float dist = Player.instance != null
                ? Vector3.Distance(src.transform.position, playerPos)
                : -1f;

            Debug.Log($"[AudioDebugger] PLAYING | " +
                $"'{src.gameObject.name}' | " +
                $"Clip: '{(src.clip != null ? src.clip.name : "N/A")}' | " +
                $"Dist: {dist:F1}m | " +
                $"Vol: {src.volume:F2} | " +
                $"MaxDist: {src.maxDistance:F1} | " +
                $"Spatial: {src.spatialBlend:F2} | " +
                $"Loop: {src.loop}", src.gameObject);
        }
        Debug.Log($"[AudioDebugger] ===== {playing} playing =====");
    }
}
