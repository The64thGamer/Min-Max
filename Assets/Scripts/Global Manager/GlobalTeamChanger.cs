using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GlobalTeamChanger : MonoBehaviour
{
    [SerializeField] TeamList team1;
    [SerializeField] TeamList team2;

    TeamList oldTeam1;
    TeamList oldTeam2;
    private void Start()
    {
        oldTeam1 = team1;
        oldTeam2 = team2;
    }
    void Update()
    {
        if(oldTeam1 != team1 || oldTeam2 != team2)
        {
            oldTeam1 = team1;
            oldTeam2 = team2;
            RepaintTeams();
        }
    }

    void RepaintTeams()
    {
        this.GetComponent<GlobalManager>().ChangeTeams(team1, team2);
    }
}
