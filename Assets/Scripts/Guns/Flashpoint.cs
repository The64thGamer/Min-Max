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
    [SerializeField] Light spotLight;
    BoxCollider boxCl;
    List<Player> playersInTrigger;

    public new void Start()
    {
        base.Start();
        playersInTrigger = new List<Player>();
        boxCl = this.GetComponent<BoxCollider>();
    }

    new void Update()
    {
        base.Update();
        //Box stuff
        spotLight.range = FindStat(ChangableWeaponStats.maxBulletRange);
        spotLight.spotAngle = FindStat(ChangableWeaponStats.bulletSpreadAngle);
        spotLight.innerSpotAngle = spotLight.spotAngle;
        boxCl.size = spotLight.range * Vector3.one;
        boxCl.center = new Vector3(0, 0, spotLight.range / 2.0f);
    }

    public new void Fire()
    {
        float ammo = FindStat(ChangableWeaponStats.currentClip);

        if ((gunState == GunState.none || gunState == GunState.reloading) && ammo > 0)
        {
            au.PlayOneShot(fireSound);
            if (gm.IsHost)
            {
                HitScanHostDamageCalculation(null);
            }
            SetStat(ChangableWeaponStats.currentClip, ammo - 1);
            gunState = GunState.firing;
            animator.SetBool("Fire", true);
            if (currentPlayer.IsOwner && currentPlayer.GetPlayerID() < botID)
            {
                currentPlayer.GetUIController().UpdateGunUI();
            }
            base.StartCoroutine(Reload(true));
        }
    }

    protected override void HitScanHostDamageCalculation(Player player)
    {
        int bulletIdHash = UnityEngine.Random.Range(-999999999, 999999999);

        for (int i = 0; i < playersInTrigger.Count; i++)
        {
            if (playersInTrigger[i] != null)
            {
                Vector3 fireAngle = ((playersInTrigger[i].transform.position + new Vector3(0, 0.75f, 0)) - firePoint.position).normalized;
                LayerMask layermask = GetIgnoreTeamAndVRLayerMask(currentPlayer);
                Debug.DrawRay(firePoint.position, fireAngle * FindStat(ChangableWeaponStats.maxBulletRange));
                RaycastHit[] hit = Physics.RaycastAll(firePoint.position, fireAngle, FindStat(ChangableWeaponStats.maxBulletRange), layermask);
                System.Array.Sort(hit, (x, y) => x.distance.CompareTo(y.distance));

                Player p;
                for (int e = 0; e < hit.Length; e++)
                {
                    p = hit[e].collider.GetComponent<Player>();
                    if (p == null)
                    {
                        break;
                    }
                    if (p == playersInTrigger[i])
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

}
