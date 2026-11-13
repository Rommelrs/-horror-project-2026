using UnityEngine;

/// <summary>
/// A fight that has to be won to go on (the Runner between Alley 2 and 3) must not soft-lock a player who walks in with an empty
/// gun or on his last legs. While the enemy below is alive:
/// - the player owns the pistol but has no bullets left at all (magazine + spare): an ammo pickup lies on the road nearby;
/// - the player is low on health and carries no heals at all: a painkiller lies on the road nearby (painkillers only).
/// Each turns up at a spot the camera can't see right now; when both are out they lie together (a little trail), so finding
/// one finds the other. Picked up and needed again, another appears. Once the enemy is dead nothing more appears.
/// </summary>
public class EmergencyAmmo : MonoBehaviour
{
    [Tooltip("The enemy that has to die to go on. Nothing appears once it is dead (or gone).")]
    [SerializeField] Enemy guardian;
    [Tooltip("The ammo pickup that appears (an Ammo prefab).")]
    [SerializeField] GameObject pickupPrefab;
    [Tooltip("Only while the player owns this weapon (an empty-handed player fights with the pipe).")]
    [SerializeField] Item weaponItem;
    [SerializeField] float checkEvery = 1f;
    [Tooltip("Where they may turn up (out on the road around the fight). Each appears at one the camera can't see right now, " +
             "so the player never watches it pop in. Empty = this object's own position.")]
    [SerializeField] Transform[] spots;
    [Tooltip("Never closer to the player than this (metres), even out of view - he could turn round onto it.")]
    [SerializeField] float minPlayerDistance = 8f;

    [Header("Emergency painkiller")]
    [Tooltip("The heal pickup that appears (PainKillerPickup - painkillers only, never a med kit).")]
    [SerializeField] GameObject healPrefab;
    [Tooltip("Carrying any of these means the player can heal himself: nothing appears.")]
    [SerializeField] Item[] healItems;
    [Tooltip("Appears only at or below this share of max health.")]
    [Range(0.05f, 1f)] [SerializeField] float healBelow = 0.5f;
    [Tooltip("When ammo and painkiller are both out, the second lies this far from the first.")]
    [SerializeField] float pairGap = 1.4f;

    GameObject currentAmmo;
    GameObject currentHeal;
    Transform lastAmmoSpot, lastHealSpot;   // the next one of each kind turns up somewhere else
    float nextCheck;

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkEvery;

        if (guardian == null || guardian.health == null || guardian.health.IsDead) { enabled = false; return; }
        if (!guardian.gameObject.activeInHierarchy) return;

        var p = Player.instance;
        if (p == null || p.inventory == null) return;

