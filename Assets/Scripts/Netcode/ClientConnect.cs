using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class ClientConnect : NetworkBehaviour
{

    /// <summary>
    /// Transfers client info from Unity Netcode object spawned on Connect.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        StartCoroutine(ServerStartCheck());
    }

    IEnumerator ServerStartCheck()
    {
        GlobalManager netcodeManager = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        while (!netcodeManager.GetServerStatus())
        {
            yield return null;
        }
        if (IsOwner)
        {
            netcodeManager.SpawnNewPlayerHostServerRpc(OwnerClientId);
        }
        Destroy(this.gameObject);
    }
}
