#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class AgentTests
{
    static Item It(string n) => Resources.LoadAll<Item>("Items").FirstOrDefault(i => i.name == n);

    static IEnumerator TS()
    {
        while (P == null) yield return 0.2f;
        yield return 3f;
        P.health.Invincible = true;
        Clear();
        foreach (var n in new[] { "Training Note", "Delivery Map" })
        {
            ItemInspectionHandler.instance.InspectItem(It(n));
            yield return 1.6f; Shot("ts_" + n.Split(' ')[0] + ".png"); yield return 0.5f;
            ItemInspectionHandler.instance.CloseInspectionMenu();
            yield return 1.5f; Clear(); yield return 0.5f;
        }
        // Options page
        var om = Resources.FindObjectsOfTypeAll<OptionsManager>().First(o => o.gameObject.scene.IsValid());
        var menu = om.transform.Find("OptionsMenu").gameObject;
        menu.SetActive(true); yield return 0.6f; Shot("ts_options.png"); yield return 0.5f; menu.SetActive(false);
        // register
        var reg = Object.FindFirstObjectByType<CashRegisterInteractable>();
        var fuse = Object.FindFirstObjectByType<Fusebox>(FindObjectsInactive.Include);
        reg.ActivateCashRegisterMenu();
        yield return 3.5f;
        Debug.Log("TS register menu " + reg.CashRegisterMenuIsActive());
        Shot("ts_reg0.png"); yield return 0.4f;
        for (int i = 0; i < 3; i++) { reg.OnNumberButtonPressed(1); yield return 0.3f; }
        reg.OnEnterButtonPressed();
        yield return 2f;
        Debug.Log("TS countdown " + PuzzleHint.SecondsLeft(CashRegisterInteractable.HintId));
        Shot("ts_reg1.png"); yield return 0.4f;
        float left = PuzzleHint.SecondsLeft(CashRegisterInteractable.HintId);
        if (left > 0f) yield return left + 1f;
        Shot("ts_reg2.png"); yield return 0.4f;
        Press(Key.H); yield return 0.15f; Press(); yield return 1f;
        Debug.Log("TS after H: subtitle " + SubtitleManager.instance.IsSubtitleBusy());
        Shot("ts_reg3.png"); yield return 5f;
        Debug.Log("TS subtitle gone " + !SubtitleManager.instance.IsSubtitleBusy() + " menu " + reg.CashRegisterMenuIsActive());
        // leave: the Runner's blink
        var runner = Object.FindFirstObjectByType<RunnerPuppet>(FindObjectsInactive.Include);
        Debug.Log("TS runner up " + (runner != null && runner.gameObject.activeInHierarchy));
        reg.CloseCashRegisterMenu();
        double t0 = Now, shown = -1, hidden = -1;
        while (Now - t0 < 8)
        {
            bool up = runner != null && runner.gameObject.activeInHierarchy;
            bool world = !reg.CashRegisterMenuIsActive();
            if (up && world && shown < 0) shown = Now;
            if (!up && shown > 0 && hidden < 0) hidden = Now;
            yield return null;
        }
        Debug.Log("TS runner seen " + (shown > 0) + " for " + (hidden > 0 ? (hidden - shown).ToString("F2") : "never hidden") + " s");
    }
}
#endif
