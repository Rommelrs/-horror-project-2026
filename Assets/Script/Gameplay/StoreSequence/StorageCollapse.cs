using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The tall metal shelving unit in the storage room topples over. Fully scripted (no physics): the shelf pivots about its
/// base edge, ease-in like a falling weight, hits the floor and rebounds slightly; everything that stood on it slides off,
/// drops and ends up as a spill of boxes on the floor. A "gap" object behind the shelf is revealed.
/// The final poses are computed up front so the spill always lands on the floor and the result is the same on every run.
/// </summary>
public class StorageCollapse : MonoBehaviour
{
    [Header("Shelf")]
    [SerializeField] Transform shelf;
    [Tooltip("Direction the shelf falls in (world space, horizontal).")]
    [SerializeField] Vector3 fallDirection = new Vector3(-1f, 0f, 0f);
    [Tooltip("How far the base edge of the shelf is from its centre in the fall direction (half its depth).")]
    [SerializeField] float hingeOffset = 0.31f;
    [Tooltip("Height of the floor under the shelf. Detected automatically when left at -9999.")]
    [SerializeField] float floorY = -9999f;
    [SerializeField] float fallSeconds = 1.15f;
    [Tooltip("Angle the shelf finally rests at (90 = flat on the floor). A bit less looks like it came to rest on the spilled boxes.")]
    [SerializeField] float restAngle = 82f;
    [SerializeField] float reboundDegrees = 5f;
    [SerializeField] float yawDegrees = 3f;
    [Tooltip("Colliders of the intact shelf (switched off when it falls).")]
    [SerializeField] Collider[] intactColliders;

    [Header("Things that were on the shelf")]
    [SerializeField] Transform[] items;
    [Tooltip("Boxes begin to slide off after this fraction of the fall.")]
    [SerializeField] Vector2 slideStart = new Vector2(0.25f, 0.55f);
    [SerializeField] Vector2 flightSeconds = new Vector2(0.45f, 0.8f);
    [Tooltip("How far (metres, in the fall direction) the items spread.")]
    [SerializeField] Vector2 spread = new Vector2(1.8f, 5.0f);
    [SerializeField] float sideScatter = 1.2f;
    [SerializeField] int seed = 17;

    [Tooltip("Parts that must travel with an item but live in a prefab (bottle caps ...). followers[i] stays attached to followerLeaders[i].")]
    [SerializeField] Transform[] followers;
    [SerializeField] Transform[] followerLeaders;

    [Header("Colliders")]
    [Tooltip("The fallen shelf gets a box collider, so it is solid.")]
    [SerializeField] bool addShelfCollider = true;
    [Tooltip("Spilled boxes get box colliders once they have settled (items smaller than this, in metres, stay non-solid).")]
    [SerializeField] bool addItemColliders = true;
    [SerializeField] float minItemColliderSize = 0.35f;

    [Header("Gap behind the shelf")]
    [SerializeField] GameObject gap;

    [Header("Events")]
    public UnityEvent onImpact;
    public UnityEvent onSettled;

    public bool Collapsed { get; private set; }

    struct Item
    {
        public Transform t;
        public Vector3 p0, p1;
        public Quaternion r0, r1;
        public float delay, duration;
    }

    Item[] data;
    Vector3[] followerPos;
    Quaternion[] followerRot;
    Vector3 shelfPos0;
    Quaternion shelfRot0;
    Vector3 hinge;
    Vector3 dirFlat;
    Vector3 axis;

    void Awake()
    {
        if (gap != null) gap.SetActive(false);
        Prepare();
    }

