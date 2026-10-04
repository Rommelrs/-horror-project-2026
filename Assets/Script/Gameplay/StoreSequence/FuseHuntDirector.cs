using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

/// <summary>
/// Phase 4 of the store level - the fuse hunt.
/// After the blackout the player searches the store's cabinets in any order. Each new search advances the dread:
///   1st search: a small physical disturbance out of sight (a can rolls out of another aisle / something falls off a shelf)
///   2nd search: the first clear whisper, from somewhere the player cannot see
///   3rd search: boxes scrape across the floor in the storage room - and only then does the fuse show up
/// In between, quieter environmental events happen 15-30 seconds apart. Everything goes through one queue, so two events
/// never overlap and there is always a gap after each one.
/// </summary>
public class FuseHuntDirector : MonoBehaviour
{
    [Header("Hooks")]
    [SerializeField] FuseHuntManager hunt;
    [SerializeField] StoreBlackoutSequence blackout;
    [Tooltip("Normally the hunt begins when the player has inspected the cash register and the fuse box (FuseHuntManager.onProblemIdentified). Turn this on to start it at the blackout instead.")]
    [SerializeField] bool beginHuntOnBlackout = false;

    [Header("Pacing")]
    [Tooltip("Seconds between ambient events (random in this range). Counted from the end of the previous event.")]
    [SerializeField] Vector2 ambientInterval = new Vector2(15f, 30f);
    [Tooltip("Minimum quiet time after any event, also between two search events that arrive close together.")]
    [SerializeField] float minGapAfterEvent = 4f;
    [Tooltip("A search event plays this long after the cabinet was opened.")]
    [SerializeField] float searchEventDelay = 1.2f;
    [Tooltip("The crash is still ringing when the hunt starts; nothing plays for this long after it.")]
    [SerializeField] float quietAfterCrash = 7f;
    [SerializeField] bool ambientEvents = true;

    [Header("Where things can happen")]
    [SerializeField] Collider interiorZone;
    [Tooltip("Never place events inside this volume (the storage room).")]
    [SerializeField] Bounds excludedVolume = new Bounds(new Vector3(-216.8f, 3f, 90.2f), new Vector3(11f, 8f, 9f));
    [SerializeField] Vector2 propDistance = new Vector2(5f, 11f);
    [SerializeField] Vector2 whisperDistance = new Vector2(6f, 10f);

    [Header("Physical props (cans, bottles ...)")]
    [SerializeField] GameObject[] propPrefabs;
    [SerializeField] AudioClip[] hitClips;
    [SerializeField] AudioClip[] tinkClips;
    [SerializeField] AudioClip rollLoop;
    [SerializeField] AudioClip shelfRattleClip;
    [SerializeField, Range(0f, 1f)] float shelfRattleVolume = 0.5f;
    [SerializeField] float rollSpeed = 1.9f;
    [SerializeField] int maxProps = 6;

