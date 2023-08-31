using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class Flashpoint : Gun
{
    [SerializeField] Transform firePoint;
    [SerializeField] Transform crosshair;
    [SerializeField] Player currentPlayer;
    [SerializeField] AudioClip fireSound;
    [SerializeField] string gunNameKey;
    [SerializeField] Transform superJank;
    [SerializeField] Light spotLight;
    BoxCollider boxCl;
    float fireCooldown;
    Vector3 currentFireAngle;
    AudioSource au;
    GlobalManager gm;
    bool showCrosshair;
    Transform currentParent;
    bool dumbstupidjank;

    List<Player> playersInTrigger;

    public void Start()
    {
        playersInTrigger = new List<Player>();
        au = this.GetComponent<AudioSource>();
        boxCl = this.GetComponent<BoxCollider>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        if (currentPlayer.IsOwner)
        {
            showCrosshair = true;
        }
        else
        {
            Destroy(crosshair.gameObject.GetComponent<Image>());
        }
    }
    void FixedUpdate()
    {
        if (currentParent != null)
        {
            transform.position = currentParent.position;
            transform.rotation = currentParent.rotation;
            transform.localScale = currentParent.localScale;

            //Dumb stupid jank, please remove this and do proper weapon animations
            if (dumbstupidjank)
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

        //Box stuff
        spotLight.range = FindStat(ChangableWeaponStats.maxBulletRange);
        spotLight.spotAngle = FindStat(ChangableWeaponStats.bulletSpreadAngle);
        spotLight.innerSpotAngle = spotLight.spotAngle;
        boxCl.size = spotLight.range * Vector3.one;
        boxCl.center = new Vector3(0, 0, spotLight.range / 2.0f);
    }


    public override void Fire()
    {
        if (gm != null && au != null)
        {
            if (fireCooldown <= 0)
            {
                au.PlayOneShot(fireSound);
                fireCooldown = 1.0f / FindStat(ChangableWeaponStats.shotsPerSecond);

                if (gm.IsHost)
                {
                    HitScanHostDamageCalculation(null);
                }
            }
        }
    }

    protected override void HitScanHostDamageCalculation(Player player)
    {
        Vector3 firepos = currentPlayer.GetTracker().GetRightHandFirePos(defaultStats.firepoint);
        int bulletIdHash = UnityEngine.Random.Range(-999999999, 999999999);

        for (int i = 0; i < playersInTrigger.Count; i++)
        {
            if (playersInTrigger[i] != null)
            {
                Vector3 fireAngle = ((playersInTrigger[i].transform.position + new Vector3(0, 0.75f, 0)) - firepos).normalized;
                LayerMask layermask = GetIgnoreTeamAndVRLayerMask(currentPlayer);
                Debug.DrawRay(firepos, fireAngle * FindStat(ChangableWeaponStats.maxBulletRange));
                RaycastHit[] hit = Physics.RaycastAll(firepos, fireAngle, FindStat(ChangableWeaponStats.maxBulletRange), layermask);
                for (int e = 0; e < hit.Length; e++)
                {
                    if (hit[e].collider.GetComponent<Player>() == playersInTrigger[i])
                    {
                        int damage = Mathf.CeilToInt(Mathf.Max(0, Mathf.SmoothStep(FindStat(ChangableWeaponStats.damage), 0, hit[e].distance / FindStat(ChangableWeaponStats.maxBulletRange))));
                        if (damage > 0)
                        {
                            playersInTrigger[i].ChangeHealth(currentPlayer.GetPlayerID(), -damage, bulletIdHash);
                        }
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p != null)
        {
            playersInTrigger.Add(p);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p != null)
        {
            for (int i = 0; i < playersInTrigger.Count; i++)
            {
                if (playersInTrigger[i] == p)
                {
                    playersInTrigger.RemoveAt(i);
                    return;
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