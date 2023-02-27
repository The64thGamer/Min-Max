using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class ClientPlayer : NetworkBehaviour
{
    GlobalManager gm;
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            SetAllLayers(gameObject, LayerMask.NameToLayer("HideFromVR"));
        }
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        //spawn code goes here
    }

    void SetAllLayers(GameObject child, int layer)
    {
        child.gameObject.layer = layer;
        foreach (Transform subchild in child.transform)
        {
            SetAllLayers(subchild.gameObject, layer);
        }
    }
}
