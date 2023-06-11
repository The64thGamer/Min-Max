using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Payload : GenericGamemode
{
    GlobalManager gm;
    [UnityEngine.Range(2,8)]
    [SerializeField] uint noOfTeams = 2;
    private void Start()
    {
        gm = this.GetComponent<GlobalManager>();
    }

    public override void SetTeams()
    {
        if(gm == null)
        {
            gm = this.GetComponent<GlobalManager>();
        }
        gm.ClearTeams();

        //Gray Team Always Team 0
        TeamInfo grayTeam = new TeamInfo() { spawns = 0, teamColor = TeamList.gray };
        gm.AddNewTeam(grayTeam);

        for (int i = 0; i < noOfTeams; i++)
        {
            TeamInfo nextTeam = new TeamInfo();
            nextTeam.spawns = i + 1;
            nextTeam.teamColor = SelectTeams(gm.GetTeamColors(false), PlayerPrefs.GetInt("Team" + (i + 1) + "Setting"));
            gm.AddNewTeam(nextTeam);
        }
        gm.ModifyTeamsAcrossServer();
    }

    public override void SetTeams(List<TeamList> setTeams)
    {
        if (gm == null)
        {
            gm = this.GetComponent<GlobalManager>();
        }
        gm.ClearTeams();

        //Gray Team Always Team 0
        TeamInfo grayTeam = new TeamInfo() { spawns = 0, teamColor = TeamList.gray };
        gm.AddNewTeam(grayTeam);

        for (int i = 0; i < noOfTeams; i++)
        {
            TeamInfo nextTeam = new TeamInfo();
            nextTeam.spawns = i + 1;
            nextTeam.teamColor = setTeams[i];
            gm.AddNewTeam(nextTeam);
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
        int minValue = teamCounts.Min();
        int minIndex = teamCounts.ToList().IndexOf(minValue);
        return teams[minIndex];
    }

}
