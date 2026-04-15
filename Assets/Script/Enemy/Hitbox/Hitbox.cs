using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    public DamageType damageType;
    public float damageMultiplier = 1f;
}

[System.Serializable]
public enum DamageType
{
    Normal,
    Headshot,
    Weakpoint
}
