using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;

public class Player : NetworkBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] TeamList currentTeam;
    [SerializeField] ClassList currentClass;
    [SerializeField] ClassStats currentStats;
    [SerializeField] GameObject[] playerModels;
    [SerializeField] List<Cosmetic> cosmetics;
    WireSounds wireSounds;
    PlayerTracker tracker;
    FirstPersonController controller;
    GlobalManager gm;
    AllStats al;
    bool currentPlayerVisibility;
    Wire.WirePoint heldWire;

    public override void OnNetworkSpawn()
    {
        //Before
        tracker = GetComponentInChildren<PlayerTracker>();
        controller = GetComponentInChildren<FirstPersonController>();
        currentGun = GetComponentInChildren<GenericGun>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        al = gm.GetComponent<AllStats>();
        gm.AddPlayerToClientList(this);
        wireSounds = transform.Find("WireSounds").GetComponent<WireSounds>();

        //Debug Default
        SetGun(gm.GetComponent<AllStats>().SearchGuns("Worker Ionizing Pistol"));

        //After
        if (IsOwner)
        {
            SetCharacterVisibility(false);
        }
        else
        {
            SetCharacterVisibility(true);
            Destroy(this.GetComponentInChildren<UniversalAdditionalCameraData>());
            Destroy(this.GetComponentInChildren<Camera>());
            Destroy(this.GetComponentInChildren<AudioListener>());
            TrackedPoseDriver[] pd = this.GetComponentsInChildren<TrackedPoseDriver>();
            for (int i = 0; i < pd.Length; i++)
            {
                Destroy(pd[i]);
            }
            ActionBasedController[] xd = this.GetComponentsInChildren<ActionBasedController>();
            for (int i = 0; i < xd.Length; i++)
            {
                Destroy(xd[i]);
            }
        }
    }

    void OnDestroy()
    {
        gm.DisconnectClient(this);
    }

    public void SetClass(ClassList setClass, int[] classCosmetics)
    {
        currentClass = setClass;
        currentStats = gm.GetComponent<AllStats>().GetClassStats(setClass);
        SetupCosmetics(classCosmetics);
        SetCharacterVisibility(currentPlayerVisibility);
        UpdateTeamColor();
    }

    public void SetTeam(TeamList team)
    {
        currentTeam = team;
        Transform[] allBits = this.GetComponentsInChildren<Transform>();
        for (int i = 0; i < allBits.Length; i++)
        {
            allBits[i].gameObject.layer = GetTeamLayer();
        }
        this.gameObject.layer = GetTeamLayer();
        UpdateTeamColor();
    }

    public void SetGun(GunProjectiles gun)
    {
        GameObject gunObject = GameObject.Instantiate(gun.gunPrefab, Vector3.zero, Quaternion.identity, this.transform);
        gunObject.GetComponent<Gun>().SetPlayer(this);
        currentGun = gunObject.GetComponent<Gun>();
        UpdateTeamColor();
    }

    void SetupCosmetics(int[] classCosmetics)
    {
        List<Cosmetic> stockCosmetics = gm.GetCosmetics().GetClassCosmetics(currentClass);
        cosmetics = new List<Cosmetic>();
        for (int i = 0; i < classCosmetics.Length; i++)
        {
            if (classCosmetics[i] < stockCosmetics.Count)
            {
                Cosmetic cm = stockCosmetics[classCosmetics[i]];
                bool isDupeEquipRegion = false;
                for (int e = 0; e < cosmetics.Count; e++)
                {
                    if (cosmetics[e].region == cm.region)
                    {
                        isDupeEquipRegion = true;
                    }
                }
                if (!isDupeEquipRegion)
                {
                    cosmetics.Add(cm);
                }
            }
        }
        //Stock
        for (int i = 0; i < stockCosmetics.Count; i++)
        {
            if (stockCosmetics[i].stock == StockCosmetic.stock && stockCosmetics[i].givenClass == currentClass)
            {
                bool isStockDupeEquipRegion = false;
                for (int e = 0; e < cosmetics.Count; e++)
                {
                    if (cosmetics[e].region == stockCosmetics[i].region)
                    {
                        isStockDupeEquipRegion = true;
                    }
                }
                if (!isStockDupeEquipRegion)
                {

                    cosmetics.Add(stockCosmetics[i]);
                }
            }
        }
    }

    public void UpdateTeamColor()
    {

        //Player
        float teamFinal = (float)currentTeam + 1;
        Renderer[] meshes = this.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < meshes.Length; i++)
        {
            Material[] mats = meshes[i].materials;
            for (int r = 0; r < mats.Length; r++)
            {
                mats[r].SetFloat("_Team_1", teamFinal);
            }
            meshes[i].materials = mats;
        }

        //Gun
        if (currentGun != null)
        {
            meshes = currentGun.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < meshes.Length; i++)
            {
                Material[] mats = meshes[i].materials;
                for (int r = 0; r < mats.Length; r++)
                {
                    mats[r].SetFloat("_Team_1", teamFinal);
                }
                meshes[i].materials = mats;
            }
        }
    }

    public PlayerTracker GetTracker()
    {
        return tracker;
    }

    public FirstPersonController GetController()
    {
        return controller;
    }

    public TeamList GetTeam()
    {
        return currentTeam;
    }

    public Gun GetCurrentGun()
    {
        return currentGun;
    }

    public ClassList GetCurrentClass()
    {
        return currentClass;
    }
    public ClassStats GetClassStats()
    {
        return currentStats;
    }

    public bool IsPlayerOwner()
    {
        return IsOwner;
    }
    public ulong GetPlayerID()
    {
        return OwnerClientId;
    }

    public int GetTeamLayer()
    {
        switch (currentTeam)
        {
            case TeamList.orange:
                return LayerMask.NameToLayer("OrangeTeam");
            case TeamList.yellow:
                return LayerMask.NameToLayer("YellowTeam");
            case TeamList.green:
                return LayerMask.NameToLayer("GreenTeam");
            case TeamList.lightBlue:
                return LayerMask.NameToLayer("LightBlueTeam");
            case TeamList.blue:
                return LayerMask.NameToLayer("BlueTeam");
            case TeamList.purple:
                return LayerMask.NameToLayer("PurpleTeam");
            case TeamList.beige:
                return LayerMask.NameToLayer("BeigeTeam");
            case TeamList.brown:
                return LayerMask.NameToLayer("BrownTeam");
            case TeamList.gray:
                return LayerMask.NameToLayer("GrayTeam");
            default:
                return LayerMask.NameToLayer("GrayTeam");
        }
    }

    public void SetCharacterVisibility(bool visible)
    {
        currentPlayerVisibility = visible;
        for (int i = 0; i < playerModels.Length; i++)
        {
            if (i == (int)currentClass)
            {
                if (visible)
                {

                    //Reveal class
                    playerModels[i].SetActive(true);

                    //Get Combination Hide Bodygroups Enum
                    BodyGroups combined = new BodyGroups();
                    for (int e = 0; e < cosmetics.Count; e++)
                    {
                        combined = combined | cosmetics[e].hideBodyGroups;
                    }

                    //Apply Bodygroup Hiding
                    Transform t = playerModels[i].transform;
                    SetMeshVis(t, "Skin Arm L", true);
                    SetMeshVis(t, "Skin Arm R", true);
                    SetMeshVis(t, "Skin Body", true);
                    SetMeshVis(t, "Skin Foot L", true);
                    SetMeshVis(t, "Skin Foot R", true);
                    SetMeshVis(t, "Skin Hand L", true);
                    SetMeshVis(t, "Skin Hand R", true);
                    SetMeshVis(t, "Skin Head", true);
                    SetMeshVis(t, "Skin Leg L", true);
                    SetMeshVis(t, "Skin Leg R", true);
                    if (combined.HasFlag(BodyGroups.armL))
                    {
                        SetMeshVis(t, "Skin Arm L", false);
                    }
                    if (combined.HasFlag(BodyGroups.armR))
                    {
                        SetMeshVis(t, "Skin Arm R", false);
                    }
                    if (combined.HasFlag(BodyGroups.body))
                    {
                        SetMeshVis(t, "Skin Body", false);
                    }
                    if (combined.HasFlag(BodyGroups.footL))
                    {
                        SetMeshVis(t, "Skin Foot L", false);
                    }
                    if (combined.HasFlag(BodyGroups.footR))
                    {
                        SetMeshVis(t, "Skin Foot R", false);
                    }
                    if (combined.HasFlag(BodyGroups.handL))
                    {
                        SetMeshVis(t, "Skin Hand L", false);
                    }
                    if (combined.HasFlag(BodyGroups.handR))
                    {
                        SetMeshVis(t, "Skin Hand R", false);
                    }
                    if (combined.HasFlag(BodyGroups.head))
                    {
                        SetMeshVis(t, "Skin Head", false);
                    }
                    if (combined.HasFlag(BodyGroups.legL))
                    {
                        SetMeshVis(t, "Skin Leg L", false);
                    }
                    if (combined.HasFlag(BodyGroups.legR))
                    {
                        SetMeshVis(t, "Skin Leg R", false);
                    }

                    //Animation
                    Transform handR = null;
                    Transform head = null;
                    foreach (Transform g in transform.GetComponentsInChildren<Transform>())
                    {
                        if (g.name == "Hand R")
                        {
                            handR = g;
                        }
                        if (g.name == "Head")
                        {
                            head = g;
                        }
                    }
                    GetTracker().SetCharacter(playerModels[i].GetComponentInChildren<Animator>(), playerModels[i].transform, handR, head);
                }
                else
                {
                    playerModels[i].SetActive(true);
                    Transform t = playerModels[i].transform;
                    SetMeshVis(t, "Skin Arm L", false);
                    SetMeshVis(t, "Skin Arm R", false);
                    SetMeshVis(t, "Skin Body", false);
                    SetMeshVis(t, "Skin Foot L", false);
                    SetMeshVis(t, "Skin Foot R", false);
                    SetMeshVis(t, "Skin Hand L", true);
                    SetMeshVis(t, "Skin Hand R", true);
                    SetMeshVis(t, "Skin Head", false);
                    SetMeshVis(t, "Skin Leg L", false);
                    SetMeshVis(t, "Skin Leg R", false);
                }
            }
            else
            {
                //Hide other classes
                playerModels[i].SetActive(true);
            }
        }

        if (currentGun != null)
        {
            if (visible)
            {
                currentGun.SetGunTransformParent(playerModels[(int)currentClass].GetNamedChild("Gun R").transform, true);
            }
            else
            {
                currentGun.SetGunTransformParent(tracker.GetRightHand(), false);
            }
        }
    }

    void SetMeshVis(Transform trans, string meshName, bool set)
    {
        trans.Find(meshName).gameObject.SetActive(set);
    }

    public void TakeDamage(ulong id, int amount)
    {
        if (IsHost)
        {
            int currentHealth = currentStats.baseHealth - amount;
            gm.PlayerTookDamageClientRpc(GetPlayerID(), currentHealth, id);
        }
    }

    public void SetHealth(int health)
    {
        currentStats.baseHealth = health;
        if (health <= 0)
        {
            if (IsHost)
            {
                gm.RespawnPlayerClientRpc(GetPlayerID(), GetTeam());
            }
        }
    }

    public void ResetClassStats()
    {
        currentStats = al.GetClassStats(currentClass);
    }

    public void RemoveHeldWire(Vector3 finalPos)
    {
        if (heldWire != null)
        {
            wireSounds.RemoveWire();
        }
        heldWire.point = finalPos;
        heldWire = null;
    }

    public void SetWirePoint(Wire.WirePoint wire)
    {
        heldWire = wire;
        if (heldWire != null)
        {
            wireSounds.AddWire();
        }
    }

    public Wire.WirePoint GetWirePoint()
    {
        return heldWire;
    }
}
