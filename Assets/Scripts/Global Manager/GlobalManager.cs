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
}
