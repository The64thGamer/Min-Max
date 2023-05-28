using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements;

public class Player : NetworkBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] Team currentTeam;
    [SerializeField] ClassList currentClass;
    [SerializeField] ClassStats currentStats;
    [SerializeField] PlayerTracker tracker;
    [SerializeField] FirstPersonController controller;
    [SerializeField] ulong playerID;
    [SerializeField] Transform vrSetup;
    [SerializeField] Transform clientSetup;
    [SerializeField] GameObject[] playerModels;
    GlobalManager gm;

    public override void OnNetworkSpawn()
    {
        //Before
        tracker = GetComponentInChildren<PlayerTracker>();
        controller = GetComponentInChildren<FirstPersonController>();
        currentGun = GetComponentInChildren<GenericGun>();
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        gm.AssignNewPlayerClient(this);

        //Debug Default
        SetClass(ClassList.programmer);
        SetGun(gm.GetComponent<AllStats>().SearchGuns("Worker Ionizing Pistol"));

        //After
        if (IsOwner)
        {
            SetCharacterVisibility(false);
        }
        else
        {
            SetCharacterVisibility(true);
        }
    }

    void OnDestroy()
    {
        gm.DisconnectClient(this);
    }

    public void SetClass(ClassList setClass)
    {
        currentClass = setClass;
        currentStats = gm.GetComponent<AllStats>().GetClassStats(ClassList.programmer);
    }

    public void SetTeam(Team team)
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
        GameObject gunObject = GameObject.Instantiate(gun.gunPrefab, Vector3.zero, Quaternion.identity,this.transform);
        gunObject.GetComponent<NetworkObject>().SpawnWithOwnership(playerID);
        gunObject.GetComponent<Gun>().SetPlayer(this);
        currentGun = gunObject.GetComponent<Gun>();
    }

    public void UpdateTeamColor()
    {
        TeamList currentList = TeamList.gray;
        switch (currentTeam)
        {
            case Team.team1:
                currentList = gm.GetTeam1();
                break;
            case Team.team2:
                currentList = gm.GetTeam2();
                break;
            default:
                break;
        }

        float teamFinal = (float)currentList + 1;
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
    }

    public PlayerTracker GetTracker()
    {
        return tracker;
    }

    public FirstPersonController GetController()
    {
        return controller;
    }

    public Team GetTeam()
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

    public ulong GetPlayerID()
    {
        return playerID;
    }

    public void SetPlayerID(ulong id)
    {
        playerID = id;
    }

    public int GetTeamLayer()
    {
        switch (currentTeam)
        {
            case Team.team1:
                return LayerMask.NameToLayer("Team1");
            case Team.team2:
                return LayerMask.NameToLayer("Team2");
            default:
                return LayerMask.NameToLayer("Neutral");
        }
    }

    public void SetCharacterVisibility(bool visible)
    {
        for (int i = 0; i < playerModels.Length; i++)
        {
            if (visible)
            {
                if (i == (int)currentClass)
                {
                    playerModels[i].SetActive(true);
                }
                else
                {
                    playerModels[i].SetActive(false);
                }
            }
            else
            {
                playerModels[i].SetActive(false);
            }
        }

        if (currentGun != null)
        {
            if (visible)
            {
                currentGun.SetGunTransformParent(playerModels[(int)currentClass].GetNamedChild("Gun R").transform);
            }
            else
            {
                currentGun.SetGunTransformParent(tracker.GetRightHand());
            }
        }
    }
}
