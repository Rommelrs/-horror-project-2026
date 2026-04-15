using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager instance;

    [SerializeField] Item ammoItem;

    public bool hitstopIsEnabled = true;
    public TMP_Text hitstopText;

    public bool damageTextIsEnabled = true;
    public TMP_Text damageText;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        UpdateHitStopDebugText();
        UpdateDamageText();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            //Decrease Stability
            Player.instance.playerStability.DecreaseStability(10);
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            //Increase Stability
            Player.instance.playerStability.IncreaseStability(10);
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            //Spawn Enemy in random spawn point
            EnemySpawner.instance.SpawnEnemyOveride(true);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            //Spawn Enemy in random spawn point
            EnemySpawner.instance.SpawnEnemyOveride(false);
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            //Add weapon ammo to inventory
            Player.instance.inventory.AddItem(ammoItem, 10);
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            //Toogle Hitstop;
            hitstopIsEnabled = !hitstopIsEnabled;
            UpdateHitStopDebugText();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            //Toogle Damage Text;
            damageTextIsEnabled = !damageTextIsEnabled;
            UpdateDamageText();
        }
    }

    void UpdateHitStopDebugText()
    {
        hitstopText.text = "HitStop: " + hitstopIsEnabled.ToString();
    }

    void UpdateDamageText()
    {
        damageText.text = "DamageText: " + damageTextIsEnabled.ToString();
    }
}
