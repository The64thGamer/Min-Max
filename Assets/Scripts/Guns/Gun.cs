using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class Gun : MonoBehaviour
{
    public abstract void Fire();
    public abstract void AltFire();
    public abstract List<WeaponStats> ChangableStats();
    public abstract int GetCurrentAmmo();
    public abstract string GetNameKey();
    public abstract int SearchStats(ChangableWeaponStats stat);
    public abstract void SetGunTransformParent(Transform parent, bool dumbStupidJank);
    public abstract void SetPlayer(Player player);

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