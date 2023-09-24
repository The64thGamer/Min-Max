using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
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
    [SerializeField] List<Gun> guns;
    [SerializeField] GameObject[] playerModels;
    [SerializeField] TextMesh nameMesh;

    PlayerUIController uiController;
    Menu menu;
    List<GameObject> currentCharMeshes = new List<GameObject>();
    WireSounds wireSounds;
    PlayerTracker tracker;
    FirstPersonController controller;
    GlobalManager gm;
    AllStats al;
    bool currentPlayerVisibility;
    Wire.WirePoint heldWire;

    //Stats
    float timeTillRespawn;
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
        uiController = this.GetComponent<PlayerUIController>();
        tracker = GetComponentInChildren<PlayerTracker>();
        controller = GetComponentInChildren<FirstPersonController>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        al = gm.GetComponent<AllStats>();
        wireSounds = transform.Find("WireSounds").GetComponent<WireSounds>();
        menu = transform.Find("Menu").GetComponent<Menu>();
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
            Destroy(this.GetComponent<PlayerUIController>());
            uiController = null;
            Destroy(transform.Find("PlayerUIDoc").gameObject);
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
        timeTillRespawn = respawnTimer;
        while (timeTillRespawn > 0)
        {
            timeTillRespawn -= Time.deltaTime;
            yield return null;
        }
        timeTillRespawn = 0;
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
        Debug.Log("Player " + GetPlayerID() + " respawned in " + gm.FindPlayerTeam(GetPlayerID()) + " spawn room");
        if(IsHost)
        {
            gm.ResetClassStats(false,0,GetPlayerID(), al.GetClassStats(gm.FindPlayerClass(GetPlayerID())));
        }
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
        if (respawnState == RespawnState.waitingForResponse)
        {
            respawnState = RespawnState.notRespawning;
        }
    }

    public void UpdateName()
    {
        string name = gm.FindPlayerName(GetPlayerID());
        this.name = name;
        nameMesh.text = name;
        if (IsOwner && GetPlayerID() < botID)
        {
            nameMesh.gameObject.SetActive(false);
        }
    }

    public void UpdateClass()
    {
        float height = PlayerPrefs.GetFloat("Settings: PlayerHeight") - 0.127f; //Height offset by 5 inches (Height from eyes to top of head)
        tracker.GetForwardRoot().localScale = Vector3.one * (gm.FindPlayerStat(GetPlayerID(), ChangablePlayerStats.eyeHeight) / height);
        SetCharacterVisibility(currentPlayerVisibility);
        UpdateTeamColor();
    }

    public void UpdateTeam()
    {
        SetLayer(GetTeamLayer());
        UpdateTeamColor();

        if (uiController != null)
        {
            uiController.UpdateTeamColorUI();
        }
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

    public void UpdateGuns()
    {
        if (currentGun != null)
        {
            Destroy(currentGun.gameObject);
        }
        GunProjectiles gun = al.SearchGuns(gm.FindPlayerGun(GetPlayerID(), (int)gm.FindPlayerStat(GetPlayerID(), ChangablePlayerStats.currentlyHeldWeapon)));
        GameObject gunObject = GameObject.Instantiate(gun.gunPrefab, Vector3.zero, Quaternion.identity, this.transform);
        gunObject.name = gun.gunName;
        currentGun = gunObject.GetComponent<Gun>();
        currentGun.SetDefaultStats(gun);
        SetCharacterVisibility(currentPlayerVisibility);
        UpdateTeamColor();
    }

    void UpdateTeamColor()
    {

        //Player
        float teamFinal = (float)gm.FindPlayerTeam(GetPlayerID()) + 1;
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

    public bool IsPlayerOwner()
    {
        return IsOwner;
    }
    public ulong GetPlayerID()
    {
        return OwnerClientId;
    }

    public float GetTimeTillRespawn()
    {
        return timeTillRespawn;
    }

    public Menu GetMenu()
    {
        return menu;
    }

    public Gun GetCurrentGun()
    {
        return currentGun;
    }

    public int GetTeamLayer()
    {
        switch (gm.FindPlayerTeam(GetPlayerID()))
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
        int[] cosmeticInts = gm.FindPlayerCosmetics(GetPlayerID());
        ClassList currentClass = gm.FindPlayerClass(GetPlayerID());
        while (currentCharMeshes.Count > 0)
        {
            Destroy(currentCharMeshes[0]);
            currentCharMeshes.RemoveAt(0);
        }
        currentPlayerVisibility = visible;

        //Hide other classes
        for (int i = 0; i < playerModels.Length; i++)
        {
            if (i != (int)currentClass)
            {                
                playerModels[i].SetActive(false);
            }
        }

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
        }
    }

    void ApplyCosmetics(GameObject prefab, Transform t)
    {
        GameObject g = GameObject.Instantiate(prefab, t, false);
        g.transform.localPosition = Vector3.zero;

        SkinnedMeshRenderer[] targetSkin = g.GetComponentsInChildren<SkinnedMeshRenderer>();
        Transform rootBone = t.Find("Armature").GetChild(0);


        Dictionary<string, Transform> boneDictionary = new Dictionary<string, Transform>();
        Transform[] rootBoneChildren = rootBone.GetComponentsInChildren<Transform>();
        foreach (Transform child in rootBoneChildren)
        {
            boneDictionary[child.name] = child;
        }

        for (int j = 0; j < targetSkin.Length; j++)
        {
            targetSkin[j].rootBone = rootBone;
            Transform[] newBones = new Transform[targetSkin[j].bones.Length];
            for (int i = 0; i < targetSkin[j].bones.Length; i++)
            {
                if (boneDictionary.TryGetValue(targetSkin[j].bones[i].name, out Transform newBone))
                {
                    newBones[i] = newBone;
                }
            }
            targetSkin[j].bones = newBones;
        }
        currentCharMeshes.Add(g);

    }

    void SetMeshVis(Transform trans, string meshName, bool set, bool alwaysUpdate)
    {
        GameObject g = trans.Find(meshName).gameObject;
        SkinnedMeshRenderer[] r = g.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < r.Length; i++)
        {
            r[i].updateWhenOffscreen = alwaysUpdate;

        }
        g.SetActive(set);
    }

    public int ChangeHealth(ulong id, int amount, int idHash)
    {
        float currentHealth = gm.FindPlayerStat(GetPlayerID(), ChangablePlayerStats.currentHealth);
        if (IsHost)
        {
            if (currentHealth <= 0)
            {
                return 0;
            }
            int finalHealth = (int)Mathf.Min(currentHealth + amount, gm.FindPlayerStat(GetPlayerID(), ChangablePlayerStats.maxHealth));
            gm.PlayerTookDamageClientRpc(GetPlayerID(), finalHealth, id, idHash);
            if (finalHealth <= 0)
            {
                gm.RespawnPlayer(GetPlayerID(), gm.FindPlayerTeam(GetPlayerID()), false);
            }
            return finalHealth;
        }
        return 0;
    }

    public PlayerUIController GetUIController()
    {
        return uiController;
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
