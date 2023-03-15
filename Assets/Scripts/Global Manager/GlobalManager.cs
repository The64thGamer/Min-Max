using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using static PlayerTracker;

public class GlobalManager : MonoBehaviour
{
    AllStats al;
    private void Start()
    {
        al = GetComponent<AllStats>();
    }

    [SerializeField] Player host;
    [SerializeField] List<Player> clients;
    [SerializeField] float serverTimeForgiveness;
    [SerializeField] LayerMask vrLayers;

    const float MINANGLE = 0.8f;
    const float SPHERESIZE = 0.4f;
    const float MAXSPHERECASTDISTANCE = 20;
    const float MAXRAYCASTDISTANCE = 1000;

    private void Update()
    {
        for (int i = 0; i < clients.Count; i++)
        {
            CheckAllPlayerInputs(clients[i]);
        }
        CheckAllPlayerInputs(host);
    }

    void CheckAllPlayerInputs(Player player)
    {
        if (player.GetTracker().GetTriggerR() == ButtonState.started || player.GetTracker().GetTriggerR() == ButtonState.on)
        {
            //player.GetCurrentGun().Fire();
            host.GetCurrentGun().Fire();
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].GetCurrentGun().Fire();
            }
        }
    }

    //Exploit: Hit needs to be parsed to ensure extreme angles aren't achievable.
    public void SpawnProjectile(Player player)
    {
        Transform rHand = player.GetTracker().GetRightHand();
        Vector3 firePoint = rHand.position; //+ rHand.TransformPoint(al.SearchGuns(player.GetCurrentGun().GetNameKey()).firepoint);
        Quaternion fpRotation = rHand.rotation;

        GunProjectiles fp = al.SearchGuns(player.GetCurrentGun().GetNameKey());
        if (fp.firePrefab != null)
        {
            GameObject currentProjectile = GameObject.Instantiate(fp.firePrefab);
            Vector3 fireAngle = CalculateFireAngle(player);
            currentProjectile.GetComponent<Projectile>().SetProjectile(firePoint, fireAngle, player.GetCurrentGun().SearchStats(ChangableWeaponStats.bulletSpeed), player.GetTeamLayer(), CalculcateFirePosition(fireAngle, player));
        }
    }

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    public Vector3 CalculateFireAngle(Player player)
    {
        RaycastHit hit;
        Vector3 startCast = player.GetTracker().GetCamera().position + (player.GetTracker().GetCamera().forward * SPHERESIZE);
        Vector3 finalAngle = Vector3.one;

        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);

        Transform rHand = player.GetTracker().GetRightHand();
        Vector3 firePoint = rHand.position + rHand.TransformPoint(al.SearchGuns(player.GetCurrentGun().GetNameKey()).firepoint);
        Vector3 fpForward = rHand.forward;

        if (Physics.SphereCast(startCast, SPHERESIZE, player.GetTracker().GetCamera().forward, out hit, MAXSPHERECASTDISTANCE, layermask))
        {
            finalAngle = ((startCast + (player.GetTracker().GetCamera().forward * hit.distance)) - firePoint);
        }
        else
        {
            finalAngle = player.GetTracker().GetCamera().forward;
        }
        float dotAngle = Vector3.Dot(fpForward, finalAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            float percentage = (dotAngle - MINANGLE) / (1 - MINANGLE);
            finalAngle = Vector3.Slerp(fpForward, finalAngle, percentage);
            return finalAngle;
        }
        return fpForward;
    }

    public Vector3 CalculcateFirePosition(Vector3 fireAngle, Player player)
    {
        RaycastHit hit;
        Transform rHand = player.GetTracker().GetRightHand();
        Vector3 firePoint = rHand.position; // + rHand.TransformPoint(al.SearchGuns(player.GetCurrentGun().GetNameKey()).firepoint);
        if (player == host)
        {
            postest1 = rHand.position;
            postest2 = firePoint;
        }
        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);
        float dotAngle = Vector3.Dot(rHand.forward, fireAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            if (Physics.Raycast(firePoint, fireAngle, out hit, MAXRAYCASTDISTANCE, layermask))
            {
                return hit.point;
            }
        }
        if (Physics.Raycast(firePoint, rHand.forward, out hit, MAXRAYCASTDISTANCE, layermask))
        {
            return hit.point;
        }
        return firePoint + (100 * fireAngle);
    }

    Vector3 postest1;
    Vector3 postest2;
    Vector3 ray1;
    Vector3 ray2;

    private void OnDrawGizmos()
    {
        Gizmos.DrawCube(postest1, Vector3.one * 0.3f);
        Gizmos.DrawCube(postest2, Vector3.one * 0.3f);
        Gizmos.DrawLine(ray1, ray2);
    }

    public LayerMask GetIgnoreTeamAndVRLayerMask(Player player)
    {
        LayerMask mask;
        switch (player.GetTeam())
        {
            case Team.team1:
                mask = 1 << LayerMask.NameToLayer("Team1");
                break;
            case Team.team2:
                mask = 1 << LayerMask.NameToLayer("Team2");
                break;
            default:
                mask = 1 << LayerMask.NameToLayer("Neutral");
                break;
        }
        mask = mask | vrLayers;
        mask = ~mask;
        return mask;
    }

    public bool IsPlayerHost(Player player)
    {
        if(player == host)
        {
            return true;
        }
        return false;
    }
}
