using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenDot : MonoBehaviour
{
    bool visible;

    SpriteRenderer spr;

    private void Start()
    {
        spr = this.GetComponentInChildren<SpriteRenderer>();
    }


    private void LateUpdate()
    {
        if (visible)
        {
            spr.enabled = true;
        }
        else
        {
            spr.enabled = false;
        }
        visible = false;
    }

    public void DotVisible()
    {
        visible = true;
    }
}
