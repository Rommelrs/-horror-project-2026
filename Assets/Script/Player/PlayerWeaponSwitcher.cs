using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's guns (pistol, sawed-off shotgun). Puts the gun that is out into PlayerWeaponSystem - its stats, its ammo
/// item and loaded rounds, its shot sound and its model in both hands - and PlayerWeaponSystem keeps doing the aiming,
/// shooting, reloading and hit logic for whichever gun it is. A gun can only be taken out once its weapon item is in
/// the inventory; picking a new one up takes it out straight away. Each gun remembers how many rounds are loaded.
/// Built by Tools > Weapons > Build Shotgun On Player.
/// </summary>
public class PlayerWeaponSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Gun
    {
        public string name;
        public Item weaponItem;
        public Item ammoItem;
        public InputActionReference selectInput;

        [Header("Stats (copied into PlayerWeaponSystem)")]
        public int magazine = 10;
        public int damage = 50;
        public float fireRate = 0.5f;
        public float range = 35f;
        public float bulletSpread = 0.01f;
        public int pierce = 3;
        public float reloadDuration = 2.2f;
        [Tooltip("1 = one bullet. More = a shotgun cone; 'damage' is then the whole shot at point blank, split between the pellets.")]
        public int pellets = 1;
        public float pelletSpread = 0.06f;
        public bool ejectsCartridge = true;
        [Tooltip("Camera shake on every shot (0 = none).")]
        public float kick = 0f;
        [Tooltip("Shot sound (empty = the pistol's).")]
        public AudioClip shootClip;
        public float shootVolume = 0.8f;

        [Header("Model")]
        [Tooltip("This gun's meshes in both hands (first and third person), shown only while it is out.")]
        public Renderer[] renderers;
        [Tooltip("Where the muzzle point (PistolEndPoint: muzzle flash + aim ray) sits on this gun, in the hand gun's local space.")]
        public Vector3 muzzleLocalPosition;

        [HideInInspector] public int loaded = -1;
    }

    public Gun[] guns;
    [Tooltip("The muzzle points of the first- and third-person hand guns (PistolEndPoint).")]
    public Transform[] muzzlePoints;
    public AudioClip switchClip;

    PlayerWeaponSystem weapons;
    Inventory inventory;
    int current = -1;
    readonly HashSet<Item> owned = new HashSet<Item>();

    public Gun Current => current >= 0 ? guns[current] : null;

    void Awake()
    {
        weapons = GetComponent<PlayerWeaponSystem>();
        inventory = GetComponent<Inventory>();
    }

    void OnEnable()
    {
        foreach (var g in guns)
            if (g.selectInput != null) g.selectInput.action.Enable();
    }

    void Start()
    {
        // the gun the scene starts with keeps what it has loaded
        if (guns.Length > 0) guns[0].loaded = weapons.currentAmmo;
        if (inventory != null) inventory.OnInventoryItemUpdated.AddListener(OnInventoryChanged);
        OnInventoryChanged();
        if (current < 0 && guns.Length > 0) Select(0, false);
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryItemUpdated.RemoveListener(OnInventoryChanged);
    }

    void Update()
    {
        if (GameManager.IsPaused || weapons.isReloading) return;
        for (int i = 0; i < guns.Length; i++)
            if (i != current && guns[i].selectInput != null && guns[i].selectInput.action.WasPressedThisFrame() && Owns(i))
                Select(i, true);
    }

    bool Owns(int i) => inventory != null && guns[i].weaponItem != null && inventory.HasItem(guns[i].weaponItem);

    // a newly picked-up gun comes straight out; if the gun that is out was taken away, fall back to one still owned
    void OnInventoryChanged()
    {
        int newest = -1;
        for (int i = 0; i < guns.Length; i++)
        {
            bool has = Owns(i);
            if (has && owned.Add(guns[i].weaponItem)) newest = i;
            else if (!has) owned.Remove(guns[i].weaponItem);
        }
        if (current < 0)
        {
            // first look at the inventory: the first gun owned, quietly
            for (int i = 0; i < guns.Length; i++)
                if (Owns(i)) { Select(i, false); return; }
            return;
        }
        if (newest >= 0 && newest != current && !weapons.isReloading) { Select(newest, current >= 0); return; }
        if (current >= 0 && !Owns(current))
            for (int i = 0; i < guns.Length; i++)
                if (Owns(i)) { Select(i, true); return; }
    }

    public void Select(int index, bool playSound)
    {
        if (current >= 0) guns[current].loaded = weapons.currentAmmo;
        current = index;
        var g = guns[index];
        if (g.loaded < 0) g.loaded = g.magazine;

        weapons.weaponAmmoItem = g.ammoItem;
        weapons.maxAmmoCapacity = g.magazine;
        weapons.currentAmmo = Mathf.Min(g.loaded, g.magazine);
        weapons.damage = g.damage;
        weapons.fireRate = g.fireRate;
        weapons.weaponRange = g.range;
        weapons.bulletSpread = g.bulletSpread;
        weapons.maxBulletPierceCount = g.pierce;
        weapons.reloadDuration = g.reloadDuration;
        weapons.pelletsPerShot = g.pellets;
        weapons.pelletSpread = g.pelletSpread;
        weapons.ejectCartridge = g.ejectsCartridge;
        weapons.shotKick = g.kick;
        weapons.shootClipOverride = g.shootClip;
        weapons.shootClipVolume = g.shootVolume;

        for (int i = 0; i < guns.Length; i++)
            foreach (var r in guns[i].renderers)
                if (r != null) r.enabled = i == index;
        foreach (var m in muzzlePoints)
            if (m != null) m.localPosition = g.muzzleLocalPosition;

        if (playSound && switchClip != null && SoundEffectManager.instance != null)
            SoundEffectManager.instance.PlaySFX(switchClip);
    }
}
