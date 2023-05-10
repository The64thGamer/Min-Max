using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class Gun : NetworkBehaviour
{
    public abstract void Fire();
    public abstract void AltFire();
    public abstract List<WeaponStats> ChangableStats();
    public abstract int GetCurrentAmmo();
    public abstract string GetNameKey();
    public abstract int SearchStats(ChangableWeaponStats stat);

}
[System.Serializable]
public class WeaponStats
{
    public ChangableWeaponStats statName;
    public int stat;
}

[SerializeField]
public enum ChangableWeaponStats
{
    shotsPerSecond,
    bulletsPerShot,
    maxAmmo,
    bulletSpeed,
}

[SerializeField]
public enum Team
{
    neutral,
    team1,
    team2,
}