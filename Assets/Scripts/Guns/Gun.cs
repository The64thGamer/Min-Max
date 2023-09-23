using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
    [SerializeField] protected Transform firePoint;
    [SerializeField] protected GunState gunState;
    [SerializeField] protected Transform crosshair;
    [SerializeField] protected AudioClip fireSound;

    //Standard Values
    protected Vector3 currentFireAngle;
    protected AudioSource au;
    protected GlobalManager gm;
    protected Transform rightHand;
    protected GunProjectiles defaultStats;
    protected float oldFireSpeed;
    protected bool showCrosshair;

    //Constants
    protected const float MINANGLE = 0.8f;
    protected const float MAXSPHERECASTDISTANCE = 20;
    protected const float MAXRAYCASTDISTANCE = 1000;
    protected const float maxDamageFalloff = 20;
    protected const ulong botID = 64646464646464;

    public virtual void Start()
    {
        au = this.GetComponent<AudioSource>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        currentPlayer = transform.parent.GetComponent<Player>();
        if (currentPlayer.IsOwner && currentPlayer.GetPlayerID() < botID)
        {
            currentPlayer.GetUIController().UpdateGunUI();
        }
        if (currentPlayer.IsOwner && currentPlayer.GetPlayerID() < botID)
        {
            showCrosshair = true;
        }
        else
        {
            showCrosshair = false;
            crosshair.gameObject.SetActive(false);
        }
        rightHand = currentPlayer.GetTracker().GetRightHand();
    }

    public virtual void Update()
    {
        float fireSpeed = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(),gunNameKey, ChangableWeaponStats.shotsPerSecond);
        if (oldFireSpeed != fireSpeed)
        {
            oldFireSpeed = fireSpeed;
            animator.SetFloat("Fire Speed", fireSpeed);
        }
        if (gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentClip) <= 0)
        {
            StartCoroutine(Reload(false));
        }
        if (gm != null)
        {
            currentFireAngle = CalculateFireAngle(currentPlayer);
            if (showCrosshair)
            {
                crosshair.position = CalculateHitPosition(currentFireAngle, currentPlayer, firePoint.position);
                crosshair.transform.LookAt(Camera.main.transform.position);
                crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * 1.5f);
            }
        }
        transform.position = rightHand.position;
        transform.rotation = rightHand.rotation;
    }

    public virtual void LateUpdate()
    {
        animator.SetBool("Fire", false);
        animator.SetBool("Reload", false);
    }

    public virtual void Fire()
    {
        float ammo = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentClip);
        if ((gunState == GunState.none || gunState == GunState.reloading) && ammo > 0)
        {
            if (au != null && gm != null)
            {
                au.PlayOneShot(fireSound);
                if (gm.IsHost)
                {
                    HitScanHostDamageCalculation(currentPlayer);
                    gm.SpawnProjectileClientRpc(currentPlayer.GetPlayerID());
                    gm.SetPlayerGunValueClientRpc(false, 0, currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentClip, ammo - 1);
                }
                gunState = GunState.firing;
                animator.SetBool("Fire", true);
                if (currentPlayer.IsOwner && currentPlayer.GetPlayerID() < botID)
                {
                    currentPlayer.GetUIController().UpdateGunUI();
                }
                StartCoroutine(Reload(true));
            }
        }
    }

    protected IEnumerator Reload(bool waitForFiring)
    {
        if (waitForFiring)
        {
            yield return new WaitForSeconds(1.0f / gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.shotsPerSecond));
            gunState = GunState.none;
        }
        float ammo = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentAmmo);
        float clip = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentClip);
        if (ammo > 0 && gunState == GunState.none)
        {
            animator.SetBool("Reload", true);
            gunState = GunState.reloading;
            float timeReloading = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.reloadSpeed);
            while (timeReloading > 0)
            {
                if (gunState != GunState.reloading)
                {
                    yield break;
                }
                timeReloading -= Time.deltaTime;
                yield return null;
            }
            if (gunState == GunState.reloading)
            {
                gunState = GunState.none;
                float bulletsNeeded = Mathf.Min(gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.maxClip) - clip,ammo);
                if (gm.IsHost)
                {
                    gm.SetPlayerGunValueClientRpc(false, 0, currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentClip, clip + bulletsNeeded);
                    gm.SetPlayerGunValueClientRpc(false, 0, currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.currentAmmo, ammo - bulletsNeeded);
                }
                if (currentPlayer.IsOwner && currentPlayer.GetPlayerID() < botID)
                {
                    currentPlayer.GetUIController().UpdateGunUI();
                }
            }
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
            int numBullets = (int)gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.bulletsPerShot);
            float angle = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.bulletSpreadAngle);
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
                currentProjectile.GetComponent<Projectile>().SetProjectile(firePoint.position, finalAngle, gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.bulletSpeed), player.GetTeamLayer(), CalculateHitPosition(finalAngle, player, firePoint.position), 1 / numBullets);
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
        switch (gm.FindPlayerTeam(player.GetPlayerID()))
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
        int numBullets = (int)gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.bulletsPerShot);
        float angle = gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.bulletSpreadAngle);
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
                        int damage = Mathf.CeilToInt(Mathf.Max(0, Mathf.SmoothStep(gm.FindPlayerGunValue(currentPlayer.GetPlayerID(), gunNameKey, ChangableWeaponStats.damage), 0, hit.distance / maxDamageFalloff)));
                        if (damage > 0)
                        {
                            hitPlayer.ChangeHealth(player.GetPlayerID(), -damage, bulletIdHash);
                        }
                    }
                }
            }
        }
    }

    public void SetDefaultStats(GunProjectiles stat)
    {
        defaultStats = stat;
    }

    public virtual void AltFire()
    {

    }
    public string GetNameKey()
    {
        return gunNameKey;
    }

    public WeaponStats[] GetWeaponStats()
    {
        return changableStats.ToArray();
    }
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
public enum GunState
{
    none,
    firing,
    altFiring,
    reloading
}