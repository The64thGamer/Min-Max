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
        if (IsHost)
        {
            //Wait for server to boot up
            while (!netcodeManager.GetServerStatus())
            {
                yield return null;
            }
        }
        if (IsOwner)
        {
            List<int> newCosInts = new List<int>();

            for (int i = 0; i < 11; i++)
            {
                int check = PlayerPrefs.GetInt("Loadout " + PlayerPrefs.GetInt("Selected Class") + " Var: " + PlayerPrefs.GetInt("Selected Loadout") + " Type: " + i) - 1;
                if (check >= 0)
                {
                    newCosInts.Add(check);
                }
            }

            netcodeManager.SpawnNewPlayerHostServerRpc(PlayerPrefs.GetString("Settings: Player Name"), PlayerPrefs.GetInt("Selected Class"), newCosInts.ToArray(), default);
        }
        //Destroy(this.gameObject);
    }
}
