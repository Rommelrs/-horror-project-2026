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

    readonly Queue<Entry> queue = new Queue<Entry>();
    Coroutine routine;

    void OnEnable() => Inventory.OnItemPickedUp += OnItemPickedUp;
    void OnDisable() => Inventory.OnItemPickedUp -= OnItemPickedUp;

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
        if (Player.instance == null || Player.instance.IsDead()) return false;
        if (GameManager.IsPaused) return false;
        if (SubtitleManager.instance != null && SubtitleManager.instance.IsSubtitleBusy()) return false;
        if (InventoryUI.instance != null && InventoryUI.instance.InventoryIsActive()) return false;
        return true;
    }
}