        if (currentAmmo == null && pickupPrefab != null && NeedsAmmo(p))
            currentAmmo = Place(pickupPrefab, currentHeal, "ammo", ref lastAmmoSpot);
        if (currentHeal == null && healPrefab != null && NeedsHeal(p))
            currentHeal = Place(healPrefab, currentAmmo, "a painkiller", ref lastHealSpot);
    }

    bool NeedsAmmo(Player p)
    {
        if (p.playerWeaponSystem == null) return false;
        if (weaponItem != null && !p.inventory.HasItem(weaponItem)) return false;
        return p.playerWeaponSystem.currentAmmo <= 0 && p.playerWeaponSystem.GetReserveAmmo() <= 0;
    }

    bool NeedsHeal(Player p)
    {
        if (p.health == null || p.health.IsDead) return false;
        float max = p.health.GetMaxHealthValue();
        if (max <= 0f || p.health.GetHealthValue() > max * healBelow) return false;
        if (healItems != null)
            foreach (var item in healItems)
                if (item != null && p.inventory.HasItem(item)) return false;
        return true;
    }

    /// <summary>Next to <paramref name="partner"/> when that one is already lying out, else at the nearest spot out of view.</summary>
    GameObject Place(GameObject prefab, GameObject partner, string what, ref Transform lastSpot)
    {
        Vector3 pos; Quaternion rot;
        bool paired = false;
        pos = default; rot = Quaternion.identity;
        if (partner != null)
        {
            // a step from the first one, on the side towards the player: walk to one, the other is right there
            Vector3 toPlayer = Player.instance.transform.position - partner.transform.position;
            toPlayer.y = 0f;
            Vector3 dir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : partner.transform.right;
            pos = partner.transform.position + dir * pairGap;
            rot = partner.transform.rotation;
            // the player may be standing right by the first one by now; and never the area this kind used last time
            Transform area = NearestSpot(pos);
            paired = Hidden(pos) && (area == null || area != lastSpot);
            if (paired) lastSpot = area;
        }
        if (!paired)
        {
            Transform spot = PickHiddenSpot(partner, lastSpot);
            if (spot == null) return null;   // every other spot is in view / too close right now: try again next check
            pos = spot.position; rot = spot.rotation;
            lastSpot = spot;
        }
        var go = Instantiate(prefab, pos, rot, transform);
        go.name = "Emergency " + prefab.name;
        SitOnGround(go);
        Debug.Log("[EmergencyAmmo] " + guardian.name + " alive and the player needs " + what + ": placed at " + go.transform.position);
        return go;
    }

    /// <summary>The pickups' pivots aren't at their base (the painkiller's is well above it): rest the visible model on the road.</summary>
    void SitOnGround(GameObject go)
    {
        Bounds b = default; bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;
        float ground = GroundBelow(go.transform.position);
        go.transform.position += Vector3.up * (ground + 0.01f - b.min.y);
    }

    /// <summary>The road under a point, ignoring the pickups this object has put out.</summary>
    float GroundBelow(Vector3 p)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
        foreach (var h in hits)
            if (!h.collider.transform.IsChildOf(transform)) return h.point.y;
        return p.y;
    }

    Transform NearestSpot(Vector3 p)
    {
        Transform best = null; float bestD = float.MaxValue;
        if (spots != null)
            foreach (var s in spots)
                if (s != null && (s.position - p).sqrMagnitude < bestD) { bestD = (s.position - p).sqrMagnitude; best = s; }
        return best;
    }

    Transform PickHiddenSpot(GameObject taken, Transform notThis)
    {
        if (spots == null || spots.Length == 0) return Hidden(transform.position) ? transform : null;
        Transform best = null;
        float bestDist = float.MaxValue;
        Vector3 player = Player.instance.transform.position;
        foreach (var s in spots)
        {
            if (s == null || !Hidden(s.position)) continue;
            if (taken != null && (taken.transform.position - s.position).sqrMagnitude < 1f) continue;   // the other one lies there
            if (s == notThis && spots.Length > 1) continue;   // it turned up here last time: somewhere else now
            // the nearest one out of view: in the area, just not popping in in front of him
            float d = (s.position - player).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    /// <summary>Far enough from the player and not on screen: he never watches it pop in or turns round onto it.</summary>
    bool Hidden(Vector3 p)
    {
        Vector3 d = p - Player.instance.transform.position;
        d.y = 0f;
        if (d.magnitude < minPlayerDistance) return false;
        return !Seen(p);
    }

    bool Seen(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 target = p + Vector3.up * 0.2f;
        Vector3 v = cam.WorldToViewportPoint(target);
        if (v.z <= 0f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
        if (v.z > cam.farClipPlane) return false;
        // in the frame: hidden only if a wall is in the way. The third-person camera looks over Khalid's shoulder, so his own
        // body (and the pickups we put out) must not count as a wall - that let things spawn right in front of him.
        Vector3 from = cam.transform.position;
        Vector3 to = target - from;
        foreach (var h in Physics.RaycastAll(from, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (Player.instance != null && h.collider.transform.IsChildOf(Player.instance.transform)) continue;
            return false;
        }
        return true;
    }
}
