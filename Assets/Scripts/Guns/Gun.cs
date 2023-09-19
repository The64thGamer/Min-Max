using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class Gun : MonoBehaviour
{

    //Inspector Values
    [SerializeField]
    protected List<WeaponStats> changableStats = new List<WeaponStats>()
    {
        new WeaponStats(){ statName = ChangableWeaponStats.maxAmmo, stat = 5},
        new WeaponStats(){ statName = ChangableWeaponStats.bulletsPerShot, stat = 1},
        new WeaponStats(){ statName = ChangableWeaponStats.shotsPerSecond, stat = 3},
        new WeaponStats(){ statName = ChangableWeaponStats.bulletSpeed, stat = 3},
        new WeaponStats(){ statName = ChangableWeaponStats.damage, stat = 30},
    };
    [SerializeField] protected List<WeaponPos> weaponPos;
    [SerializeField] protected string gunNameKey;
    [SerializeField] protected Player currentPlayer;
    [SerializeField] protected Animator animator;
    [SerializeField] protected float fireAnimationSpeed;
    [SerializeField] protected Transform firePoint;


    //Standard Values
    protected int currentAmmo;
    protected GunProjectiles defaultStats;
    protected float oldFireSpeed;
    protected float fireCooldown;

    //Constants
    protected const float MINANGLE = 0.8f;
    protected const float MAXSPHERECASTDISTANCE = 20;
    protected const float MAXRAYCASTDISTANCE = 1000;
    protected const float maxDamageFalloff = 20;


    public virtual void Start()
    {
        currentAmmo = (int)FindStat(ChangableWeaponStats.maxAmmo);
    }

    public virtual void Update()
    {
        float fireSpeed = FindStat(ChangableWeaponStats.shotsPerSecond) * fireAnimationSpeed;
        if (oldFireSpeed != fireSpeed)
        {
            oldFireSpeed = fireSpeed;
            animator.SetFloat("Fire Speed", fireSpeed);
        }
    }

    public virtual void LateUpdate()
    {
        animator.SetBool("Fire", false);
    }

    public virtual void Fire()
    {
        if (fireCooldown <= 0)
        {
            animator.SetBool("Fire", true);
            fireCooldown = 1.0f / FindStat(ChangableWeaponStats.shotsPerSecond);
        }
    }


    //Exploit: Hit needs to be parsed to ensure extreme angles aren't achievable.
    //This function breaks if the currently held weapon switches before its called
    public void SpawnProjectile(Player player)
    {
        if (defaultStats.firePrefab != null)
        {
            Vector3 fireAngle = CalculateFireAngle(player);

            Transform gunAngle = player.GetTracker().GetRightHand();
            int numBullets = (int)FindStat(ChangableWeaponStats.bulletsPerShot);
            float angle = FindStat(ChangableWeaponStats.bulletSpreadAngle);
            float angleStep = 0;
            if (numBullets < 4)
            {
                angleStep = 360f / numBullets;
            }
            else
            {
                angleStep = 360f / (numBullets - 1);
            }

            for (int i = 0; i < numBullets; i++)
            {
                GameObject currentProjectile = GameObject.Instantiate(defaultStats.firePrefab);
                currentProjectile.name = "Projectile";
                Vector3 finalAngle = fireAngle;
                if (!(numBullets >= 4 && i == 0))
                {
                    int newi = i;
                    if (numBullets >= 4)
                    {
                        newi--;
                    }
                    float currentAngle = newi * angleStep;

                    // Convert angle to radians
                    float angleInRadians = currentAngle * Mathf.Deg2Rad;

                    finalAngle = Quaternion.AngleAxis(Mathf.Cos(angleInRadians) * angle, gunAngle.right) * finalAngle;
                    finalAngle = Quaternion.AngleAxis(Mathf.Sin(angleInRadians) * angle, gunAngle.up) * finalAngle;
                }
                currentProjectile.GetComponent<Projectile>().SetProjectile(firePoint.position, finalAngle, player.GetCurrentGun().FindStat(ChangableWeaponStats.bulletSpeed), player.GetTeamLayer(), CalculateHitPosition(finalAngle, player, firePoint.position), 1 / numBullets);
            }
        }
    }

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    protected Vector3 CalculateFireAngle(Player player)
    {
        Transform cam = player.GetTracker().GetCamera();
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
        mask &= ~(1 << 19); //Dead Player Layermask
        return mask;
    }

    protected virtual void HitScanHostDamageCalculation(Player player)
    {
        Vector3 fireAngle = CalculateFireAngle(player);

        Transform gunAngle = player.GetTracker().GetRightHand();
        int numBullets = (int)FindStat(ChangableWeaponStats.bulletsPerShot);
        float angle = FindStat(ChangableWeaponStats.bulletSpreadAngle);
        float angleStep;
        if (numBullets < 4)
        {
            angleStep = 360f / numBullets;
        }
        else
        {
            angleStep = 360f / (numBullets - 1);
        }

        int bulletIdHash = Random.Range(-999999999, 999999999);

        for (int i = 0; i < numBullets; i++)
        {
            Vector3 finalAngle = fireAngle;
            if (!(numBullets >= 4 && i == 0))
            {
                int newi = i;
                if (numBullets >= 4)
                {
                    newi--;
                }
                float currentAngle = newi * angleStep;

                // Convert angle to radians
                float angleInRadians = currentAngle * Mathf.Deg2Rad;

                finalAngle = Quaternion.AngleAxis(Mathf.Cos(angleInRadians) * angle, gunAngle.right) * finalAngle;
                finalAngle = Quaternion.AngleAxis(Mathf.Sin(angleInRadians) * angle, gunAngle.up) * finalAngle;
            }
            RaycastHit hit;
            LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);
            float dotAngle = Vector3.Dot(player.GetTracker().GetRightHandSafeForward(), fireAngle.normalized);
            if (dotAngle > MINANGLE)
            {
                if (Physics.Raycast(firePoint.position, finalAngle, out hit, MAXRAYCASTDISTANCE, layermask))
                {
                    Player hitPlayer = hit.collider.GetComponent<Player>();
                    if (hitPlayer != null)
                    {
                        int damage = Mathf.CeilToInt(Mathf.Max(0, Mathf.SmoothStep(FindStat(ChangableWeaponStats.damage), 0, hit.distance / maxDamageFalloff)));
                        if (damage > 0)
                        {
                            hitPlayer.ChangeHealth(player.GetPlayerID(), -damage, bulletIdHash);
                        }
                    }
                }
            }
        }
    }

    protected float FindStat(ChangableWeaponStats statName)
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

    public void SetDefaultStats(GunProjectiles stat)
    {
        defaultStats = stat;
    }

    public abstract void AltFire();
    public abstract List<WeaponStats> ChangableStats();
    public abstract int GetCurrentAmmo();
    public abstract string GetNameKey();
    public abstract void SetPlayer(Player player);
}
[System.Serializable]
public class WeaponStats
{
    public ChangableWeaponStats statName;
    public float stat;
}

[System.Serializable]
public struct WeaponPos
{
    public ClassList classUsed;
    public Vector3 frontPos;
    public Vector3 rightPos;
    public Vector3 leftPos;
    public Vector3 upPos;
    public Vector3 downPos;
}

[SerializeField]
public enum ChangableWeaponStats
{
    shotsPerSecond,
    bulletsPerShot,
    maxAmmo,
    bulletSpeed,
    damage,
    bulletSpreadAngle,
    radiationAfterburn,
    maxBulletRange,
}