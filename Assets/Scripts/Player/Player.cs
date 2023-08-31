using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(NetworkObject))]
public class Player : NetworkBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] TeamList currentTeam;
    [SerializeField] ClassList currentClass;
    [SerializeField] ClassStats currentStats;
    [SerializeField] GameObject[] playerModels;
    string playerName;
    [SerializeField] TextMesh nameMesh;

    UIDocument playerUIVTA;
    Menu menu;
    int[] cosmeticInts = new int[0];
    List<GameObject> currentCharMeshes = new List<GameObject>();
    WireSounds wireSounds;
    PlayerTracker tracker;
    FirstPersonController controller;
    GlobalManager gm;
    AllStats al;
    bool currentPlayerVisibility;
    Wire.WirePoint heldWire;

    //Stats
    int currentHealth;
    RespawnState respawnState = RespawnState.notRespawning;
    public enum RespawnState
    {
        notRespawning,
        respawning,
        waitingForResponse,
    }

    //Const
    const ulong botID = 64646464646464;
    private void Awake()
    {
        tracker = GetComponentInChildren<PlayerTracker>();
        controller = GetComponentInChildren<FirstPersonController>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        al = gm.GetComponent<AllStats>();
        wireSounds = transform.Find("WireSounds").GetComponent<WireSounds>();
        menu = transform.Find("Menu").GetComponent<Menu>();
        playerUIVTA = transform.Find("PlayerUIDoc").GetComponent<UIDocument>();
    }
    public override void OnNetworkSpawn()
    {
        //Before
        gm.AddPlayerToClientList(this);

        //After
        if (IsOwner && GetPlayerID() < botID)
        {
            SetCharacterVisibility(false);
        }
        else
        {
            SetCharacterVisibility(true);
            Destroy(this.GetComponentInChildren<UniversalAdditionalCameraData>());
            Destroy(this.GetComponentInChildren<Camera>());
            Destroy(this.GetComponentInChildren<AudioListener>());
            Destroy(menu.gameObject);
            Destroy(playerUIVTA.gameObject);
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

    private new void OnDestroy()
    {
        if (IsOwner && !IsHost)
        {
            gm.DisconnectToTitleScreen(false);
        }
    }

    public void RespawnPlayer(Vector3 spawnPos, float respawnTimer)
    {
        if (respawnState == RespawnState.notRespawning)
        {
            StartCoroutine(RespawnTimed(spawnPos, respawnTimer));
        }
    }

    IEnumerator RespawnTimed(Vector3 spawnPos, float respawnTimer)
    {
        respawnState = RespawnState.respawning;
        SetLayer(19); //Dead Player
        Debug.Log("Player " + GetPlayerID() + " Died. Respawning in " + respawnTimer + " sec");
        yield return new WaitForSeconds(respawnTimer);
        if (IsHost && GetPlayerID() < botID)
        {
            //Check if the player wants to update their class
            //Or Cosmetics before respawning
            respawnState = RespawnState.waitingForResponse;
            gm.RequestPlayerStatusOnSwitchedClassesClientRpc(GetPlayerID());

            while (respawnState != RespawnState.notRespawning)
            {
                yield return null;
            }
        }
        else
        {
            respawnState = RespawnState.notRespawning;
        }
        Debug.Log("Player " + GetPlayerID() + " respawned in " + currentTeam.ToString() + " spawn room");
        ResetClassStats();
        GetTracker().ForceNewPosition(spawnPos);
        SetLayer(GetTeamLayer());
    }

    public bool SendPlayerSwitchClassStatus()
    {
        if (respawnState == RespawnState.waitingForResponse)
        {
            respawnState = RespawnState.notRespawning;
            return true;
        }
        return false;
    }

    public void SendPlayerSwitchClassStatusRejection()
    {
        if(respawnState == RespawnState.waitingForResponse)
        {
            respawnState = RespawnState.notRespawning;
        }
    }

    public void SetName(string name)
    {
        playerName = name;
        this.name = name;
        nameMesh.text = name;
        if(IsOwner && GetPlayerID() < botID)
        {
            nameMesh.gameObject.SetActive(false);
        }
    }

    public void SetClass(ClassList setClass, int[] classCosmetics)
    {
        cosmeticInts = classCosmetics;
        currentClass = setClass;
        currentStats = gm.GetComponent<AllStats>().GetClassStats(setClass);
        SetHealth(currentStats.baseHealth);
        SetupCosmetics(classCosmetics);
        SetCharacterVisibility(currentPlayerVisibility);
        UpdateTeamColor();
        float height = PlayerPrefs.GetFloat("Settings: PlayerHeight") - 0.127f; //Height offset by 5 inches (Height from eyes to top of head)
        tracker.GetForwardRoot().localScale = Vector3.one * (currentStats.classEyeHeight / height);
    }

    public void SetTeam(TeamList team)
    {
        currentTeam = team;
        SetLayer(GetTeamLayer());
        UpdateTeamColor();
    }

    void SetLayer(int layer)
    {
        Transform[] allBits = this.GetComponentsInChildren<Transform>();
        for (int i = 0; i < allBits.Length; i++)
        {
            allBits[i].gameObject.layer = layer;
        }
        this.gameObject.layer = layer;
    }

    public void SetGun(GunProjectiles gun)
    {
        if (currentGun != null)
        {
            Destroy(currentGun.gameObject);
        }
        GameObject gunObject = GameObject.Instantiate(gun.gunPrefab, Vector3.zero, Quaternion.identity, this.transform);
        gunObject.name = gun.gunName;
        currentGun = gunObject.GetComponent<Gun>();
        currentGun.SetPlayer(this);
        currentGun.SetDefaultStats(gun);
        SetCharacterVisibility(currentPlayerVisibility);
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

    public string GetPlayerName()
    {
        return playerName;
    }

    public Menu GetMenu()
    {
        return menu;
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
                    SetMeshVis(t, "Skin Arm L", true, false);
                    SetMeshVis(t, "Skin Arm R", true, false);
                    SetMeshVis(t, "Skin Body", true, false);
                    SetMeshVis(t, "Skin Foot L", true, false);
                    SetMeshVis(t, "Skin Foot R", true, false);
                    SetMeshVis(t, "Skin Hand L", true, false);
                    SetMeshVis(t, "Skin Hand R", true, false);
                    SetMeshVis(t, "Skin Head", true, false);
                    SetMeshVis(t, "Skin Leg L", true, false);
                    SetMeshVis(t, "Skin Leg R", true, false);

                    //Get Combination Hide Bodygroups Enum
                    BodyGroups combined = new BodyGroups();
                    for (int e = 0; e < cosmeticInts.Length; e++)
                    {
                        combined = combined | classCosmetics[cosmeticInts[e]].hideBodyGroups;
                    }

                    if (combined.HasFlag(BodyGroups.armL))
                    {
                        SetMeshVis(t, "Skin Arm L", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.armR))
                    {
                        SetMeshVis(t, "Skin Arm R", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.body))
                    {
                        SetMeshVis(t, "Skin Body", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.footL))
                    {
                        SetMeshVis(t, "Skin Foot L", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.footR))
                    {
                        SetMeshVis(t, "Skin Foot R", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.handL))
                    {
                        SetMeshVis(t, "Skin Hand L", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.handR))
                    {
                        SetMeshVis(t, "Skin Hand R", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.head))
                    {
                        SetMeshVis(t, "Skin Head", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.legL))
                    {
                        SetMeshVis(t, "Skin Leg L", false, false);
                    }
                    if (combined.HasFlag(BodyGroups.legR))
                    {
                        SetMeshVis(t, "Skin Leg R", false, false);
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
                    SetMeshVis(t, "Skin Arm L", false, false);
                    SetMeshVis(t, "Skin Arm R", false, false);
                    SetMeshVis(t, "Skin Body", false, false);
                    SetMeshVis(t, "Skin Foot L", false, false);
                    SetMeshVis(t, "Skin Foot R", false, false);
                    SetMeshVis(t, "Skin Hand L", true, true);
                    SetMeshVis(t, "Skin Hand R", true, true);
                    SetMeshVis(t, "Skin Head", false, false);
                    SetMeshVis(t, "Skin Leg L", false, false);
                    SetMeshVis(t, "Skin Leg R", false, false);
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

    void SetMeshVis(Transform trans, string meshName, bool set, bool alwaysUpdate)
    {
        GameObject g = trans.Find(meshName).gameObject;
        SkinnedMeshRenderer r = g.GetComponent<SkinnedMeshRenderer>();
        if (r != null)
        {
            r.updateWhenOffscreen = alwaysUpdate;
        }
        g.SetActive(set);
    }

    public int ChangeHealth(ulong id, int amount, int idHash)
    {
        if (IsHost)
        {
            if (currentHealth <= 0)
            {
                UpdateHealthUI();
                return 0;
            }
            int finalHealth = Mathf.Min(currentHealth + amount, currentStats.baseHealth);
            gm.PlayerTookDamageClientRpc(GetPlayerID(), finalHealth, id, idHash);
            if (currentHealth <= 0)
            {
                gm.RespawnPlayer(GetPlayerID(), GetTeam(), false);
            }
            UpdateHealthUI();
            return finalHealth;
        }
        return 0;
    }

    public void SetHealth(int health)
    {
        currentHealth = health;
        UpdateHealthUI();
    }

    void UpdateHealthUI()
    {
        if (playerUIVTA != null)
        {
            playerUIVTA.rootVisualElement.Q<Label>("Health").text = currentHealth.ToString();
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

    public void RemoveHeldWire(Vector3 finalPos, bool playSound)
    {
        if (heldWire != null)
        {
            if (playSound)
            {
                wireSounds.RemoveWire();
            }
            else
            {
                wireSounds.PauseWire();
            }
            heldWire.point = finalPos;
            heldWire = null;
        }
    }

    public void SetWirePoint(Wire.WirePoint wire, bool playSound)
    {
        heldWire = wire;
        if (playSound)
        {
            if (heldWire != null)
            {
                wireSounds.AddWire();
            }
        }
        else
        {
            wireSounds.ResumeWire();
        }
    }

    public Wire.WirePoint GetWirePoint()
    {
        return heldWire;
    }
}
