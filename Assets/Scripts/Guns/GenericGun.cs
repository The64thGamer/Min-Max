using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GenericGun : Gun
{
    [SerializeField]
    List<WeaponStats> changableStats = new List<WeaponStats>()
    {
        new WeaponStats(){ statName = ChangableWeaponStats.maxAmmo, stat = 5},
        new WeaponStats(){ statName = ChangableWeaponStats.bulletsPerShot, stat = 1},
        new WeaponStats(){ statName = ChangableWeaponStats.shotsPerSecond, stat = 3},
        new WeaponStats(){ statName = ChangableWeaponStats.bulletSpeed, stat = 3},
    };
    [SerializeField] Transform firePoint;
    [SerializeField] Transform crosshair;
    [SerializeField] Player currentPlayer;
    [SerializeField] AudioClip fireSound;
    [SerializeField] string gunNameKey;

    int currentAmmo;
    float fireCooldown;
    Vector3 currentFireAngle;
    AudioSource au;
    GlobalManager gm;
    bool showCrosshair;
    Transform currentParent;
    GunProjectiles defaultStats;

    public void Start()
    {
        currentAmmo = SearchStats(ChangableWeaponStats.maxAmmo);
        au = this.GetComponent<AudioSource>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        defaultStats = gm.GetComponent<AllStats>().SearchGuns(GetNameKey());
        if (currentPlayer.IsOwner)
        {
            showCrosshair = true;
        }
        else
        {
            Destroy(crosshair.gameObject.GetComponent<Image>());
        }
    }
    void Update()
    {
        if (currentParent != null)
        {
            transform.position = currentParent.position;
            transform.rotation = currentParent.rotation;
            transform.localScale = currentParent.localScale;
        }

        if (gm != null)
        {
            Vector3 firePos = currentPlayer.GetTracker().GetRightHandFirePos(defaultStats.firepoint);
            currentFireAngle = gm.CalculateFireAngle(currentPlayer, firePos);
            if (showCrosshair)
            {
                crosshair.position = gm.CalculcateFirePosition(currentFireAngle, currentPlayer, firePos);
                crosshair.transform.LookAt(Camera.main.transform.position);
                crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * 1.5f);
            }
            if (fireCooldown > 0)
            {
                fireCooldown = Mathf.Max(0, fireCooldown - Time.deltaTime);
            }
        }
    }


    public override void Fire()
    {
        if (gm != null && au != null)
        {
            if (fireCooldown <= 0)
            {
                au.PlayOneShot(fireSound);
                fireCooldown = 1.0f / SearchStats(ChangableWeaponStats.shotsPerSecond);
                gm.SpawnProjectile(currentPlayer);
            }
        }
    }
    public override void AltFire() { }

    public override void SetPlayer(Player player)
    {
        currentPlayer = player;
    }

    public override List<WeaponStats> ChangableStats()
    {
        return null;
    }
    public override int GetCurrentAmmo()
    {
        return 0;
    }

    public override string GetNameKey()
    {
        return gunNameKey;
    }
    public override int SearchStats(ChangableWeaponStats stat)
    {
        for (int i = 0; i < changableStats.Count; i++)
        {
            if (changableStats[i].statName == stat)
            {
                return changableStats[i].stat;
            }
        }
        return 0;
    }

    public override void SetGunTransformParent(Transform parent)
    {
        currentParent = parent;
    }

}
