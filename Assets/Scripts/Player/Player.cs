using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] Gun currentGun;
    [SerializeField] Team currentTeam;
    ButtonState triggerR;
    ButtonState triggerL;
    [SerializeField] LayerMask vrLayers;
    public enum ButtonState
    {
        off,
        started,
        on,
        cancelled,
    }
    public void TriggerPressed(bool left)
    {
        if (!left)
        {
            if (triggerR == ButtonState.off)
            {
                triggerR = ButtonState.started;
            }
        }
        else
        {
            if (triggerL == ButtonState.off)
            {
                triggerL = ButtonState.started;
            }
        }
    }
    public void TriggerReleased(bool left)
    {
        if (!left)
        {
            if (triggerR == ButtonState.on)
            {
                triggerR = ButtonState.cancelled;
            }
        }
        else
        {
            if (triggerL == ButtonState.on)
            {
                triggerL = ButtonState.cancelled;
            }
        }
    }
    private void Update()
    {
        if (triggerR == ButtonState.started || triggerR == ButtonState.on)
        {
            currentGun.Fire();
        }
    }

    void LateUpdate()
    {
        UpdateTriggers();
    }

    void UpdateTriggers()
    {
        switch (triggerR)
        {
            case ButtonState.started:
                triggerR = ButtonState.on;
                break;
            case ButtonState.cancelled:
                triggerR = ButtonState.off;
                break;
            default:
                break;
        }
        switch (triggerL)
        {
            case ButtonState.started:
                triggerL = ButtonState.on;
                break;
            case ButtonState.cancelled:
                triggerL = ButtonState.off;
                break;
            default:
                break;
        }
    }

    public Team GetTeam()
    {
        return currentTeam;
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

    public LayerMask GetIgnoreTeamAndVRLayerMask()
    {
        LayerMask mask;
        switch (currentTeam)
        {
            case Team.team1:
                mask = 1 << LayerMask.NameToLayer("Team1");
                break;
            case Team.team2:
                mask = 1 << LayerMask.NameToLayer("Team2");
                break;
            default:
                mask = 1 << LayerMask.NameToLayer("Neutral");
                break;
        }
        mask = mask | vrLayers;
        mask = ~mask;
        return mask;
    }
}
