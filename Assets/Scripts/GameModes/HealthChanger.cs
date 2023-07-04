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
    [SerializeField] bool healthIsPercent;
    [SerializeField] bool spinObject;

    float loopTimer;
    float respawningTimer;
    List<Player> currentPlayer = new List<Player>();
    bool alreadyDespawn;

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
                if(respawningTimer == 0)
                {
                    RespawnDespawn(true);
                    alreadyDespawn = true;
                }
                break;
            case DamageChangerSettings.loopWhileInside:
                loopTimer = Mathf.Max(0, loopTimer - Time.deltaTime);
                AttemptHealthChange();
                break;
            default:
                break;
        }
        if(spinObject)
        {
            this.transform.Rotate(Vector3.up,Time.deltaTime * 100);
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
                        alreadyDespawn = false;
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
                    currentPlayer = new List<Player>();
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
                int healthFinal = health;
                if(healthIsPercent)
                {
                    healthFinal = Mathf.CeilToInt((currentPlayer[i].GetClassStats().baseHealth / 100.0f) * health);
                }
                if (currentPlayer[i].GetHealth() + healthFinal <= 0)
                {
                    currentPlayer[i] = null;
                }
                currentPlayer[i].ChangeHealth(currentPlayer[i].GetPlayerID(), healthFinal,Random.Range(-999999999,999999999));
            }
        }
    }

    void RespawnDespawn(bool respawn)
    {
        if (!alreadyDespawn)
        {
            Renderer[] rend = this.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rend.Length; i++)
            {
                rend[i].enabled = respawn;
            }
            this.GetComponent<BoxCollider>().enabled = respawn;
            if (!respawn)
            {
                currentPlayer = new List<Player>();
            }
        }
    }


    enum DamageChangerSettings
    {
        standard,
        despawnOnTouch,
        loopWhileInside,
    }
}
