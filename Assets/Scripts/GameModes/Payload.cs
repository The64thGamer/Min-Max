using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Payload : GenericGamemode
{
    GlobalManager gm;
    [Range(2,8)]
    [SerializeField] uint noOfTeams = 2;
    private void Start()
    {
        gm = this.GetComponent<GlobalManager>();
    }

    public override void SetTeams()
    {
        //Gray Team Always Team 0
        TeamInfo grayTeam = new TeamInfo() { spawns = gm.GetTeamSpawns()[0], teamColor = TeamList.gray };
        gm.AddNewTeam(grayTeam);

        for (int i = 0; i < noOfTeams; i++)
        {
            TeamInfo nextTeam = new TeamInfo();
            nextTeam.spawns = gm.GetTeamSpawns()[i + 1];
            nextTeam.teamColor = SelectTeams(gm.GetTeamColors(), PlayerPrefs.GetInt("Team1Setting"));
        }
    }
}
