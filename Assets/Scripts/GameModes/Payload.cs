using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.AI;

public class Payload : GenericGamemode
{
    GlobalManager gm;
    [SerializeField] List<PayLoadTeam> payloadTeams;

    private void Start()
    {
        gm = this.GetComponent<GlobalManager>();
        for (int i = 0; i < payloadTeams.Count; i++)
        {
            if (payloadTeams[i].goal != null)
            {
                payloadTeams[i].goal.SetDefendingTeam(i+1);
            }
        }
    }

    public override void SetTeams()
    {
        TeamChanger(null);
    }

    public override void SetTeams(List<TeamList> setTeams)
    {
        TeamChanger(setTeams);
    }

    public override Vector3 GetCurrentMatchFocalPoint(int team)
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < payloadTeams.Count; i++)
        {
            if (payloadTeams[i].goal != null && i != team)
            {
                points.Add(payloadTeams[i].goal.transform.position);
            }
        }

        if (points.Count > 0)
        {
            try
            {
                return gm.GetWire(gm.GetTeamColors(true)[team]).FindClosestWireToGoal(points).point;
            }
            catch (System.NullReferenceException)
            {
                return Vector3.zero;
            }
        }
        else
        {
            points = new List<Vector3>();
            for (int i = 0; i < payloadTeams.Count; i++)
            {
                if (payloadTeams[i].goal == null)
                {
                    points.Add(gm.GetMatchFocalPoint(gm.GetTeamColors(true)[i]));
                }
            }

            float shortest = float.PositiveInfinity;
            Vector3 final = Vector3.zero;
            NavMeshPath path = new NavMeshPath();
            for (int i = 0; i < points.Count; i++)
            {
                float lng = 0;
                NavMesh.CalculatePath(points[i], payloadTeams[team].goal.transform.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathInvalid)
                {
                    for (int e = 1; e < path.corners.Length; ++e)
                    {
                        lng += Vector3.Distance(path.corners[e - 1], path.corners[e]);
                    }
                }
                else
                {
                    lng = float.PositiveInfinity;
                }
                if (lng < shortest)
                {
                    shortest = lng;
                    final = points[i];
                }
            }
            return final;
        }
    }

    void TeamChanger(List<TeamList> setTeams)
    {
        if (gm == null)
        {
            gm = this.GetComponent<GlobalManager>();
        }

        //Gather player original teams
        List<Player> clients = gm.GetClients();
        List<TeamList> teamColors = gm.GetTeamColors(false);
        int[] clientTeams = new int[clients.Count];
        for (int i = 0; i < clients.Count; i++)
        {
            TeamList color = clients[i].GetTeam();
            for (int e = 0; e < teamColors.Count; e++)
            {
                if (color == teamColors[e])
                {
                    clientTeams[i] = e;
                }
            }
        }

        //Select new teams
        gm.ClearTeams();

        //Gray Team Always Team 0
        TeamInfo grayTeam = new TeamInfo() { spawns = 0, teamColor = TeamList.gray };
        gm.AddNewTeam(grayTeam);

        for (int i = 0; i < payloadTeams.Count; i++)
        {
            TeamInfo nextTeam = new TeamInfo();
            nextTeam.spawns = i + 1;
            if (setTeams == null)
            {
                nextTeam.teamColor = SelectTeams(gm.GetTeamColors(false), PlayerPrefs.GetInt("Team" + (i + 1) + "Setting"));
            }
            else
            {
                nextTeam.teamColor = setTeams[i];
            }
            gm.AddNewTeam(nextTeam);
        }

        //Re-apply Player Teams
        for (int i = 0; i < clients.Count; i++)
        {
            gm.SetPlayerTeamClientRpc(clients[i].GetPlayerID(), gm.GetTeams()[clientTeams[i]].teamColor);
        }

        gm.ModifyTeamsAcrossServer();
    }

    public override TeamList DecideWhichPlayerTeam()
    {
        //Decides based on which team has the least amount of players
        List<Player> clients = gm.GetClients();
        List<TeamList> teams = gm.GetTeamColors(true);
        int[] teamCounts = new int[teams.Count];
        for (int i = 0; i < clients.Count; i++)
        {
            TeamList t = clients[i].GetTeam();
            for (int e = 0; e < teams.Count; e++)
            {
                if (teams[e] == t)
                {
                    teamCounts[e]++;
                    break;
                }
            }
        }
        int finalIndex = 0;
        for (int i = 0; i < teamCounts.Length; i++)
        {
            if (teamCounts[i] < teamCounts[finalIndex])
            {
                finalIndex = i;
            }
        }
        return teams[finalIndex];
    }

    public override float RequestPlayerRespawnTimer(int index)
    {
        List<TeamInfo> tempTeams = gm.GetTeams();
        TeamList playerTeam = gm.GetClients()[index].GetTeam();
        for (int i = 1; i < tempTeams.Count; i++)
        {
            if (tempTeams[i].teamColor == playerTeam)
            {
                int respawnTime = payloadTeams[i - 1].respawnWaveTime;
                return respawnTime - (Time.time % respawnTime) + respawnTime;
            }
        }
        return 10;
    }

    [System.Serializable]
    public struct PayLoadTeam
    {
        public string teamName;
        public WireCheck goal;
        public int respawnWaveTime;
    }
}
