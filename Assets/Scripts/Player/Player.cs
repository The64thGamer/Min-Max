using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;
using static UnityEngine.UI.Image;

public class Player : NetworkBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] TeamList currentTeam;
    [SerializeField] ClassList currentClass;
    [SerializeField] ClassStats currentStats;
    [SerializeField] GameObject[] playerModels;
    int[] cosmeticInts = new int[0];
    List<GameObject> currentCharMeshes = new List<GameObject>();
    WireSounds wireSounds;
    PlayerTracker tracker;
    FirstPersonController controller;
    GlobalManager gm;
    AllStats al;
    bool currentPlayerVisibility;
    Wire.WirePoint heldWire;
    int currentHealth;

    public override void OnNetworkSpawn()
    {
        //Before
        tracker = GetComponentInChildren<PlayerTracker>();
        controller = GetComponentInChildren<FirstPersonController>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        al = gm.GetComponent<AllStats>();
        gm.AddPlayerToClientList(this);
        wireSounds = transform.Find("WireSounds").GetComponent<WireSounds>();

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
        cosmeticInts = classCosmetics;
        currentClass = setClass;
        currentStats = gm.GetComponent<AllStats>().GetClassStats(setClass);
        currentHealth = currentStats.baseHealth;
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
        if(currentGun != null)
        {
            Destroy(currentGun.gameObject);
        }
        GameObject gunObject = GameObject.Instantiate(gun.gunPrefab, Vector3.zero, Quaternion.identity, this.transform);
        gunObject.name = gun.gunName;
        currentGun = gunObject.GetComponent<Gun>();
        currentGun.SetPlayer(this);
        currentGun.SetDefaultStats(gun);
        UpdateTeamColor();
    }

    void SetupCosmetics(int[] classCosmetics)
    {
        List<Cosmetic> stockCosmetics = gm.GetCosmetics().GetClassCosmetics(currentClass);
        List<int> cosmeticIntList = cosmeticInts.ToList<int>();
        for (int i = 0; i < classCosmetics.Length; i++)
        {
            if (classCosmetics[i] < stockCosmetics.Count)
            {
                Cosmetic cm = stockCosmetics[classCosmetics[i]];
                bool isDupeEquipRegion = false;
                for (int e = 0; e < cosmeticIntList.Count; e++)
                {
                    if (stockCosmetics[cosmeticIntList[e]].region == cm.region)
                    {
                        isDupeEquipRegion = true;
                    }
                }
                if (!isDupeEquipRegion)
                {
                    cosmeticIntList.Add(classCosmetics[i]);
                }
            }
        }
        //Stock
        for (int i = 0; i < stockCosmetics.Count; i++)
        {
            if (stockCosmetics[i].stock == StockCosmetic.stock)
            {
                bool isStockDupeEquipRegion = false;
                for (int e = 0; e < cosmeticIntList.Count; e++)
                {
                    if (stockCosmetics[cosmeticIntList[e]].region == stockCosmetics[i].region)
                    {
                        isStockDupeEquipRegion = true;
                    }
                }
                if (!isStockDupeEquipRegion)
                {

                    cosmeticIntList.Add(i);
                }
            }
        }
        cosmeticInts = cosmeticIntList.ToArray();
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

    public int[] GetCosmeticInts()
    {
        return cosmeticInts;
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
        while (currentCharMeshes.Count > 0)
        {
            Destroy(currentCharMeshes[0]);
            currentCharMeshes.RemoveAt(0);
        }
        currentPlayerVisibility = visible;
        for (int i = 0; i < playerModels.Length; i++)
        {
            if (i == (int)currentClass)
            {
                if (visible)
                {
                    List<Cosmetic> classCosmetics = gm.GetCosmetics().GetClassCosmetics(currentClass);

                    //Reveal class
                    playerModels[i].SetActive(true);

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

                    //Get Combination Hide Bodygroups Enum
                    BodyGroups combined = new BodyGroups();
                    for (int e = 0; e < cosmeticInts.Length; e++)
                    {
                        combined = combined | classCosmetics[cosmeticInts[e]].hideBodyGroups;
                    }

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

                    //Apply Cosmetics
                    for (int e = 0; e < cosmeticInts.Length; e++)
                    {
                        ApplyCosmetics(classCosmetics[cosmeticInts[e]].prefab, t);
                    }
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
                    List<Cosmetic> classCosmetics = gm.GetCosmetics().GetClassCosmetics(currentClass);
                    //Apply Only Hand Cosmetics
                    for (int e = 0; e < cosmeticInts.Length; e++)
                    {
                        if (classCosmetics[cosmeticInts[e]].region == EquipRegion.hands)
                        {
                            ApplyCosmetics(classCosmetics[cosmeticInts[e]].prefab, t);
                        }
                    }
                }
                //Animation
                Transform handR = null;
                Transform handL = null;
                Transform head = null;
                foreach (Transform g in playerModels[i].GetComponentsInChildren<Transform>())
                {
                    if (g.name == "Hand R")
                    {
                        handR = g;
                    }
                    if (g.name == "Hand L")
                    {
                        handL = g;
                    }
                    if (g.name == "Head")
                    {
                        head = g;
                    }
                }
                GetTracker().SetCharacter(playerModels[i].GetComponentInChildren<Animator>(), playerModels[i].transform, handR, handL, head, !visible);
            }
            else
            {
                //Hide other classes
                playerModels[i].SetActive(false);
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

    void ApplyCosmetics(GameObject prefab, Transform t)
    {
        GameObject g = new GameObject(prefab.name);
        g.transform.parent = t;
        SkinnedMeshRenderer targetSkin = g.AddComponent<SkinnedMeshRenderer>();
        SkinnedMeshRenderer originalSkin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        targetSkin.SetSharedMaterials(originalSkin.sharedMaterials.ToList<Material>());
        targetSkin.sharedMesh = originalSkin.sharedMesh;
        targetSkin.rootBone = t.Find("Armature").GetChild(0);
        currentCharMeshes.Add(g);

        Transform[] newBones = new Transform[originalSkin.bones.Length];

        int a = 0;
        foreach (var originalBone in originalSkin.bones)
        {

            foreach (var newBone in targetSkin.rootBone.GetComponentsInChildren<Transform>())
            {
                if (newBone.name == originalBone.name)
                {
                    newBones[a] = newBone;
                    continue;
                }
            }
            a++;
        }
        targetSkin.bones = newBones;
    }

    void SetMeshVis(Transform trans, string meshName, bool set)
    {
        trans.Find(meshName).gameObject.SetActive(set);
    }

    public void ChangeHealth(ulong id, int amount, int idHash)
    {
        if (IsHost)
        {
            int finalHealth = Mathf.Min(currentHealth + amount, currentStats.baseHealth);
            gm.PlayerTookDamageClientRpc(GetPlayerID(), finalHealth, id, idHash);
        }
    }

    public void SetHealth(int health)
    {
        currentHealth = health;
        if (health <= 0)
        {
            if (IsHost)
            {
                gm.RespawnPlayerClientRpc(GetPlayerID(), GetTeam());
            }
        }
    }

    public int GetHealth()
    {
        return currentHealth;
    }

    public void ResetClassStats()
    {
        currentStats = al.GetClassStats(currentClass);
        currentHealth = currentStats.baseHealth;
    }

    public void RemoveHeldWire(Vector3 finalPos)
    {
        if (heldWire != null)
        {
            wireSounds.RemoveWire();
            heldWire.point = finalPos;
            heldWire = null;
        }
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
