using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// The first Questioner teaches the counter to his grab: his first move is always the grab dash; when he is almost on Khalid everything
/// but Khalid stops, the world fades out round the two of them, and it stays like that until the player raises the hammer and
/// strikes. The strike is the real counter (QuestionerCombatState.OnMeleeHit), then time goes on. Once per game (StoryFlags).
/// </summary>
public class GrabCounterTutorial : MonoBehaviour
{
    public const string Flag = "GrabCounterTutorialDone";
    /// <summary>How near he must be (m) for the world to stop: just inside the hammer's reach.</summary>
    public const float StopDistance = 1.75f;

    static GrabCounterTutorial instance;
    /// <summary>The world is standing still for it (no walking, no weapon switching).</summary>
    public static bool Holding => instance != null && !instance.ending;

    QuestionerEnemy q;
    bool ending;
    float amount, endT, startVolume = 1f;
    FreezeFocusEffect effect;
    InteractPromptUI raise, strike;
    int shown;
    CinemachineVirtualCamera cam;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    readonly List<ParticleSystem> paused = new List<ParticleSystem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    /// <summary>This Questioner should open with the grab dash and be stopped in it.</summary>
    public static bool Pending(QuestionerEnemy who)
    {
        if (instance != null || who == null || who.hordeMode) return false;
        if (!PlayerMeleeWeapon.Equipped || PlayerMeleeWeapon.Switching) return false;
        var p = Player.instance;
        if (p == null || p.IsDead() || p.isKnockedDown) return false;
        return !StoryFlags.Has(Flag);
    }

    public static void Begin(QuestionerEnemy who)
    {
        if (instance != null) return;
        instance = new GameObject("Grab Counter Tutorial").AddComponent<GrabCounterTutorial>();
        instance.q = who;
        instance.StartHold();
    }

    /// <summary>A strike landed on him: the lesson is over.</summary>
    public static void OnStrike(QuestionerEnemy who)
    {
        if (instance == null || instance.ending || instance.q != who) return;
        StoryFlags.Set(Flag);
        instance.End();
    }

    void StartHold()
    {
        EnemyFreeze.Hold(this);
        startVolume = AudioListener.volume;
        foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            if (ps.isPlaying && (Player.instance == null || !ps.transform.IsChildOf(Player.instance.transform))) { ps.Pause(false); paused.Add(ps); }
        var main = Camera.main;
        if (main != null)
        {
            effect = main.GetComponent<FreezeFocusEffect>();
            if (effect == null) effect = main.gameObject.AddComponent<FreezeFocusEffect>();
            MakeCamera(main);
        }
        raise = PromptCopy.Make("Hold ", "AimPromptUI", " to prepare your attack");
        strike = PromptCopy.Make("Press ", "ShootPromptUI", " to attack");
    }

    // a step out to his right, so the Questioner isn't hidden behind him (he fills the normal view at this distance)
    void MakeCamera(Camera main)
    {
        brain = main.GetComponent<CinemachineBrain>();
        var p = Player.instance;
        if (brain == null || p == null || q == null) return;
        Vector3 to = q.transform.position - p.transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        to.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, to);
        Vector3 chest = p.transform.position + Vector3.up * 1.45f;
        Vector3 best = Vector3.zero; float bestRoom = -1f;
        foreach (float side in new[] { 1f, -1f })
        {
            Vector3 want = p.transform.position - to * 1.25f + right * side * 1.55f + Vector3.up * 1.6f;
            Vector3 d = want - chest;
            float room = Physics.SphereCast(chest, 0.2f, d.normalized, out var hit, d.magnitude, ~0, QueryTriggerInteraction.Ignore)
                         && !hit.transform.IsChildOf(p.transform) && !hit.transform.IsChildOf(q.transform) ? hit.distance : d.magnitude;
            if (room > bestRoom + 0.05f) { bestRoom = room; best = chest + d.normalized * room; }
        }
        if (bestRoom < 1f) return;
        var go = new GameObject("Grab lesson shot");
        cam = go.AddComponent<CinemachineVirtualCamera>();
        cam.m_Lens.FieldOfView = 52f; cam.m_Lens.NearClipPlane = 0.06f;
        cam.m_Lens.FarClipPlane = Mathf.Clamp(main.farClipPlane, 10f, 60f);
        cam.transform.position = best;
        cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(p.transform.position, q.transform.position, 0.6f) + Vector3.up * 1.25f - best);
        savedBlend = brain.m_DefaultBlend;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.9f);
        cam.Priority = 400;
    }

    void End()
    {
        if (ending) return;
        ending = true;
        if (cam != null) cam.Priority = -50;
        EnemyFreeze.Release(this);
        foreach (var ps in paused) if (ps != null) ps.Play(false);
        paused.Clear();
        Prompt(0);
    }

    void Prompt(int which)
    {
        if (which == shown) return;
        shown = which;
        if (raise != null) { if (which == 1) raise.ShowPrompt(); else raise.HidePrompt(); }
        if (strike != null) { if (which == 2) strike.ShowPrompt(); else strike.HidePrompt(); }
    }

    void Update()
    {
        var p = Player.instance;
        if (!ending && (q == null || q.health == null || q.health.IsDead || p == null || p.IsDead() || !PlayerMeleeWeapon.Equipped)) End();

        if (!ending && !GameManager.IsPaused)
        {
            Vector3 to = q.transform.position - p.transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f && !PlayerMeleeWeapon.Attacking)
                p.transform.rotation = Quaternion.RotateTowards(p.transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
            // the weapon tutorial's own prompts may still be up (the Store): they say the same
            Prompt(GunTutorialPrompts.Showing || PlayerMeleeWeapon.Attacking ? 0 : PlayerMeleeWeapon.Ready ? 2 : 1);
        }

        amount = Mathf.MoveTowards(amount, ending ? 0f : 1f, Time.unscaledDeltaTime / (ending ? 0.5f : 1.6f));
        float eased = amount * amount * (3f - 2f * amount);
        AudioListener.volume = Mathf.Lerp(startVolume, startVolume * 0.3f, eased);
        if (effect != null)
        {
            effect.Amount = eased;
            if (p != null) effect.focus = (q != null ? (p.transform.position + q.transform.position) * 0.5f : p.transform.position) + Vector3.up;
        }
        if (ending && amount <= 0f && (endT += Time.unscaledDeltaTime) > 0.6f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        End();
        AudioListener.volume = startVolume;
        if (cam != null) { Destroy(cam.gameObject); if (brain != null) brain.m_DefaultBlend = savedBlend; }
        if (effect != null) effect.Amount = 0f;
        if (raise != null) Destroy(raise.gameObject);
        if (strike != null) Destroy(strike.gameObject);
        if (instance == this) instance = null;
    }
}
