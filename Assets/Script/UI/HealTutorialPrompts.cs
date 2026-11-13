using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One-off hints for the heal key: the first painkiller shows "Press [G] to use Painkillers", the first medic kit shows
// "Hold [G] to use Medic Kit". Lives on GameCanvas.prefab, so every scene with that canvas has it. Listens to real pickups
// only (Inventory.OnItemPickedUp), so loading a save never fires it. The "seen" state is a StoryFlag: it travels with
// saves and checkpoints across scenes and resets on New Game.
//
// The player can't heal at full health, so the hint can't wait for a use like the weapon hints: it shows for showSeconds
// of unpaused time, or until the player heals. It sits one line above the other prompts (they share y -420) because the
// Store has a painkiller right next to the pipe and the pipe's hint stays up until the first swing.
public class HealTutorialPrompts : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public Item item;
        public string seenFlag;
        public InteractPromptUI prompt;
    }

    [SerializeField] Entry[] entries;
    [Tooltip("Seconds on screen (time while it is hidden by a pause, subtitle or the inventory doesn't count).")]
    [SerializeField] float showSeconds = 6f;
    [Tooltip("Lets the pickup banner start sliding in first.")]
    [SerializeField] float startDelay = 0.4f;

    [Header("Low health reminders")]
    [Tooltip("At or below this much health (fraction of max) with painkillers: \"Press [G] to use Painkillers\".")]
    [SerializeField, Range(0f, 1f)] float remindPainkillerAt = 0.5f;
    [Tooltip("At or below this much health with a medic kit: \"Hold [G] to use Medic Kit\" (comes first).")]
    [SerializeField, Range(0f, 1f)] float remindMedKitAt = 0.3f;
    [SerializeField] float reminderSeconds = 5f;
    [Tooltip("The same reminder waits this long before it can show again (it also re-arms once health is back above its line).")]
    [SerializeField] float reminderCooldown = 45f;

    readonly Queue<Entry> queue = new Queue<Entry>();
    Coroutine routine;
    Coroutine reminderRoutine;
    readonly Dictionary<Entry, float> nextReminder = new Dictionary<Entry, float>();

    void OnEnable() => Inventory.OnItemPickedUp += OnItemPickedUp;
    void OnDisable()
    {
        Inventory.OnItemPickedUp -= OnItemPickedUp;
        reminderRoutine = null;
    }

    void Update()
    {
        if (routine != null || reminderRoutine != null || entries == null) return;
        var p = Player.instance;
        if (p == null || p.health == null || p.inventory == null || !CanShow()) return;
        float frac = p.health.GetHealthValue() / (float)Mathf.Max(1, p.health.GetMaxHealthValue());

        Entry painkiller = null, medKit = null;
        foreach (var e in entries)
        {
            if (e.item == null || e.prompt == null) continue;
            if (e.item.itemType == ItemType.HealingAndAddStability) medKit = e; else painkiller = e;
        }
        // back above a line: that reminder may come again next time health drops below it
        if (medKit != null && frac > remindMedKitAt) nextReminder.Remove(medKit);
        if (painkiller != null && frac > remindPainkillerAt) nextReminder.Remove(painkiller);

        Entry pick = null;
        if (medKit != null && frac <= remindMedKitAt && p.inventory.CountOf(medKit.item) > 0 && Ready(medKit)) pick = medKit;
        else if (painkiller != null && frac <= remindPainkillerAt && p.inventory.CountOf(painkiller.item) > 0 && Ready(painkiller)) pick = painkiller;
        if (pick == null || PlayerQuickHeal.Using) return;

        nextReminder[pick] = Time.unscaledTime + reminderCooldown;
        reminderRoutine = StartCoroutine(Co_Remind(pick));
    }

    bool Ready(Entry e) => !nextReminder.TryGetValue(e, out float t) || Time.unscaledTime >= t;

    IEnumerator Co_Remind(Entry e)
    {
        bool on = false;
        float shown = 0f;
        while (shown < reminderSeconds && routine == null)
        {
            bool can = CanShow();
            if (can != on)
            {
                on = can;
                if (on) e.prompt.ShowPrompt(); else e.prompt.HidePrompt();
            }
            if (on)
            {
                if (PlayerQuickHeal.Using) break;
                shown += Time.unscaledDeltaTime;
            }
            yield return null;
        }
        e.prompt.HidePrompt();
        reminderRoutine = null;
    }

    void OnItemPickedUp(Item item, int before, int after)
    {
        foreach (var e in entries)
        {
            if (e.item != item || StoryFlags.Has(e.seenFlag) || queue.Contains(e)) continue;
            queue.Enqueue(e);
            if (routine == null) routine = StartCoroutine(Co_Run());
        }
    }

    IEnumerator Co_Run()
    {
        while (queue.Count > 0)
        {
            Entry e = queue.Peek();
            yield return new WaitForSecondsRealtime(startDelay);

            bool on = false;
            float shown = 0f;
            while (shown < showSeconds)
            {
                bool can = CanShow();
                if (can != on)
                {
                    on = can;
                    if (on)
                    {
                        StoryFlags.Set(e.seenFlag);   // set here, not on pickup: a pickup whose hint never got on screen still gets it next time
                        e.prompt.ShowPrompt();
                    }
                    else e.prompt.HidePrompt();
                }

                if (on)
                {
                    if (PlayerQuickHeal.Using) break;   // done, they've got it
                    shown += Time.unscaledDeltaTime;
                }
                yield return null;
            }

            e.prompt.HidePrompt();
            queue.Dequeue();
        }
        routine = null;
    }

    static bool CanShow()
    {
        if (Player.instance == null || Player.instance.IsDead() || Player.instance.isGrabbed) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        return true;
    }
}
