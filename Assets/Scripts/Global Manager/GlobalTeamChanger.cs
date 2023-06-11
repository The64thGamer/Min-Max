using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GlobalTeamChanger : MonoBehaviour
{
    [SerializeField] List<TeamList> teams;

    List<TeamList> oldTeam;

    private void Start()
    {
        oldTeam = teams;
    }
    void Update()
    {
        if(oldTeam != teams)
        {
            oldTeam = teams;
            RepaintTeams();
        }
    }

    void RepaintTeams()
    {
        this.GetComponent<GenericGamemode>().SetTeams(teams);
    }
}
