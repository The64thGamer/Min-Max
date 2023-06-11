using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public abstract class GenericGamemode : MonoBehaviour
{

    public abstract void SetTeams();


    TeamList SelectTeams(TeamList teamSet, List<TeamList> teamRef, int setting)
    {
       
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
                    if(i > 0)
                    {
                        otherOptions = oldOptions.Intersect(otherOptions).ToArray();
                    }
                }
                
                teamSet = otherOptions[Random.Range(0, otherOptions.Length)];
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