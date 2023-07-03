using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class HealthChanger : MonoBehaviour
{
    [SerializeField] int health;
    [SerializeField] DamageChangerSettings setting;
    [SerializeField] float respawnTime;
    [SerializeField] float looptime;

    float loopTimer;
    float respawningTimer;
    List<Player> currentPlayer;

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            currentPlayer.Add(player);
            AttemptHealthChange();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            currentPlayer.Remove(player);
            AttemptHealthChange();
        }
    }

    private void Update()
    {
        switch (setting)
        {
            case DamageChangerSettings.despawnOnTouch:
                respawningTimer = Mathf.Max(0, respawningTimer - Time.deltaTime);
                RespawnDespawn(true);
                break;
            case DamageChangerSettings.loopWhileInside:
                loopTimer = Mathf.Max(0, loopTimer - Time.deltaTime);
                AttemptHealthChange();
                break;
            default:
                break;
        }
    }

    void AttemptHealthChange()
    {
        if (currentPlayer.Count != 0)
        {
            switch (setting)
            {
                case DamageChangerSettings.despawnOnTouch:
                    if (respawningTimer <= 0)
                    {
                        respawningTimer = respawnTime;
                        TakeDamage();
                        RespawnDespawn(false);
                    }
                    break;
                case DamageChangerSettings.loopWhileInside:
                    if (loopTimer <= 0)
                    {
                        loopTimer = looptime;
                        TakeDamage();
                    }
                    break;
                default:
                    TakeDamage();
                    break;
            }
        }
    }

    void TakeDamage()
    {
        for (int i = 0; i < currentPlayer.Count; i++)
        {
            if (currentPlayer != null)
            {
                currentPlayer[i].TakeDamage(currentPlayer[i].GetPlayerID(), health);
            }
        }
    }

    void RespawnDespawn(bool respawn)
    {
        Renderer[] rend = this.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rend.Length; i++)
        {
            rend[i].enabled = respawn;
        }
        this.GetComponent<BoxCollider>().enabled = respawn;
        if(!respawn)
        {
            currentPlayer = new List<Player>();
        }
    }


    enum DamageChangerSettings
    {
        standard,
        despawnOnTouch,
        loopWhileInside,
    }
}
