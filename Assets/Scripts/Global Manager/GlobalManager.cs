using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public class GlobalManager : MonoBehaviour
{
    AllStats al;
    private void Start()
    {
        al = GetComponent<AllStats>();
    }

    [SerializeField] Player host;
    [SerializeField] List<ClientPlayer> clients;
    [SerializeField] float serverTimeForgiveness;

    //Exploit: Hit needs to be parsed to ensure extreme angles aren't achievable.
    public void SpawnProjectile(string gunNameKey, Quaternion rot, Vector3 pos, Vector3 forw, float setSpeed, int layer, Vector3 hit)
    {
        GunProjectiles fp = al.SearchGuns(gunNameKey);
        if (fp.firePrefab != null)
        {
            GameObject currentProjectile = GameObject.Instantiate(fp.firePrefab, pos, rot);
            currentProjectile.GetComponent<Projectile>().SetProjectile(pos, forw, setSpeed, layer, hit);
        }
    }

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    public Vector3 CalculateFireAngle(Player player, Transform crosshair)
    {
        const float MINANGLE = 0.8f;
        const float SPHERESIZE = 0.4f;
        const float MAXSPHERECASTDISTANCE = 20;
        const float CROSSHAIRDISTANCESCALE = 0.5f;
        const float MAXRAYCASTDISTANCE = 1000;

        RaycastHit hit;
        Vector3 startCast = Camera.main.transform.position + (Camera.main.transform.forward * SPHERESIZE);
        Vector3 finalAngle = Vector3.one;

        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);

        Transform rHand = player.GetRightHand();
        Vector3 firePoint = rHand.position + rHand.TransformPoint(al.SearchGuns(player.GetCurrentGun().GetNameKey()).firepoint);
        Vector3 fpForward = rHand.forward;

        if (Physics.SphereCast(startCast, SPHERESIZE, Camera.main.transform.forward, out hit, MAXSPHERECASTDISTANCE, layermask))
        {
            finalAngle = ((startCast + (Camera.main.transform.forward * hit.distance)) - firePoint);
        }
        else
        {
            finalAngle = Camera.main.transform.forward;
        }
        float dotAngle = Vector3.Dot(fpForward, finalAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            float percentage = (dotAngle - MINANGLE) / (1 - MINANGLE);
            finalAngle = Vector3.Slerp(fpForward, finalAngle, percentage);
            if (Physics.Raycast(firePoint, finalAngle, out hit, MAXRAYCASTDISTANCE, layermask))
            {
                crosshair.position = hit.point;
                crosshair.transform.LookAt(Camera.main.transform.position);
                crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * CROSSHAIRDISTANCESCALE);
            }
            return finalAngle;
        }
        if (Physics.Raycast(firePoint, fpForward, out hit, MAXRAYCASTDISTANCE, layermask))
        {
            crosshair.position = hit.point;
            crosshair.transform.LookAt(Camera.main.transform.position);
            crosshair.localScale = Vector3.one + (Vector3.one * (crosshair.position - Camera.main.transform.position).magnitude * CROSSHAIRDISTANCESCALE);
        }
        return fpForward;
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
        mask = mask | player.GetVRLayers();
        mask = ~mask;
        return mask;
    }
}
