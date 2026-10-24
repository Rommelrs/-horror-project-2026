using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;

// A locked wall box (e.g. a circuit box). Without the key the player only hears it rattle ("It's locked.");
// with the key the key is used up, the door swings open and the things inside (e.g. a key pickup that was hidden until now) appear.
// The opened state is saved through SaveableInteractable (needs a UniqueID too), so a loaded game shows the box already open.
// A beacon on the box (red while locked, green once opened) blinks gently so the box can be found from a distance.
public class CircuitBoxInteractable : Interactable
{
    [Header("Key")]
    [Tooltip("The key item that opens this box.")]
    [SerializeField] Item requiredKey;
    [Tooltip("On: the key is removed from the inventory when it opens the box (the note says to leave it at the car).")]
    [SerializeField] bool consumeKey = true;

    [Header("Door")]
    [Tooltip("The hinged door (its pivot is the hinge).")]
    [SerializeField] Transform door;
    [Tooltip("Local Y rotation of the open door. Positive swings it outward to the viewer's left.")]
    [SerializeField] float openAngle = 100f;
    [SerializeField] float openDuration = 0.9f;
    [Tooltip("Hidden until the box is open (e.g. the pickup that lives inside).")]
    [SerializeField] GameObject[] revealOnOpen;

    [Header("Beacon")]
    [SerializeField] Renderer beaconLens;
    [SerializeField] Renderer beaconGlow;
    [SerializeField] Light beaconLight;
    [SerializeField, ColorUsage(false, true)] Color lockedColor = new Color(1f, 0.08f, 0.04f);
    [SerializeField, ColorUsage(false, true)] Color openColor = new Color(0.1f, 1f, 0.25f);
    [Tooltip("How much the locked beacon dims at the bottom of its pulse (0 = steady, 1 = fully off).")]
    [SerializeField, Range(0f, 1f)] float pulseDepth = 0.45f;
    [SerializeField] float pulseSpeed = 2.4f;
    [Tooltip("Light intensity at full brightness.")]
    [SerializeField] float lightIntensity = 1.1f;

    [Header("Audio")]
    [SerializeField] AudioClip lockedClip;
    [SerializeField] AudioClip unlockClip;
    [SerializeField] AudioClip openClip;

    [Header("Lines")]
    [SerializeField] LocalizedString lockedLine;
    [SerializeField] LocalizedString unlockedLine;

    [Header("Events")]
    [Tooltip("Fires when the key opens the box (not when a saved, already-open box is loaded).")]
    [SerializeField] UnityEvent onOpened;

    static readonly int ColorId = Shader.PropertyToID("_Color");

    AudioSource audioSource;
    Collider coll;
    SaveableInteractable saveable;
    MaterialPropertyBlock lensBlock, glowBlock;
    bool isOpen;
    bool busy;
    bool playerInRange;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        coll = GetComponent<Collider>();
        saveable = GetComponent<SaveableInteractable>();
        lensBlock = new MaterialPropertyBlock();
        glowBlock = new MaterialPropertyBlock();
        SetBeacon(lockedColor, 1f);
        SetRevealed(false);
    }

    void Start()
    {
        if (saveable != null && saveable.WasAlreadyUsed())
            RestoreOpenState();
    }

    void Update()
    {
        float k = 1f;
        if (!isOpen && pulseDepth > 0f)
            k = 1f - pulseDepth * (0.5f - 0.5f * Mathf.Cos(Time.time * pulseSpeed));
        SetBeacon(isOpen ? openColor : lockedColor, k);
    }

    public override void Interacted()
    {
        base.Interacted();

        if (isOpen || busy) return;

        if (!PlayerHasKey())
        {
            Play(lockedClip);
            Say(lockedLine);
            // after any interact the handler drops this box from its list: put it back so E can be pressed again without leaving the trigger
            StartCoroutine(Co_Retarget());
            return;
        }

        StartCoroutine(Co_Open());
    }

    bool PlayerHasKey()
    {
        if (requiredKey == null || Player.instance == null || Player.instance.inventory == null) return false;
        foreach (var stack in Player.instance.inventory.GetItems())
            if (stack.item == requiredKey) return true;
        return false;
    }

    IEnumerator Co_Open()
    {
        busy = true;

        if (consumeKey && requiredKey != null)
            Player.instance.inventory.RemoveItem(requiredKey);

        if (saveable != null) saveable.MarkAsUsed();
        else Debug.LogWarning("[CircuitBox] '" + name + "' has no SaveableInteractable: its opened state will not be saved.", this);

        Play(unlockClip);
        Say(unlockedLine);

        yield return new WaitForSeconds(0.45f);

        isOpen = true;
        Play(openClip);
        if (door != null)
            door.DOLocalRotate(new Vector3(0f, openAngle, 0f), openDuration).SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(openDuration * 0.4f);
        SetRevealed(true);

        // nothing left to press E on
        if (coll != null) coll.enabled = false;

        busy = false;
        onOpened?.Invoke();
    }

    void RestoreOpenState()
    {
        isOpen = true;
        if (door != null) door.localRotation = Quaternion.Euler(0f, openAngle, 0f);
        SetRevealed(true);
        if (coll != null) coll.enabled = false;
    }

    void SetRevealed(bool revealed)
    {
        if (revealOnOpen == null) return;
        foreach (var go in revealOnOpen)
            if (go != null) go.SetActive(revealed);
    }

    void SetBeacon(Color c, float k)
    {
        if (beaconLens != null)
        {
            beaconLens.GetPropertyBlock(lensBlock);
            lensBlock.SetColor(ColorId, c * k);
            beaconLens.SetPropertyBlock(lensBlock);
        }
        if (beaconGlow != null)
        {
            beaconGlow.GetPropertyBlock(glowBlock);
            glowBlock.SetColor(ColorId, c * k);
            beaconGlow.SetPropertyBlock(glowBlock);
        }
        if (beaconLight != null)
        {
            beaconLight.color = c;
            beaconLight.intensity = lightIntensity * k;
        }
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void Say(LocalizedString line)
    {
        if (line == null || line.IsEmpty) return;
        // the subtitle's "continue" button is the same E: stamp the interaction time so the press that opened it doesn't close it again
        if (Player.instance != null) Player.instance.SetLastInteractionTime(Time.unscaledTime);
        SubtitleManager.ShowSubtitle(line, false);
    }

    IEnumerator Co_Retarget()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (playerInRange && !isOpen && InteractionHandler.instance != null)
            InteractionHandler.instance.InspectableItemTriggerEnter(this);
    }

    public override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
        if (other.CompareTag("Player")) playerInRange = true;
    }

    public override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);
        if (other.CompareTag("Player")) playerInRange = false;
    }
}
