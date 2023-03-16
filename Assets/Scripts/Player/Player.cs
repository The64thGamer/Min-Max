using Autohand;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] Team currentTeam;
    [SerializeField] ClassList currentClass;
    [SerializeField] ClassStats currentStats;
    [SerializeField] PlayerTracker tracker;
    [SerializeField] AutoHandPlayer autoHand;

    private void Start()
    {
        SetTeam(ClassList.programmer);
    }

    public void SetTeam(ClassList setClass)
    {
        currentClass = setClass;
        currentStats = GameObject.Find("Global Manager").GetComponent<AllStats>().GetClassStats(ClassList.programmer);
    }

    public PlayerTracker GetTracker()
    {
        return tracker;
    }

    public AutoHandPlayer GetAutoHand()
    {
        return autoHand;
    }

    public Team GetTeam()
    {
        return currentTeam;
    }

    public Gun GetCurrentGun()
    {
        return currentGun;
    }

    public ClassList GetCurrentClass()
    {
        return currentClass;
    }
    public ClassStats GetClassStats()
    {
        return currentStats;
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
