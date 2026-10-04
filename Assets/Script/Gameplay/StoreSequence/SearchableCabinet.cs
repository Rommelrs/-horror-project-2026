using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A cabinet / box the player can search with E. Pressing E opens all of its drawers / doors, pressing E again (or walking
/// away) closes them. Every time it opens it tells FuseHuntManager, which decides what the player finds / what Khalid says.
/// One cabinet = one search location (the fuse is placed in the last cabinet the player searches).
/// </summary>
public class SearchableCabinet : Interactable
{
    public static readonly List<SearchableCabinet> All = new List<SearchableCabinet>();

    [System.Serializable]
    public class Part
    {
        public Transform transform;
        [Tooltip("Added to the closed local position (for sliding drawers).")]
        public Vector3 openPositionOffset;
        [Tooltip("Euler angles applied on top of the closed local rotation (for hinged doors).")]
        public Vector3 openEulerOffset;
        [Tooltip("Seconds to wait before this part starts moving (staggers several drawers).")]
        public float delay;
        [HideInInspector] public Vector3 closedPos;
        [HideInInspector] public Quaternion closedRot;
    }

    [Header("Search")]
    public string displayName = "Cabinet";
    [Tooltip("Where the fuse appears if this turns out to be the last cabinet the player searches (put it inside a drawer / shelf).")]
    public Transform fuseSlot;

    [Header("Opening")]
    public List<Part> parts = new List<Part>();
    [SerializeField] float openDuration = 0.4f;
    [SerializeField] bool closeWhenPlayerLeaves = true;
    [SerializeField] float closeDelay = 0.6f;

    [Header("Audio")]
    [SerializeField] AudioClip openClip;
    [SerializeField] AudioClip closeClip;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    public bool IsOpen { get; private set; }

    bool busy;
    int insideCount;
    Coroutine autoCloseCR;

    void Awake()
    {
        foreach (var p in parts)
        {
            if (p.transform == null) continue;
            p.closedPos = p.transform.localPosition;
            p.closedRot = p.transform.localRotation;
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        SetSlotContentsInteractable(false);
    }

    public override void Interacted()
    {
        base.Interacted();
        if (busy) return;

        if (IsOpen)
        {
            Close();
        }
        else
        {
            // tell the hunt manager first, so a fuse can be placed in the slot before the drawers slide out
            if (FuseHuntManager.instance != null)
                FuseHuntManager.instance.NotifyOpened(this);
            Open();
        }

        StartCoroutine(Co_ReRegister());
    }

    // The interaction handler drops an interactable from its list right after it was used; add it back while the
    // player is still standing in front so E can close it again.
    IEnumerator Co_ReRegister()
    {
        yield return null;
        yield return null;
        if (insideCount > 0 && InteractionHandler.instance != null)
            InteractionHandler.instance.InspectableItemTriggerEnter(this);
    }

    public void Open()
    {
        if (IsOpen) return;
        if (autoCloseCR != null) { StopCoroutine(autoCloseCR); autoCloseCR = null; }
        IsOpen = true;
        PlayClip(openClip);
        StartCoroutine(Co_Move(true));
        onOpened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        SetSlotContentsInteractable(false);
        PlayClip(closeClip);
        StartCoroutine(Co_Move(false));
        onClosed?.Invoke();
    }

    IEnumerator Co_Move(bool open)
    {
        busy = true;
        float maxDelay = 0f;
        foreach (var p in parts) maxDelay = Mathf.Max(maxDelay, p.delay);
        float total = openDuration + maxDelay;
        float t = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            foreach (var p in parts)
            {
                if (p.transform == null) continue;
                float k = Mathf.Clamp01((t - p.delay) / Mathf.Max(0.01f, openDuration));
                k = k * k * (3f - 2f * k);
                if (!open) k = 1f - k;
                p.transform.localPosition = Vector3.LerpUnclamped(p.closedPos, p.closedPos + p.openPositionOffset, k);
                p.transform.localRotation = Quaternion.SlerpUnclamped(p.closedRot, p.closedRot * Quaternion.Euler(p.openEulerOffset), k);
            }
            yield return null;
        }
        foreach (var p in parts)
        {
            if (p.transform == null) continue;
            p.transform.localPosition = open ? p.closedPos + p.openPositionOffset : p.closedPos;
            p.transform.localRotation = open ? p.closedRot * Quaternion.Euler(p.openEulerOffset) : p.closedRot;
        }
        busy = false;
        if (open) SetSlotContentsInteractable(true);
    }

    /// <summary>Colliders of whatever sits in the fuse slot only work while the cabinet is open (so nothing can be grabbed through closed doors).</summary>
    public void SetSlotContentsInteractable(bool on)
    {
        if (fuseSlot == null) return;
        foreach (var c in fuseSlot.GetComponentsInChildren<Collider>(true))
            c.enabled = on;
    }

    public void RefreshSlotContents()
    {
        SetSlotContentsInteractable(IsOpen && !busy);
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (SoundEffectManager.instance != null)
            SoundEffectManager.instance.PlaySFXAtPosition(clip, transform.position);
        else
            AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    public override void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) insideCount++;
        base.OnTriggerEnter(other);
    }

    public override void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) insideCount = Mathf.Max(0, insideCount - 1);
        base.OnTriggerExit(other);
        if (insideCount == 0 && IsOpen && closeWhenPlayerLeaves && isActiveAndEnabled)
        {
            if (autoCloseCR != null) StopCoroutine(autoCloseCR);
            autoCloseCR = StartCoroutine(Co_AutoClose());
        }
    }

    IEnumerator Co_AutoClose()
    {
        yield return new WaitForSeconds(closeDelay);
        while (busy) yield return null;
        if (insideCount == 0) Close();
        autoCloseCR = null;
    }
}
