using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sits on the "Store Makeover" root. Remembers every original thing the makeover hid or changed (renderers, colliders,
/// objects, material slots, moved props) so Tools > Store Makeover > Remove can put the scene back exactly. Editor data only.
/// </summary>
public class StoreMakeoverRecord : MonoBehaviour
{
    [System.Serializable]
    public class MaterialSwap
    {
        public Renderer renderer;
        public int slot;
        public Material original;
    }

    [System.Serializable]
    public class Move
    {
        public Transform target;
        public Vector3 originalPosition;
        public bool hasRotation;
        public Quaternion originalRotation;
    }

    [TextArea] public string note = "Built by Tools > Store Makeover. Use Remove to undo everything (originals come back).";
    public List<Renderer> hiddenRenderers = new List<Renderer>();
    public List<Collider> disabledColliders = new List<Collider>();
    public List<GameObject> deactivatedObjects = new List<GameObject>();
    public List<MaterialSwap> materialSwaps = new List<MaterialSwap>();
    public List<Move> moves = new List<Move>();
    [System.Serializable]
    public class Reparent
    {
        public Transform target;
        public Transform originalParent;
        public Vector3 originalLocalPosition;
    }

    [System.Serializable]
    public class RefChange
    {
        public Object component;
        public string property;
        public Object original;
    }

    public List<RefChange> refChanges = new List<RefChange>();   // serialized references pointed at a new object (e.g. the register's screen)
    [System.Serializable]
    public class BoolChange
    {
        public Object component;
        public string property;
        public bool original;
    }

    public List<BoolChange> boolChanges = new List<BoolChange>();   // serialized switches turned off (e.g. the old shelf collapse adding colliders)
    public List<Reparent> reparented = new List<Reparent>();   // e.g. the fridge hums, moved from the old fridges onto the new ones
    public List<Rigidbody> madeKinematic = new List<Rigidbody>();   // pickups on the new shelves: physics would push them out of the fitting's collider
}
