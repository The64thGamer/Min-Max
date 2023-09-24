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
    bool stateFinishedChanging;
    NetworkVariable<bool> currentState = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    GlobalManager gm;
    private void Start()
    {
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
    }


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
                    if (respawningTimer <= 0)
                    {
                        RespawnDespawn(true);
                        stateFinishedChanging = true;
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

                        for (int i = 0; i < currentPlayer.Count; i++)
                        {
                            if (currentPlayer[i] != null)
                            {
                                if (TakeDamage(i))
                                {
                                    stateFinishedChanging = false;
                                    respawningTimer = respawnTime;
                                    RespawnDespawn(false);
                                    return;
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
        ulong id = currentPlayer[i].GetPlayerID();
        switch (dispenseType)
        {
            case HealthChangerVariant.health:
                int oldHealth = (int)gm.FindPlayerStat(id, ChangablePlayerStats.currentHealth);
                int healthFinal = health;
                if (healthIsPercent)
                {
                    healthFinal = Mathf.CeilToInt((gm.FindPlayerStat(id, ChangablePlayerStats.maxHealth) / 100.0f) * health);
                }
                if (dontKill && oldHealth + healthFinal <= 0)
                {
                    healthFinal = -oldHealth + 1;
                }

                if (currentPlayer[i].ChangeHealth(id, healthFinal, Random.Range(-999999999, 999999999)) == oldHealth)
                {
                    return false;
                }
                else
                {
                    if (gm.FindPlayerStat(id, ChangablePlayerStats.currentHealth) + healthFinal <= 0)
                    {
                        currentPlayer[i] = null;
                    }
                    return true;
                }
            case HealthChangerVariant.ammo:
                int ammoFinal = health;
                string gun = gm.FindPlayerGun(id, (int)gm.FindPlayerStat(id, ChangablePlayerStats.currentlyHeldWeapon));
                if(gm.FindPlayerGunValue(id,gun,ChangableWeaponStats.currentAmmo) >= gm.FindPlayerGunValue(id, gun, ChangableWeaponStats.maxAmmo))
                {
                    return false;
                }
                if (healthIsPercent)
                {
                    ammoFinal = Mathf.CeilToInt((gm.FindPlayerGunValue(id, gun, ChangableWeaponStats.maxAmmo) / 100.0f) * health);
                }
                ammoFinal = Mathf.Min(ammoFinal, (int)gm.FindPlayerGunValue(id, gun, ChangableWeaponStats.maxAmmo));
                if(IsHost)
                {
                    gm.SetPlayerGunValueClientRpc(false, 0, id, gun, ChangableWeaponStats.currentAmmo, ammoFinal);
                }
                return true;
            default:
                break;
        }
        return false;
    }

    void ClientRespawn(bool prev, bool current)
    {
        if (!IsHost)
        {
            RespawnDespawn(currentState.Value);
        }
    }

    void RespawnDespawn(bool respawn)
    {
        if (!stateFinishedChanging)
        {
            Renderer[] rend = this.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rend.Length; i++)
            {
                rend[i].enabled = respawn;
            }
            this.GetComponent<BoxCollider>().enabled = respawn;
            if (!respawn)
            {
                Debug.Log("Pack Despawned");
                currentPlayer = new List<Player>();
            }
            else
            {
                Debug.Log("Pack Respawned");
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