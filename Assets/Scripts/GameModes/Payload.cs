using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Payload : GenericGamemode
{
    GlobalManager gm;
    [UnityEngine.Range(2, 8)]
    [SerializeField] uint noOfTeams = 2;
    [SerializeField]

    private void Start()
    {
        gm = this.GetComponent<GlobalManager>();
    }

    public override void SetTeams()
    {
        TeamChanger(null);
    }

    public override void SetTeams(List<TeamList> setTeams)
    {
        TeamChanger(setTeams);
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

        for (int i = 0; i < noOfTeams; i++)
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
        return teams[teamCounts[finalIndex]];
    }
}
