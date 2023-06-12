using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class Gun : MonoBehaviour
{
    //Constants
    const float MINANGLE = 0.8f;
    const float MAXSPHERECASTDISTANCE = 20;
    const float MAXRAYCASTDISTANCE = 1000;


    [SerializeField]
    protected List<WeaponStats> changableStats = new List<WeaponStats>()
    {
        new WeaponStats(){ statName = ChangableWeaponStats.maxAmmo, stat = 5},
        new WeaponStats(){ statName = ChangableWeaponStats.bulletsPerShot, stat = 1},
        new WeaponStats(){ statName = ChangableWeaponStats.shotsPerSecond, stat = 3},
        new WeaponStats(){ statName = ChangableWeaponStats.bulletSpeed, stat = 3},
        new WeaponStats(){ statName = ChangableWeaponStats.damage, stat = 30},
    };
    protected int currentAmmo;
    protected GunProjectiles defaultStats;

    public abstract void Fire();
    public abstract void AltFire();
    public abstract List<WeaponStats> ChangableStats();
    public abstract int GetCurrentAmmo();
    public abstract string GetNameKey();
    public abstract void SetGunTransformParent(Transform parent, bool dumbStupidJank);
    public abstract void SetPlayer(Player player);

    private void Start()
    {
        currentAmmo = FindStat(ChangableWeaponStats.maxAmmo);
    }

    //Exploit: Hit needs to be parsed to ensure extreme angles aren't achievable.
    //This function breaks if the currently held weapon switches before its called
    public void SpawnProjectile(Player player)
    {
        if (defaultStats.firePrefab != null)
        {
            Vector3 firepos = player.GetTracker().GetRightHandFirePos(defaultStats.firepoint);
            GameObject currentProjectile = GameObject.Instantiate(defaultStats.firePrefab);
            Vector3 fireAngle = CalculateFireAngle(player);
            currentProjectile.GetComponent<Projectile>().SetProjectile(firepos, fireAngle, player.GetCurrentGun().FindStat(ChangableWeaponStats.bulletSpeed), player.GetTeamLayer(), CalculateHitPosition(fireAngle, player, firepos));
        }
    }

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    protected Vector3 CalculateFireAngle(Player player)
    {
        Transform cam = player.GetTracker().GetCamera();
        RaycastHit hit;
        Vector3 fpForward = player.GetTracker().GetRightHandSafeForward();

        float dotAngle = Vector3.Dot(fpForward, cam.forward);
        if (dotAngle > MINANGLE)
        {
            float percentage = (dotAngle - MINANGLE) / (1 - MINANGLE);
            return Vector3.Slerp(fpForward, cam.forward, percentage);
        }
        return fpForward;
    }

    protected Vector3 CalculateHitPosition(Vector3 fireAngle, Player player, Vector3 firePoint)
    {
        RaycastHit hit;
        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);
        float dotAngle = Vector3.Dot(player.GetTracker().GetRightHandSafeForward(), fireAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            if (Physics.Raycast(firePoint, fireAngle, out hit, MAXRAYCASTDISTANCE, layermask))
            {
                return hit.point;
            }
        }
        return firePoint + (MAXRAYCASTDISTANCE * fireAngle.normalized);
    }

    protected LayerMask GetIgnoreTeamAndVRLayerMask(Player player)
    {
        LayerMask mask;
        switch (player.GetTeam())
        {
            case TeamList.orange:
                mask = 1 << LayerMask.NameToLayer("OrangeTeam");
                break;
            case TeamList.yellow:
                mask = 1 << LayerMask.NameToLayer("YellowTeam");
                break;
            case TeamList.green:
                mask = 1 << LayerMask.NameToLayer("GreenTeam");
                break;
            case TeamList.lightBlue:
                mask = 1 << LayerMask.NameToLayer("LightBlueTeam");
                break;
            case TeamList.blue:
                mask = 1 << LayerMask.NameToLayer("BlueTeam");
                break;
            case TeamList.purple:
                mask = 1 << LayerMask.NameToLayer("PurpleTeam");
                break;
            case TeamList.beige:
                mask = 1 << LayerMask.NameToLayer("BeigeTeam");
                break;
            case TeamList.brown:
                mask = 1 << LayerMask.NameToLayer("BrownTeam");
                break;
            case TeamList.gray:
                mask = 1 << LayerMask.NameToLayer("GrayTeam");
                break;
            default:
                mask = 1 << LayerMask.NameToLayer("GrayTeam");
                break;
        }
        mask = mask | 805306368; //VR Layermask
        mask = ~mask;
        return mask;
    }

    protected void HitScanHostDamageCalculation(Player player)
    {
        Vector3 firepos = player.GetTracker().GetRightHandFirePos(defaultStats.firepoint);
        Vector3 fireAngle = CalculateFireAngle(player);

        RaycastHit hit;
        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);
        float dotAngle = Vector3.Dot(player.GetTracker().GetRightHandSafeForward(), fireAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            if (Physics.Raycast(firepos, fireAngle, out hit, MAXRAYCASTDISTANCE, layermask))
            {
                Player hitPlayer = hit.collider.GetComponent<Player>();
                if (hitPlayer != null)
                {
                    hitPlayer.TakeDamage(player.GetPlayerID(), FindStat(ChangableWeaponStats.damage));
                }
            }
        }
    }

    protected int FindStat(ChangableWeaponStats statName)
    {
        for (int i = 0; i < changableStats.Count; i++)
        {
            if (changableStats[i].statName == statName)
            {
                return changableStats[i].stat;
            }
        }
        return 0;
    }
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
    damage,
}