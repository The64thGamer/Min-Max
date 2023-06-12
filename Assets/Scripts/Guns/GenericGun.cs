using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GenericGun : Gun
{
    [SerializeField] Transform firePoint;
    [SerializeField] Transform crosshair;
    [SerializeField] Player currentPlayer;
    [SerializeField] AudioClip fireSound;
    [SerializeField] string gunNameKey;
    [SerializeField] Transform superJank;

    float fireCooldown;
    Vector3 currentFireAngle;
    AudioSource au;
    GlobalManager gm;
    bool showCrosshair;
    Transform currentParent;
    bool dumbstupidjank;

    public void Start()
    {
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

            //Dumb stupid jank, please remove this and do proper weapon animations
            if(dumbstupidjank)
            {
                //playermodel version jank
                transform.rotation = superJank.rotation;
            }
            else
            {
                //vr mode jank
                transform.localPosition += transform.up * -.15f;
                transform.localPosition += transform.forward * -.1f;
            }
        }

        if (gm != null)
        {
            Vector3 firePos = currentPlayer.GetTracker().GetRightHandFirePos(defaultStats.firepoint);
            currentFireAngle = CalculateFireAngle(currentPlayer);
            if (showCrosshair)
            {
                crosshair.position = CalculateHitPosition(currentFireAngle, currentPlayer, firePos);
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
                fireCooldown = 1.0f / FindStat(ChangableWeaponStats.shotsPerSecond);

                if(gm.IsHost)
                {
                    HitScanHostDamageCalculation(currentPlayer);
                    gm.SpawnProjectileClientRpc(currentPlayer.GetPlayerID());
                }
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

    public override void SetGunTransformParent(Transform parent, bool dumbStupidJank)
    {
        currentParent = parent;
        dumbstupidjank = dumbStupidJank;
    }

}
