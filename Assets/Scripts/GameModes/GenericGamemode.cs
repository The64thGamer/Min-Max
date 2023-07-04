using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public abstract class GenericGamemode : NetworkBehaviour
{
    public abstract void SetTeams();
    public abstract void SetTeams(List<TeamList> setTeams);

    public abstract TeamList DecideWhichPlayerTeam();

    public void ResetMatch()
    {
        if (IsHost)
        {
            GlobalManager gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            gm.RemoveAllWiresClientRpc();
            List<Player> clients = gm.GetClients();

            //Assuming only 2 teams, please rewrite later
            TeamList team1 = gm.GetTeams()[1].teamColor;
            TeamList team2 = gm.GetTeams()[2].teamColor;

            for (int i = 0; i < clients.Count; i++)
            {
                TeamList team = TeamList.gray;
                if (clients[i].GetTeam() == team1)
                {
                    team = team2;
                }
                else if (clients[i].GetTeam() == team2)
                {
                    team = team1;
                }

                //Auto Team
                ClassList autoClass = ClassList.programmer;
                string autoGun = "W.I.P.";
                int random = UnityEngine.Random.Range(0, 3);
                switch (random)
                {
                    case 0:
                        autoClass = ClassList.programmer;
                        autoGun = "W.I.P.";
                        break;
                    case 1:
                        autoClass = ClassList.computer;
                        autoGun = "W.I.P.";
                        break;
                    case 2:
                        autoClass = ClassList.fabricator;
                        autoGun = "O-S F-O";
                        break;
                    default:
                        break;
                }

                //Random cosmetics
                List<int> cos = new List<int>();
                switch (autoClass)
                {
                    case ClassList.programmer:
                        int randoP = UnityEngine.Random.Range(0, 4);
                        switch (randoP)
                        {
                            case 0:
                                cos.Add(5);
                                break;
                            default:
                                break;
                        }
                        break;
                    case ClassList.fabricator:
                        int randoF = UnityEngine.Random.Range(0, 4);
                        switch (randoF)
                        {
                            case 0:
                                cos.Add(0);
                                cos.Add(1);
                                break;
                            case 1:
                                cos.Add(0);
                                cos.Add(6);
                                break;
                            case 2:
                                cos.Add(1);
                                cos.Add(5);
                                break;
                            case 3:
                                cos.Add(0);
                                cos.Add(5);
                                break;
                            default:
                                break;
                        }
                        break;
                    case ClassList.computer:
                        cos.Add(0);
                        cos.Add(1);
                        cos.Add(2);
                        break;
                    default:
                        break;
                }
                PlayerInfoSentToClient pdstc = new PlayerInfoSentToClient
                {
                    id = clients[i].GetPlayerID(),
                    currentClass = autoClass,
                    currentTeam = team,
                    cosmetics = cos.ToArray(),
                    gunName = autoGun,
                };
                gm.AssignPlayerClassAndTeamClientRpc(pdstc);
                gm.RespawnPlayerClientRpc(clients[i].GetPlayerID(), clients[i].GetTeam());

            }
        }
    }

    protected TeamList SelectTeams(List<TeamList> teamRef, int setting)
    {
        TeamList teamSet;
        switch (setting)
        {
            case 0:
                TeamList[] otherOptions = new TeamList[0];
                for (int i = 0; i < teamRef.Count; i++)
                {
                    TeamList[] oldOptions = otherOptions;
                    switch (teamRef[i])
                    {
                        case TeamList.orange:
                            otherOptions = new TeamList[] { TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.brown };
                            break;
                        case TeamList.yellow:
                            otherOptions = new TeamList[] { TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.brown };
                            break;
                        case TeamList.green:
                            otherOptions = new TeamList[] { TeamList.orange, TeamList.blue, TeamList.purple, TeamList.beige, TeamList.brown };
                            break;
                        case TeamList.lightBlue:
                            otherOptions = new TeamList[] { TeamList.orange, TeamList.yellow, TeamList.purple, TeamList.beige, TeamList.brown };
                            break;
                        case TeamList.blue:
                            otherOptions = new TeamList[] { TeamList.orange, TeamList.yellow, TeamList.green, TeamList.beige, TeamList.brown };
                            break;
                        case TeamList.purple:
                            otherOptions = new TeamList[] { TeamList.yellow, TeamList.green, TeamList.lightBlue, TeamList.beige, TeamList.brown };
                            break;
                        case TeamList.beige:
                            otherOptions = new TeamList[] { TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.brown };
                            break;
                        case TeamList.brown:
                            otherOptions = new TeamList[] { TeamList.yellow, TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.beige };
                            break;
                        case TeamList.gray:
                            break;
                        default:
                            break;
                    }
                    if (i > 0)
                    {
                        //Finds the best possible teams that complements the current ones
                        otherOptions = oldOptions.Intersect(otherOptions).ToArray();

                        //If no valid best possible result, pick a random team not chosen yet.
                        if (otherOptions.Length == 0)
                        {
                            List<TeamList> allteams = new List<TeamList> { TeamList.orange, TeamList.yellow, TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.beige, TeamList.brown };
                            for (int e = 0; e < teamRef.Count; e++)
                            {
                                allteams.Remove(teamRef[e]);
                            }
                            otherOptions = allteams.ToArray();
                            break;
                        }
                    }
                }
                if (otherOptions.Length > 0)
                {
                    teamSet = otherOptions[Random.Range(0, otherOptions.Length)];
                }
                else
                {
                    teamSet = (TeamList)Random.Range(0, 8);
                }
                break;
            case 1:
                teamSet = TeamList.orange;
                break;
            case 2:
                teamSet = TeamList.yellow;
                break;
            case 3:
                teamSet = TeamList.green;
                break;
            case 4:
                teamSet = TeamList.lightBlue;
                break;
            case 5:
                teamSet = TeamList.blue;
                break;
            case 6:
                teamSet = TeamList.purple;
                break;
            case 7:
                teamSet = TeamList.beige;
                break;
            case 8:
                teamSet = TeamList.brown;
                break;
            default:
                teamSet = TeamList.gray;
                break;
        }
        return teamSet;
    }


}