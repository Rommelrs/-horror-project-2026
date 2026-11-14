using UnityEngine;

/// <summary>
/// Keeps its target hidden until the store has power again (the fuse is in), then shows it for good.
/// Used for the blood trail to the storage room: in the blackout it can't be seen, with the lights back it can.
/// Works after a checkpoint load too, since it only looks at the fuse box state.
/// </summary>
public class ShowWhenPowered : MonoBehaviour
{
    [SerializeField] Fusebox fusebox;
    [SerializeField] GameObject target;

    bool shown;

    void Start()
    {
        if (fusebox == null) fusebox = FindFirstObjectByType<Fusebox>();
        if (target != null) target.SetActive(false);
    }

    void Update()
    {
        if (shown || fusebox == null || !fusebox.hasEnergy) return;
        shown = true;
        if (target != null) target.SetActive(true);
        enabled = false;
    }
}
