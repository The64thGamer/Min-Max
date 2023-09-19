using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GenericGun : Gun
{
    [SerializeField] Transform crosshair;
    [SerializeField] AudioClip fireSound;

    Vector3 currentFireAngle;
    AudioSource au;
    GlobalManager gm;
    bool showCrosshair;
    Transform rightHand;

    public override void Start()
    {
        base.Start();
        au = this.GetComponent<AudioSource>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        if (currentPlayer.IsOwner)
        {
            showCrosshair = true;
        }
        else
        {
            Destroy(crosshair.gameObject.GetComponent<Image>());
        }
        rightHand = currentPlayer.GetTracker().GetRightHand();
    }
    public override void Update()
    {
        base.Update();
        if (gm != null)
        {
            currentFireAngle = CalculateFireAngle(currentPlayer);
            if (showCrosshair)
            {
                crosshair.position = CalculateHitPosition(currentFireAngle, currentPlayer, firePoint.position);
                crosshair.transform.LookAt(Camera.main.transform.position);
                crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * 1.5f);
            }
            if (fireCooldown > 0)
            {
                fireCooldown = Mathf.Max(0, fireCooldown - Time.deltaTime);
            }
        }
        transform.position = rightHand.position;
        transform.rotation = rightHand.rotation;
    }

    public override void Fire()
    {
        if (fireCooldown <= 0)
        {
            au.PlayOneShot(fireSound);

            if (gm.IsHost)
            {
                HitScanHostDamageCalculation(currentPlayer);
                gm.SpawnProjectileClientRpc(currentPlayer.GetPlayerID());
            }
        }
        base.Fire();
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

}
