using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Use healing items straight from the keyboard / pad without opening the inventory:
//   tap  G (gamepad East) - Painkiller  (the same heal the inventory "Use" gives)
//   hold G                - Med Kit     (heal + stability)
// While G is held past a short delay the pickup banner slides in so the player sees the counts; using an item ticks
// its count down there. Does nothing while paused, dead, aiming / reloading, at full health, or with none left.
// Using an item plays an upper-body animation on the animator's "UseItem" layer (masked, so he can still walk): the gun
// is hidden, the painkiller bottle / med kit syringe shows in his right hand, and the heal lands at the key moment of the
// animation (pills at the mouth / needle in the shoulder). Built by Tools > Items > Build Healing Items (PS1).
public class PlayerQuickHeal : MonoBehaviour
{
    public static bool Using { get; private set; }

    [SerializeField] InputActionReference healInput;
    [SerializeField] Item painkiller;
    [SerializeField] Item medKit;
    [SerializeField] AudioClip useClip;

    [Tooltip("Held at least this long = Med Kit, shorter = Painkiller.")]
    [SerializeField] float holdTime = 0.4f;
    [Tooltip("Holding this long already slides the banner in, so the player sees what they have.")]
    [SerializeField] float previewAfter = 0.15f;

    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] string layerName = "UseItem";
    [SerializeField] string painkillerState = "UsePainkiller";
    [SerializeField] string medKitState = "UseMedKit";
    [Tooltip("Seconds the painkiller animation runs / when the pills reach the mouth (heal).")]
    [SerializeField] float painkillerDuration = 1.85f;
    [SerializeField] float painkillerHealAt = 0.85f;
    [Tooltip("Seconds the med kit animation runs / when the needle is in the shoulder (heal).")]
    [SerializeField] float medKitDuration = 2.5f;
    [SerializeField] float medKitHealAt = 1.3f;
    [SerializeField] float layerBlendIn = 0.15f;
    [SerializeField] float layerBlendOut = 0.25f;
    [Tooltip("Shown in the right hand while taking the painkiller.")]
    [SerializeField] GameObject painkillerProp;
    [Tooltip("Shown in the right hand while injecting the med kit.")]
    [SerializeField] GameObject medKitProp;
    [SerializeField] bool hideGun = true;

    bool down;
    bool handled;
    float heldFor;
    bool previewed;
    int layer = -1;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (painkillerProp != null) painkillerProp.SetActive(false);
        if (medKitProp != null) medKitProp.SetActive(false);
    }

    void OnEnable() => healInput?.action?.Enable();

    void OnDisable()
    {
        Using = false;
    }

    void Update()
    {
        if (healInput == null || healInput.action == null) return;
        var action = healInput.action;

        if (action.WasPressedThisFrame() && CanAct())
        {
            down = true; handled = false; previewed = false; heldFor = 0f;
        }

        if (!down) return;

        if (!CanAct()) { down = false; return; }

        if (action.IsPressed())
        {
            heldFor += Time.unscaledDeltaTime;
            if (!previewed && heldFor >= previewAfter && HealingItemsHUD.instance != null)
            {
                previewed = true;
                HealingItemsHUD.instance.ShowReminder();
            }
            if (!handled && heldFor >= holdTime)
            {
                handled = true;
                Use(medKit, "No med kits left");
            }
        }
        else
        {
            if (!handled)
                Use(painkiller, "No painkillers left");
            down = false;
        }
    }

    bool CanAct()
    {
        var player = Player.instance;
        if (player == null || player.IsDead() || GameManager.IsPaused || Using || PlayerMeleeWeapon.Ready) return false;
        var weapon = player.playerWeaponSystem;
        return weapon == null || (!weapon.isAiming && !weapon.isReloading);
    }

    void Use(Item item, string noneLeftMessage)
    {
        var player = Player.instance;
        if (item == null || player == null) return;

        if (player.inventory.CountOf(item) <= 0) { Say(noneLeftMessage); return; }

        // same rule as the inventory: the medic kit still works at full health while stability is not full
        string fullKey = InventoryDescriptionUI.FullMessageKey(item);
        if (fullKey != null) { Say(fullKey == "Health Full" ? "Health is already full" : "Health and stability are already full"); return; }

        StartCoroutine(Co_Use(player, item));
    }

    IEnumerator Co_Use(Player player, Item item)
    {
        Using = true;
        bool isPainkiller = item == painkiller;
        float duration = isPainkiller ? painkillerDuration : medKitDuration;
        float healAt = isPainkiller ? painkillerHealAt : medKitHealAt;

        var weapon = player.playerWeaponSystem;
        if (weapon != null) weapon.blockAimingSystem = true;
        GameObject gun = null;
        bool gunWasActive = false;
        if (hideGun)
        {
            var enabler = player.GetComponent<PlayerWeaponEnabler>();
            gun = enabler != null ? enabler.WeaponObject : null;
            if (gun != null) { gunWasActive = gun.activeSelf; gun.SetActive(false); }
        }
        PlayerMeleeWeapon.SetHiddenForUse(true);
        var prop = isPainkiller ? painkillerProp : medKitProp;
        if (prop != null) prop.SetActive(true);

        if (animator != null)
        {
            if (layer < 0) layer = animator.GetLayerIndex(layerName);
            if (layer >= 0) animator.CrossFadeInFixedTime(isPainkiller ? painkillerState : medKitState, 0.1f, layer, 0f);
        }

        float t = 0f;
        bool healed = false;
        while (t < duration)
        {
            if (player.IsDead()) break;
            t += Time.deltaTime;
            SetLayerWeight(Mathf.Min(Mathf.Clamp01(t / layerBlendIn), Mathf.Clamp01((duration - t) / layerBlendOut)));

            if (!healed && t >= healAt)
            {
                healed = true;
                Consume(player, item);
                // the inventory update re-enables the gun (PlayerWeaponEnabler) - keep it hidden until he's done
                if (gun != null) gun.SetActive(false);
            }
            yield return null;
        }

        SetLayerWeight(0f);
        if (prop != null) prop.SetActive(false);
        PlayerMeleeWeapon.SetHiddenForUse(false);
        if (gun != null && gunWasActive && !player.IsDead()) gun.SetActive(true);
        if (weapon != null) weapon.blockAimingSystem = false;
        Using = false;
    }

    void SetLayerWeight(float w)
    {
        if (animator != null && layer >= 0) animator.SetLayerWeight(layer, w);
    }

    void Consume(Player player, Item item)
    {
        var inventory = player.inventory;
        int before = inventory.CountOf(item);
        if (before <= 0) return;

        player.health.Heal(item.healingAmount);
        if (item.stabilityIncreaseAmount > 0 && player.playerStability != null)
            player.playerStability.IncreaseStability(item.stabilityIncreaseAmount);

        if (useClip != null && SoundEffectManager.instance != null)
            SoundEffectManager.instance.PlaySFX(useClip);

        inventory.RemoveItem(item);
        if (HealingItemsHUD.instance != null)
            HealingItemsHUD.instance.ShowCount(item, before, inventory.CountOf(item));
    }

    static void Say(string message)
    {
        if (MessageHandler.instance != null) MessageHandler.instance.ShowMessage(message);
    }
}
