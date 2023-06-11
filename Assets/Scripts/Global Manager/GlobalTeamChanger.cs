using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class GlobalTeamChanger : MonoBehaviour
{
    [SerializeField] List<TeamList> teams;
    [SerializeField] bool submitChange;

    void Update()
    {
        if(submitChange)
        {
            submitChange = false;
            RepaintTeams();
        }
    }

    void RepaintTeams()
    {
        this.GetComponent<GenericGamemode>().SetTeams(teams);
    }
}