    void Prepare()
    {
        if (shelf == null) return;
        dirFlat = fallDirection; dirFlat.y = 0f;
        dirFlat = dirFlat.sqrMagnitude < 0.001f ? Vector3.left : dirFlat.normalized;
        shelfPos0 = shelf.position;
        shelfRot0 = shelf.rotation;

        if (floorY < -9000f)
        {
            floorY = shelfPos0.y;
            Vector3 probe = shelfPos0 + dirFlat * 1.8f + Vector3.up * 0.5f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                floorY = hit.point.y;
        }

        float halfHeight = 0f;
        var rs = shelf.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); halfHeight = b.extents.y; }
        // hinge = bottom edge of the shelf on the side it falls towards
        hinge = new Vector3(shelfPos0.x, shelfPos0.y - halfHeight, shelfPos0.z) + dirFlat * hingeOffset;
        // rotating "up" towards dirFlat
        axis = Vector3.Cross(Vector3.up, dirFlat).normalized;

        // remember where each follower sits relative to its leader
        int fc = (followers != null && followerLeaders != null) ? Mathf.Min(followers.Length, followerLeaders.Length) : 0;
        followerPos = new Vector3[fc]; followerRot = new Quaternion[fc];
        for (int i = 0; i < fc; i++)
        {
            if (followers[i] == null || followerLeaders[i] == null) continue;
            followerPos[i] = followerLeaders[i].InverseTransformPoint(followers[i].position);
            followerRot[i] = Quaternion.Inverse(followerLeaders[i].rotation) * followers[i].rotation;
        }

        // work out where each item ends up
        var rng = new System.Random(seed);
        data = new Item[items == null ? 0 : items.Length];
        Vector3 side = Vector3.Cross(Vector3.up, dirFlat);
        for (int i = 0; i < data.Length; i++)
        {
            var t = items[i];
            if (t == null) continue;
            Item it = new Item { t = t, p0 = t.position, r0 = t.rotation };

            float along = Mathf.Lerp(spread.x, spread.y, (float)rng.NextDouble());
            float across = ((float)rng.NextDouble() * 2f - 1f) * sideScatter;
            Vector3 target = it.p0 + dirFlat * along + side * across;
            float yaw = (float)rng.NextDouble() * 360f;
            // mostly flat, a few tipped over
            float tiltA = rng.NextDouble() < 0.3 ? 40f + (float)rng.NextDouble() * 50f : (float)rng.NextDouble() * 12f;
            float tiltDir = (float)rng.NextDouble() * 360f;
            Quaternion rot = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(tiltA, Quaternion.AngleAxis(tiltDir, Vector3.up) * Vector3.right) * it.r0;

            // place it, measure the bounds, drop it onto the floor
            t.SetPositionAndRotation(target, rot);
            float minY = float.MaxValue;
            var trs = t.GetComponentsInChildren<Renderer>();
            foreach (var r in trs) minY = Mathf.Min(minY, r.bounds.min.y);
            if (trs.Length == 0) minY = target.y;
            target.y += floorY - minY + 0.005f;
            it.p1 = target;
            it.r1 = rot;
            it.delay = Mathf.Lerp(slideStart.x, slideStart.y, (float)rng.NextDouble()) * fallSeconds;
            it.duration = Mathf.Lerp(flightSeconds.x, flightSeconds.y, (float)rng.NextDouble());
            t.SetPositionAndRotation(it.p0, it.r0);
            data[i] = it;
        }
    }

    [ContextMenu("Collapse now")]
    public void Collapse()
    {
        if (Collapsed || shelf == null) return;
        Collapsed = true;
        if (intactColliders != null)
            foreach (var c in intactColliders) if (c != null) c.enabled = false;
        if (gap != null) gap.SetActive(true);
        StartCoroutine(Co_Collapse());
    }

    // snap everything to the post-collapse state (used by save/load or debugging)
    public void SnapToCollapsed()
    {
        Collapsed = true;
        if (intactColliders != null)
            foreach (var c in intactColliders) if (c != null) c.enabled = false;
        if (gap != null) gap.SetActive(true);
        SetShelfAngle(restAngle);
        foreach (var it in data) if (it.t != null) it.t.SetPositionAndRotation(it.p1, it.r1);
        UpdateFollowers();
        AddShelfCollider();
        AddItemColliders();
    }

    bool shelfColliderAdded, itemCollidersAdded;

    void AddShelfCollider()
    {
        if (!addShelfCollider || shelfColliderAdded || shelf == null) return;
        shelfColliderAdded = true;
        var mf = shelf.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var bc = shelf.gameObject.AddComponent<BoxCollider>();
        bc.center = mf.sharedMesh.bounds.center;
        bc.size = mf.sharedMesh.bounds.size;
    }

    void AddItemColliders()
    {
        if (!addItemColliders || itemCollidersAdded || data == null) return;
        itemCollidersAdded = true;
        foreach (var it in data)
        {
            if (it.t == null || it.t.GetComponent<Collider>() != null) continue;
            var mf = it.t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, it.t.lossyScale);
            if (Mathf.Max(size.x, size.y, size.z) < minItemColliderSize) continue;
            var bc = it.t.gameObject.AddComponent<BoxCollider>();
            bc.center = mf.sharedMesh.bounds.center;
            bc.size = mf.sharedMesh.bounds.size;
        }
    }

    void UpdateFollowers()
    {
        if (followerPos == null) return;
        for (int i = 0; i < followerPos.Length; i++)
        {
            if (followers[i] == null || followerLeaders[i] == null) continue;
            followers[i].SetPositionAndRotation(followerLeaders[i].TransformPoint(followerPos[i]), followerLeaders[i].rotation * followerRot[i]);
        }
    }

    void SetShelfAngle(float deg)
    {
        Quaternion q = Quaternion.AngleAxis(deg, axis) * Quaternion.AngleAxis(yawDegrees * Mathf.Clamp01(deg / Mathf.Max(1f, restAngle)), Vector3.up);
        shelf.position = hinge + q * (shelfPos0 - hinge);
        shelf.rotation = q * shelfRot0;
    }

    IEnumerator Co_Collapse()
    {
        float t = 0f;
        bool slid = false;
        // slow creak at the start, then gravity takes over
        while (t < fallSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallSeconds);
            float eased = Mathf.Pow(k, 2.4f);
            SetShelfAngle(restAngle * eased);
            slid = AnimateItems(t) || slid;
            yield return null;
        }
        SetShelfAngle(restAngle);
        AddShelfCollider();
        onImpact?.Invoke();

        // rebound
        float rb = 0.18f; t = 0f;
        while (t < rb * 2f)
        {
            t += Time.deltaTime;
            float bump = Mathf.Sin(Mathf.Clamp01(t / (rb * 2f)) * Mathf.PI) * reboundDegrees;
            SetShelfAngle(restAngle - bump);
            AnimateItems(fallSeconds + t);
            yield return null;
        }
        SetShelfAngle(restAngle);

        // wait for the last item
        float last = 0f;
        foreach (var it in data) last = Mathf.Max(last, it.delay + it.duration);
        while (t + fallSeconds < last + 0.05f)
        {
            t += Time.deltaTime;
            AnimateItems(fallSeconds + t);
            yield return null;
        }
        AnimateItems(last + 1f);
        AddItemColliders();
        onSettled?.Invoke();
    }

    bool AnimateItems(float time)
    {
        bool any = false;
        for (int i = 0; i < data.Length; i++)
        {
            var it = data[i];
            if (it.t == null) continue;
            float k = (time - it.delay) / it.duration;
            if (k <= 0f) continue;
            any = true;
            k = Mathf.Clamp01(k);
            Vector3 p = it.p0;
            p.x = Mathf.Lerp(it.p0.x, it.p1.x, k);
            p.z = Mathf.Lerp(it.p0.z, it.p1.z, k);
            // accelerate downwards like a falling object, with a little hop on landing
            p.y = Mathf.Lerp(it.p0.y, it.p1.y, k * k);
            if (k >= 1f) p.y = it.p1.y;
            it.t.SetPositionAndRotation(p, Quaternion.Slerp(it.r0, it.r1, Mathf.SmoothStep(0f, 1f, k)));
        }
        UpdateFollowers();
        return any;
    }
}
