using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// The barber candy wrapper Adel drops while he runs off in the table-knockdown pre-cutscene (KnockdownPreCutscene).
/// Sits on the wrapper interactable (ImageFadeInteractable + SubtitleTrigger "Emerald Saloon?" + AutoInteractCutscene, the
/// old CandyWrapperInteractable's setup). Hidden and not interactable until the timeline reaches dropTime: then it leaves
/// Adel's hand, falls with his running speed and tumbles to rest at its spot. Once the knockdown is done in the save (or the
/// player has seen it) it just lies there from the start. Built by Tools > Cutscenes > Build Adel Wrapper Drop.
/// </summary>
public class AdelWrapperDrop : MonoBehaviour
{
    public const string SeenFlag = "AdelWrapperSeen";

    [SerializeField] PlayableDirector director;
    [SerializeField] Transform adelHand;
    [SerializeField] float dropTime = 3.1f;
    [SerializeField] Transform visual;
    [SerializeField] CinematicKnockdownSequence knockdown;
    [Tooltip("Speed he throws it with (his run, a little up) - world space.")]
    [SerializeField] Vector3 releaseVelocity = new Vector3(3.2f, 0.6f, 0f);
    [SerializeField] float gravity = 9.81f;
    [SerializeField] float settleTime = 0.35f;

    Collider coll;
    ImageFadeInteractable interactable;
    AutoInteractCutscene autoInteract;
    Vector3 restPos;
    Quaternion restRot;
    enum State { Waiting, Falling, Settling, Resting }
    State state;
    Vector3 pos, vel;
    Quaternion rot, spin;
    float settleT;
    Vector3 settleFrom;
    Quaternion settleFromRot;

    void Awake()
    {
        coll = GetComponent<Collider>();
        interactable = GetComponent<ImageFadeInteractable>();
        autoInteract = GetComponent<AutoInteractCutscene>();
        restPos = visual.position;
        restRot = visual.rotation;
    }

    void Start()
    {
        if (interactable != null) interactable.onImageClosed.AddListener(OnSeen);
        if (StoryFlags.Has(SeenFlag) && autoInteract != null) autoInteract.enabled = false;   // no second walk-over after a reload
        bool done = knockdown != null && knockdown.AlreadyDone;
        if (done || StoryFlags.Has(SeenFlag)) Rest();
        else
        {
            state = State.Waiting;
            visual.gameObject.SetActive(false);
            if (coll != null) coll.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (interactable != null) interactable.onImageClosed.RemoveListener(OnSeen);
    }

    void OnSeen() => StoryFlags.Set(SeenFlag);

    void Rest()
    {
        state = State.Resting;
        visual.SetPositionAndRotation(restPos, restRot);
        visual.gameObject.SetActive(true);
        if (coll != null) coll.enabled = true;
    }

    void Update()
    {
        switch (state)
        {
            case State.Waiting:
                // the knockdown went ahead without the cutscene (or it was skipped): it's simply on the ground
                if (knockdown != null && knockdown.AlreadyDone) { Rest(); return; }
                if (director == null || adelHand == null) return;
                if (director.state != PlayState.Playing || director.time < dropTime) return;
                pos = adelHand.position;
                vel = releaseVelocity;
                rot = Random.rotation;
                spin = Quaternion.Euler(Random.Range(-540f, 540f), Random.Range(-540f, 540f), Random.Range(-540f, 540f));
                visual.SetPositionAndRotation(pos, rot);
                visual.gameObject.SetActive(true);
                state = State.Falling;
                break;

            case State.Falling:
            {
                float dt = Time.deltaTime;
                vel.y -= gravity * dt;
                vel.x *= 1f - 0.6f * dt; vel.z *= 1f - 0.6f * dt;   // a wrapper is light: the air slows it
                pos += vel * dt;
                rot = Quaternion.SlerpUnclamped(Quaternion.identity, spin, dt) * rot;
                visual.SetPositionAndRotation(pos, rot);
                if (pos.y <= restPos.y + 0.02f)
                {
                    settleFrom = new Vector3(pos.x, restPos.y, pos.z);
                    settleFromRot = rot;
                    settleT = 0f;
                    state = State.Settling;
                }
                break;
            }

            case State.Settling:
            {
                // a short skid to its resting spot, flattening out
                settleT += Time.deltaTime / settleTime;
                float k = 1f - (1f - Mathf.Clamp01(settleT)) * (1f - Mathf.Clamp01(settleT));
                visual.SetPositionAndRotation(Vector3.Lerp(settleFrom, restPos, k), Quaternion.Slerp(settleFromRot, restRot, k));
                if (settleT >= 1f) Rest();
                break;
            }
        }
    }
}
