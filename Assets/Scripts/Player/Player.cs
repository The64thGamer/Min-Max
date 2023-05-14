using Autohand;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class Player : NetworkBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] Team currentTeam;
    [SerializeField] ClassList currentClass;
    [SerializeField] ClassStats currentStats;
    [SerializeField] PlayerTracker tracker;
    [SerializeField] AutoHandPlayer autoHand;
    [SerializeField] ulong playerID;

    [SerializeField] Transform vrSetup;
    [SerializeField] Transform clientSetup;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Destroy(clientSetup.gameObject);
            vrSetup.gameObject.SetActive(true);
        }
        else
        {
            Destroy(vrSetup.gameObject);
            clientSetup.gameObject.SetActive(true);
        }
        tracker = GetComponentInChildren<PlayerTracker>();
        autoHand = GetComponentInChildren<AutoHandPlayer>();
        currentGun = GetComponentInChildren<GenericGun>();
        GameObject.Find("Global Manager").GetComponent<GlobalManager>().AssignNewPlayerClient(this);

        //Debug Default
        SetClass(ClassList.programmer);
    }

    void OnDestroy()
    {
        GameObject.Find("Global Manager").GetComponent<GlobalManager>().DisconnectClient(this);
    }

    public void SetClass(ClassList setClass)
    {
        currentClass = setClass;
        currentStats = GameObject.Find("Global Manager").GetComponent<AllStats>().GetClassStats(ClassList.programmer);
    }

    public void SetTeam(Team team)
    {
        currentTeam = team;
        TeamList currentList = TeamList.gray;
        switch (currentTeam)
        {
            case Team.team1:
                currentList = GameObject.Find("Global Manager").GetComponent<GlobalManager>().GetTeam1();
                break;
            case Team.team2:
                currentList = GameObject.Find("Global Manager").GetComponent<GlobalManager>().GetTeam2();
                break;
            default:
                break;
        }
        Transform modelRoot = tracker.GetModelRoot();
        float teamFinal = (float)currentList + 1;
        if (modelRoot != null)
        {
            Renderer[] meshes = modelRoot.GetComponentsInChildren<Renderer>();
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

    public AutoHandPlayer GetAutoHand()
    {
        return autoHand;
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
}
