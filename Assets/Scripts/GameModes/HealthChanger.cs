using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(NetworkObject))]
[ExecuteInEditMode]
public class HealthChanger : NetworkBehaviour
{

    [Header("Setting")]
    [SerializeField] DamageChangerSettings setting;
    [SerializeField] HealthChangerVariant dispenseType;


    [Header("All Modes")]
    [SerializeField] int health;
    [SerializeField] bool healthIsPercent;
    [SerializeField] bool spinObject;
    [SerializeField] bool dontKill;

    [Header("Despawn Mode")]
    [SerializeField] float respawnTime;

    [Header("Loop Mode")]
    [SerializeField] float looptime;

    float loopTimer;
    float respawningTimer;
    List<Player> currentPlayer = new List<Player>();
    bool alreadyDespawn;
    NetworkVariable<bool> currentState = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    public override void OnNetworkSpawn()
    {
        if (!IsHost)
        {
            currentState.OnValueChanged += ClientRespawn;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsHost)
        {
            currentState.OnValueChanged -= ClientRespawn;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsHost)
        {
            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                currentPlayer.Add(player);
                AttemptHealthChange();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsHost)
        {
            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                currentPlayer.Remove(player);
                AttemptHealthChange();
            }
        }
    }

    private void Update()
    {
        if (IsHost)
        {
            switch (setting)
            {
                case DamageChangerSettings.despawnOnTouch:
                    respawningTimer = Mathf.Max(0, respawningTimer - Time.deltaTime);
                    if (respawningTimer == 0)
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
        }
        if (spinObject)
        {
            this.transform.Rotate(Vector3.up, Time.deltaTime * 100);
        }
    }

    private void OnRenderObject()
    {
        if (spinObject)
        {
            this.transform.Rotate(Vector3.up, Time.deltaTime * 100);
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
                        for (int i = 0; i < currentPlayer.Count; i++)
                        {
                            if (currentPlayer[i] != null)
                            {
                                if (TakeDamage(i))
                                {
                                    RespawnDespawn(false);
                                }
                            }
                        }
                        
                    }
                    break;
                case DamageChangerSettings.loopWhileInside:
                    if (loopTimer <= 0)
                    {
                        loopTimer = looptime;
                        for (int i = 0; i < currentPlayer.Count; i++)
                        {
                            if (currentPlayer[i] != null)
                            {
                                TakeDamage(i);
                            }
                        }

                    }
                    break;
                default:
                    for (int i = 0; i < currentPlayer.Count; i++)
                    {
                        if (currentPlayer[i] != null)
                        {
                            TakeDamage(i);
                        }
                    }
                    currentPlayer = new List<Player>();
                    break;
            }
        }
    }

    bool TakeDamage(int i)
    {
        switch (dispenseType)
        {
            case HealthChangerVariant.health:
                int oldHealth = currentPlayer[i].GetHealth();
                int healthFinal = health;
                if (healthIsPercent)
                {
                    healthFinal = Mathf.CeilToInt((currentPlayer[i].GetClassStats().baseHealth / 100.0f) * health);
                }
                if (dontKill && oldHealth + healthFinal <= 0)
                {
                    healthFinal = -oldHealth + 1;
                }

                if (currentPlayer[i].ChangeHealth(currentPlayer[i].GetPlayerID(), healthFinal, Random.Range(-999999999, 999999999)) == oldHealth)
                {
                    return false;
                }
                else
                {
                    if (currentPlayer[i].GetHealth() + healthFinal <= 0)
                    {
                        currentPlayer[i] = null;
                    }
                    return true;
                }
            case HealthChangerVariant.ammo:
                //AAAAAAAAAAAAAAAAAAA
                break;
            default:
                break;
        }
        return false;
    }

    void ClientRespawn(bool prev, bool current)
    {
        RespawnDespawn(currentState.Value);
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
            if(IsHost)
            {
                currentState.Value = respawn;
            }
        }
    }

    public DamageChangerSettings GetSetting()
    {
        return setting;
    }

    public enum HealthChangerVariant
    {
        health,
        ammo,
    }

    public enum DamageChangerSettings
    {
        standard,
        despawnOnTouch,
        loopWhileInside,
    }
}