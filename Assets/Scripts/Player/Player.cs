using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] Team currentTeam;
    [SerializeField] ClassList currentClass;

    [SerializeField] LayerMask vrLayers;
    [SerializeField] Transform camera;
    [SerializeField] Transform handR;
    [SerializeField] Transform handL;

    public LayerMask GetVRLayers()
    {
        return vrLayers;
    }

    public Team GetTeam()
    {
        return currentTeam;
    }

    public Gun GetCurrentGun()
    {
        return currentGun;
    }

    public Transform GetCamera()
    {
        return camera;
    }

    public Transform GetRightHand()
    {
        return handR;
    }

    public Transform GetLeftHand()
    {
        return handL;
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