    [Header("Whispers")]
    [Tooltip("The first one is the 'clear' whisper on the 2nd search; the others are used for faint ones later.")]
    [SerializeField] AudioClip[] whisperClips;
    [SerializeField, Range(0f, 1f)] float clearWhisperVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] float faintWhisperVolume = 0.4f;
    [Tooltip("The whisper source drifts this far (metres) while it plays, so its direction is hard to pin down.")]
    [SerializeField] float whisperDrift = 2.5f;

    [Header("Storage room")]
    [SerializeField] Transform storagePoint;
    [SerializeField] AudioClip[] storageThumps;
    [SerializeField] AudioClip scrapeClip;
    [SerializeField, Range(0f, 1f)] float scrapeVolume = 1f;
    [Tooltip("A spilled box that slides across the floor while the scraping plays (optional).")]
    [SerializeField] Transform scrapeBox;
    [SerializeField] Vector3 scrapeSlide = new Vector3(0f, 0f, 0.9f);
    [Tooltip("The fuse shows up this many seconds after the scraping starts.")]
    [SerializeField] float fuseRevealOffset = 2.2f;

    [Header("Audio")]
    [SerializeField] AudioMixerGroup sfxGroup;

    public bool HuntRunning { get; private set; }
    public bool Busy { get { return running || queue.Count > 0; } }

    readonly Queue<Func<IEnumerator>> queue = new Queue<Func<IEnumerator>>();
    readonly List<AudioSource> pool = new List<AudioSource>();
    readonly List<ScareProp> props = new List<ScareProp>();
    bool running;
    bool crashPending;          // blackout started, crash not heard yet
    float lockedUntil;          // nothing starts before this time
    float nextAmbientAt;
    int lastAmbient = -1;
    int searches;
    Camera cam;
    Transform playerT;

    void Awake()
    {
        if (hunt != null) hunt.revealIsHandledByDirector = true;
    }

    void Start()
    {
        if (hunt != null)
        {
            hunt.SearchCounted += OnSearchCounted;
            hunt.onFusePickedUp.AddListener(OnFusePickedUp);
            hunt.onProblemIdentified.AddListener(OnProblemIdentified);
            if (hunt.ProblemIdentified) OnProblemIdentified();
        }
        if (blackout != null)
        {
            blackout.onBlackout.AddListener(OnBlackout);
            blackout.onCrash.AddListener(OnCrash);
        }
    }

    void OnDestroy()
    {
        if (hunt != null) hunt.SearchCounted -= OnSearchCounted;
    }

    // ---------------------------------------------------------------- flow

    void OnProblemIdentified()
    {
        if (hunt != null && hunt.FusePickedUp) return;
        HuntRunning = true;
        ScheduleNextAmbient(Time.time);
        Debug.Log("[FuseHuntDirector] the player knows what is wrong - the cabinets count, dread events begin");
    }

    void OnBlackout()
    {
        if (beginHuntOnBlackout && hunt != null) { hunt.BeginHunt(); HuntRunning = true; }
        if (!HuntRunning) return;
        crashPending = true;
        lockedUntil = float.MaxValue;   // wait for the crash
    }

    void OnCrash()
    {
        if (!HuntRunning) return;
        crashPending = false;
        lockedUntil = Time.time + quietAfterCrash;
        ScheduleNextAmbient(lockedUntil);
    }

    void OnFusePickedUp()
    {
        HuntRunning = false;
        queue.Clear();
    }

    void ScheduleNextAmbient(float from)
    {
        nextAmbientAt = from + Random.Range(ambientInterval.x, ambientInterval.y);
    }

    void OnSearchCounted(int n, int total, SearchableCabinet cabinet)
    {
        searches = n;
        Debug.Log("[FuseHuntDirector] search " + n + "/" + total + " (" + cabinet.displayName + ")");
        Func<IEnumerator> ev;
        // the last search must not wait behind the earlier events: drop anything that has not started yet
        if (n >= total) queue.Clear();
        if (n >= total) ev = Co_StorageScrape;
        else if (n == 1) ev = UnityEngine.Random.value < 0.5f ? (Func<IEnumerator>)Co_CanRoll : Co_ItemFall;
        else ev = Co_ClearWhisper;
        StartCoroutine(Co_Enqueue(ev, searchEventDelay));
    }

    IEnumerator Co_Enqueue(Func<IEnumerator> ev, float delay)
    {
        yield return new WaitForSeconds(delay);
        queue.Enqueue(ev);
    }

    void Update()
    {
        if (!HuntRunning || running) return;
        if (GameManager.IsPaused || Time.time < lockedUntil) return;

        if (queue.Count > 0)
        {
            if (!DreadEvents.TryAcquire()) return;
            StartCoroutine(Co_Run(queue.Dequeue()));
        }
        else if (ambientEvents && !crashPending && hunt != null && !hunt.FinalSearchDone && Time.time >= nextAmbientAt)
        {
            if (!DreadEvents.TryAcquire()) return;
            StartCoroutine(Co_Run(PickAmbient()));
        }
    }

    IEnumerator Co_Run(Func<IEnumerator> ev)
    {
        running = true;
        yield return ev();
        running = false;
        DreadEvents.Release(minGapAfterEvent);
        lockedUntil = Time.time + minGapAfterEvent;
        ScheduleNextAmbient(lockedUntil);
    }

    Func<IEnumerator> PickAmbient()
    {
        // weights: can roll 3, item fall 3, storage thump 2, faint whisper 1 (only after the first clear whisper)
        int[] w = { 3, 3, 2, searches >= 2 ? 1 : 0 };
        if (lastAmbient >= 0) w[lastAmbient] = 0;
        int total = 0; foreach (var x in w) total += x;
        int r = Random.Range(0, Mathf.Max(1, total));
        int pick = 0;
        for (int i = 0; i < w.Length; i++) { if (r < w[i]) { pick = i; break; } r -= w[i]; }
        lastAmbient = pick;
        switch (pick)
        {
            case 0: return Co_CanRoll;
            case 1: return Co_ItemFall;
            case 2: return Co_StorageThump;
            default: return Co_FaintWhisper;
        }
    }

    // ---------------------------------------------------------------- events

    IEnumerator Co_CanRoll()
    {
        Debug.Log("[FuseHuntDirector] event: can rolls out of another aisle");
        if (!FindPoint(propDistance.x, propDistance.y, false, out Vector3 floorPos)) { yield return Co_StorageThump(); yield break; }

        // roll along whichever direction has the most room
        Vector3 best = Vector3.forward; float bestFree = -1f;
        for (int i = 0; i < 8; i++)
        {
            float a = (i + Random.value * 0.5f) * 45f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            float free = Physics.Raycast(floorPos + Vector3.up * 0.1f, d, out RaycastHit h, 7f, QueryMask, QueryTriggerInteraction.Ignore) ? h.distance : 7f;
            if (free > bestFree) { bestFree = free; best = d; }
        }
        var prop = SpawnProp(floorPos + Vector3.up * 0.06f);
        if (prop == null) yield break;
        var rb = prop.GetComponent<Rigidbody>();
        Vector3 side = Vector3.Cross(Vector3.up, best);
        AlignAxis(prop, side);
        rb.linearVelocity = best * rollSpeed;
        rb.angularVelocity = side * (rollSpeed / Mathf.Max(0.02f, prop.radius));

        float t = 0f;
        while (t < 5f && prop != null && !(t > 1f && prop.Settled)) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator Co_ItemFall()
    {
        Debug.Log("[FuseHuntDirector] event: something falls off a shelf");
        if (!FindShelfPoint(out Vector3 pos, out Vector3 outward)) { yield return Co_CanRoll(); yield break; }
        if (shelfRattleClip != null) Play3D(shelfRattleClip, pos, shelfRattleVolume, 3f, 30f);
        yield return new WaitForSeconds(0.25f);
        var prop = SpawnProp(pos);
        if (prop == null) yield break;
        var rb = prop.GetComponent<Rigidbody>();
        rb.linearVelocity = outward * 0.7f;
        rb.angularVelocity = Random.onUnitSphere * 4f;
        float t = 0f;
        while (t < 4.5f && prop != null && !(t > 1f && prop.Settled)) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator Co_StorageThump()
    {
        Debug.Log("[FuseHuntDirector] event: a dull thump from the storage room");
        if (storageThumps != null && storageThumps.Length > 0 && storagePoint != null)
        {
            var c = storageThumps[Random.Range(0, storageThumps.Length)];
            Play3D(c, storagePoint.position + Random.insideUnitSphere * 1.2f, 0.9f, 4f, 50f);
            yield return new WaitForSeconds(c.length);
        }
    }

    IEnumerator Co_ClearWhisper()
    {
        Debug.Log("[FuseHuntDirector] event: the first clear whisper");
        yield return Whisper(0, clearWhisperVolume, whisperDistance, 6500f);
    }

    IEnumerator Co_FaintWhisper()
    {
        Debug.Log("[FuseHuntDirector] event: a faint whisper");
        int i = whisperClips != null && whisperClips.Length > 1 ? Random.Range(1, whisperClips.Length) : 0;
        yield return Whisper(i, faintWhisperVolume, new Vector2(whisperDistance.y, whisperDistance.y + 5f), 3200f);
    }

    IEnumerator Whisper(int clipIndex, float volume, Vector2 dist, float lowPass)
    {
        if (whisperClips == null || whisperClips.Length == 0) yield break;
        var clip = whisperClips[Mathf.Clamp(clipIndex, 0, whisperClips.Length - 1)];

        // somewhere the player cannot see: behind or beside them, behind cover if possible
        if (!FindPoint(dist.x, dist.y, true, out Vector3 floorPos)) floorPos = PlayerPos() - CamForward() * dist.x;
        Vector3 pos = floorPos + Vector3.up * 1.3f;

        var src = Play3D(clip, pos, volume, 3f, 26f, lowPass);
        if (src == null) yield break;

        // slow drift sideways so the direction is never quite certain
        Vector3 tangent = Vector3.Cross(Vector3.up, (pos - PlayerPos()).normalized) * (Random.value < 0.5f ? 1f : -1f);
        float t = 0f;
        while (t < clip.length && src != null && src.isPlaying)
        {
            t += Time.deltaTime;
            src.transform.position = pos + tangent * (whisperDrift * t / clip.length);
            yield return null;
        }
    }

    IEnumerator Co_StorageScrape()
    {
        Debug.Log("[FuseHuntDirector] event: boxes scrape across the storage room floor - fuse follows");
        Vector3 at = storagePoint != null ? storagePoint.position : transform.position;
        if (scrapeBox != null) at = scrapeBox.position;
        float len = 4f;
        if (scrapeClip != null) { Play3D(scrapeClip, at, scrapeVolume, 5f, 60f); len = scrapeClip.length; }

        StartCoroutine(Co_SlideBox(len));
        yield return new WaitForSeconds(Mathf.Min(fuseRevealOffset, len));
        if (hunt != null) hunt.RevealPendingFuse();
        yield return new WaitForSeconds(Mathf.Max(0f, len - fuseRevealOffset));
    }

    // jerky drag: short slides separated by pauses
    IEnumerator Co_SlideBox(float length)
    {
        if (scrapeBox == null) yield break;
        Vector3 from = scrapeBox.position;
        const int steps = 4;
        float stepTime = length / (steps * 1.8f);
        for (int s = 0; s < steps; s++)
        {
            Vector3 a = from + scrapeSlide * (s / (float)steps);
            Vector3 b = from + scrapeSlide * ((s + 1) / (float)steps);
            float t = 0f;
            while (t < stepTime) { t += Time.deltaTime; scrapeBox.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / stepTime)); yield return null; }
            yield return new WaitForSeconds(stepTime * 0.8f);
        }
        scrapeBox.position = from + scrapeSlide;
    }

    // ---------------------------------------------------------------- props

    ScareProp SpawnProp(Vector3 pos)
    {
        if (propPrefabs == null || propPrefabs.Length == 0) return null;
        // keep the number of loose props bounded
        props.RemoveAll(p => p == null);
        while (props.Count >= maxProps) { Destroy(props[0].gameObject); props.RemoveAt(0); }

        var go = Instantiate(propPrefabs[Random.Range(0, propPrefabs.Length)], pos, Quaternion.identity);
        go.name = "ScareProp";
        var rend = go.GetComponentInChildren<Renderer>();
        var b = rend != null ? rend.bounds : new Bounds(pos, Vector3.one * 0.1f);
        Vector3 sz = b.size;

        // long axis in world space (the prefab is spawned unrotated)
        int axis = sz.x > sz.y ? (sz.x > sz.z ? 0 : 2) : (sz.y > sz.z ? 1 : 2);
        float longest = sz[axis];
        float radius = Mathf.Max(0.015f, (sz[(axis + 1) % 3] + sz[(axis + 2) % 3]) * 0.25f);
        Vector3 axisDir = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = axis;
        col.radius = radius / go.transform.lossyScale.x;
        col.height = longest / go.transform.lossyScale.x;
        col.center = go.transform.InverseTransformPoint(b.center);
        var mat = new PhysicsMaterial("prop") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.35f, bounceCombine = PhysicsMaterialCombine.Maximum };
        col.material = mat;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.linearDamping = 0.45f;   // a can stops after roughly 4 m
        rb.angularDamping = 0.3f;
        rb.maxAngularVelocity = 120f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var sp = go.AddComponent<ScareProp>();
        sp.hitClips = hitClips; sp.tinkClips = tinkClips; sp.rollLoop = rollLoop; sp.sfxGroup = sfxGroup; sp.radius = radius;
        axisLocalCache = axisDir;
        props.Add(sp);
        return sp;
    }

    Vector3 axisLocalCache = Vector3.up;

    // the store has a big 'RainBlocker' collider over it that is not a real obstacle
    static int QueryMask { get { return ~LayerMask.GetMask("RainBlocker"); } }

    // turn the prop so its long axis points along 'dir' (so a can lies on its side and rolls)
    void AlignAxis(ScareProp p, Vector3 dir)
    {
        p.transform.rotation = Quaternion.FromToRotation(axisLocalCache, dir.normalized) * p.transform.rotation;
    }

    // ---------------------------------------------------------------- finding places

    Vector3 PlayerPos()
    {
        if (playerT == null) { var p = FindObjectOfType<Player>(); if (p != null) playerT = p.transform; }
        return playerT != null ? playerT.position : transform.position;
    }

    Vector3 CamForward()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector3.forward;
        Vector3 f = cam.transform.forward; f.y = 0f;
        return f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
    }

    Vector3 EyePos()
    {
        if (cam == null) cam = Camera.main;
        return cam != null ? cam.transform.position : PlayerPos() + Vector3.up * 1.6f;
    }

    bool Valid(Vector3 floor)
    {
        if (interiorZone != null && !interiorZone.bounds.Contains(floor + Vector3.up * 1f)) return false;
        if (excludedVolume.Contains(floor + Vector3.up * 1f)) return false;
        if (Physics.CheckSphere(floor + Vector3.up * 0.25f, 0.18f, QueryMask, QueryTriggerInteraction.Ignore)) return false;
        return true;
    }

    /// <summary>A free floor point in the store, 'min..max' metres from the player, out of their line of sight.</summary>
    bool FindPoint(float min, float max, bool preferBehind, out Vector3 result)
    {
        Vector3 pp = PlayerPos();
        Vector3 fwd = CamForward();
        Vector3 eye = EyePos();
        float bestScore = float.MinValue; result = pp; bool found = false;
        for (int i = 0; i < 60; i++)
        {
            float ang = Random.value * 360f;
            Vector3 d = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            float dist = Random.Range(min, max);
            // start below shelf tops / ceiling, shoot down, and only accept the store floor (not the terrain outside)
            Vector3 probe = pp + d * dist + Vector3.up * 1.5f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 4f, QueryMask, QueryTriggerInteraction.Ignore)) continue;
            if (hit.collider is TerrainCollider) continue;
            if (Mathf.Abs(hit.point.y - pp.y) > 0.3f) continue;
            if (!Valid(hit.point)) continue;

            bool hidden = Physics.Linecast(eye, hit.point + Vector3.up * 0.5f, QueryMask, QueryTriggerInteraction.Ignore);
            float dot = Vector3.Dot(fwd, d);
            float score = 0f;
            if (hidden) score += 2f;
            if (preferBehind) score += (1f - dot) * 1.2f; else score += (dot < 0.2f ? 0.5f : 0f);
            // nobody should see the spot: if it is not hidden it must at least be behind the player
            if (!hidden && dot > -0.2f) score -= 5f;
            score += Random.value * 0.3f;
            if (score > bestScore) { bestScore = score; result = hit.point; found = true; }
        }
        return found;
    }

    /// <summary>Spot next to something tall (a shelf) that is out of sight; 'outward' points away from it.</summary>
    bool FindShelfPoint(out Vector3 pos, out Vector3 outward)
    {
        pos = PlayerPos(); outward = Vector3.forward;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (!FindPoint(propDistance.x, propDistance.y, false, out Vector3 floorPos)) continue;
            float h = Random.Range(0.9f, 1.5f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 d = Quaternion.Euler(0f, k * 90f + Random.value * 40f, 0f) * Vector3.forward;
                Vector3 origin = floorPos + Vector3.up * h;
                if (Physics.Raycast(origin, d, out RaycastHit hit, 1.6f, QueryMask, QueryTriggerInteraction.Ignore) && hit.collider.bounds.size.y > 1.2f)
                {
                    pos = hit.point - d * 0.2f; pos.y = origin.y;
                    outward = -d;
                    return true;
                }
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- audio

    AudioSource Play3D(AudioClip clip, Vector3 pos, float volume, float minDist, float maxDist, float lowPassHz = 0f)
    {
        if (clip == null) return null;
        AudioSource a = null;
        foreach (var s in pool) if (s != null && !s.isPlaying) { a = s; break; }
        if (a == null)
        {
            var go = new GameObject("DreadSfx");
            go.transform.SetParent(transform, false);
            a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = false; a.dopplerLevel = 0f;
            a.outputAudioMixerGroup = sfxGroup;
            pool.Add(a);
        }
        a.transform.position = pos;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = minDist; a.maxDistance = maxDist;
        a.volume = volume; a.pitch = 1f;
        a.clip = clip;
        var lp = a.GetComponent<AudioLowPassFilter>();
        if (lowPassHz > 0f)
        {
            if (lp == null) lp = a.gameObject.AddComponent<AudioLowPassFilter>();
            lp.enabled = true; lp.cutoffFrequency = lowPassHz;
        }
        else if (lp != null) lp.enabled = false;
        a.Play();
        return a;
    }

    /// <summary>For other scripts: something small falls off a shelf somewhere out of the player's sight (waits until it has played).</summary>
    public IEnumerator ItemFallRoutine() { return Co_ItemFall(); }

    [ContextMenu("Test: can roll")] void TestCan() { HuntRunning = true; lockedUntil = 0f; queue.Enqueue(Co_CanRoll); }
    [ContextMenu("Test: item fall")] void TestFall() { HuntRunning = true; lockedUntil = 0f; queue.Enqueue(Co_ItemFall); }
    [ContextMenu("Test: whisper")] void TestWhisper() { HuntRunning = true; lockedUntil = 0f; queue.Enqueue(Co_ClearWhisper); }
    [ContextMenu("Test: storage scrape")] void TestScrape() { HuntRunning = true; lockedUntil = 0f; queue.Enqueue(Co_StorageScrape); }
}
