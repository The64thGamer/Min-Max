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
    [SerializeField] GameObject projectile;
    [SerializeField] Transform crosshair;
    [SerializeField] Player currentPlayer;

    int currentAmmo;
    float fireCooldown;
    Vector3 currentFireAngle;

    void Start()
    {
        currentAmmo = SearchStats(ChangableWeaponStats.maxAmmo);
    }
    void Update()
    {
        currentFireAngle = CalculateFireAngle();
        if (fireCooldown > 0)
        {
            fireCooldown = Mathf.Max(0, fireCooldown - Time.deltaTime);
        }
    }
    public override void Fire()
    {
        if (fireCooldown <= 0)
        {
            fireCooldown = 1.0f / SearchStats(ChangableWeaponStats.shotsPerSecond);
            GameObject currentProjectile = GameObject.Instantiate(projectile, firePoint.position, firePoint.rotation);
            currentProjectile.GetComponent<Projectile>().SetProjectile(firePoint.position, currentFireAngle, SearchStats(ChangableWeaponStats.bulletSpeed), currentPlayer.GetTeamLayer());
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

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    Vector3 CalculateFireAngle()
    {
        const float MINANGLE = 0.8f;
        const float SPHERESIZE = 0.4f;
        const float MAXSPHERECASTDISTANCE = 20;
        const float CROSSHAIRDISTANCESCALE = 0.5f;
        const float MAXRAYCASTDISTANCE = 1000;

        RaycastHit hit;
        Vector3 startCast = Camera.main.transform.position + (Camera.main.transform.forward * SPHERESIZE);
        Vector3 finalAngle = Vector3.one;

        LayerMask layermask = currentPlayer.GetIgnoreTeamAndVRLayerMask();


        if (Physics.SphereCast(startCast, SPHERESIZE, Camera.main.transform.forward, out hit, MAXSPHERECASTDISTANCE, layermask))
        {
            finalAngle = ((startCast + (Camera.main.transform.forward * hit.distance)) - firePoint.position);
        }
        else
        {
            finalAngle = Camera.main.transform.forward;
        }
        float dotAngle = Vector3.Dot(firePoint.forward, finalAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            float percentage = (dotAngle - MINANGLE) / (1 - MINANGLE);
            finalAngle = Vector3.Slerp(firePoint.forward, finalAngle, percentage);
            if (Physics.Raycast(firePoint.transform.position, finalAngle, out hit, MAXRAYCASTDISTANCE, layermask))
            {
                crosshair.position = hit.point;
                crosshair.transform.LookAt(Camera.main.transform.position);
                crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * CROSSHAIRDISTANCESCALE);
            }
            return finalAngle;
        }
        if (Physics.Raycast(firePoint.transform.position, firePoint.transform.forward, out hit, MAXRAYCASTDISTANCE, layermask))
        {
            crosshair.position = hit.point;
            crosshair.transform.LookAt(Camera.main.transform.position);
            crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * CROSSHAIRDISTANCESCALE);
        }
        return firePoint.forward;
    }
}
