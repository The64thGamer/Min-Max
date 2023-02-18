using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] Gun currentGun;
    ButtonState triggerR;
    ButtonState triggerL;
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
        if(triggerR == ButtonState.started || triggerR == ButtonState.on)
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
}
