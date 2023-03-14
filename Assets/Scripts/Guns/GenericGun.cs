using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    void Start()
    {
        currentAmmo = SearchStats(ChangableWeaponStats.maxAmmo);
        au = this.GetComponent<AudioSource>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
    }
    void Update()
    {
        currentFireAngle = gm.CalculateFireAngle(currentPlayer,crosshair);
        if (fireCooldown > 0)
        {
            fireCooldown = Mathf.Max(0, fireCooldown - Time.deltaTime);
        }
    }
    public override void Fire()
    {
        if (fireCooldown <= 0)
        {
            au.PlayOneShot(fireSound);
            fireCooldown = 1.0f / SearchStats(ChangableWeaponStats.shotsPerSecond);
            gm.SpawnProjectile(gunNameKey, firePoint.rotation, firePoint.position, currentFireAngle, SearchStats(ChangableWeaponStats.bulletSpeed), currentPlayer.GetTeamLayer(), crosshair.position);
        }
    }
    public override void AltFire() { }
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

    int SearchStats(ChangableWeaponStats stat)
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
}
