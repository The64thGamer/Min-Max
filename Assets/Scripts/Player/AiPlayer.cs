using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class AiPlayer : NetworkBehaviour
{
    Player player;
    Transform headset;
    GlobalManager gm;
    const ulong botID = 64646464646464;

    //Target
    Player target;
    Transform targetHeadset;
    bool isTargetEnemy;

    private void Start()
    {
        player = GetComponent<Player>();
        if (player.GetPlayerID() < botID || !IsHost)
        {
            Destroy(this);
        }
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        headset = player.GetTracker().GetCamera();
    }

    float timeToSwap;
    void Update()
    {
        if (timeToSwap <= 0)
        {
            timeToSwap = Random.Range(1, 10);
            ChangeFocus();
        }
        timeToSwap -= Time.deltaTime;

        headset.rotation = Quaternion.Lerp(headset.rotation, Quaternion.LookRotation(targetHeadset.position - headset.position, headset.up), Time.deltaTime * 10);
    }

    void ChangeFocus()
    {
        List<Player> clients = gm.GetClients();
        RaycastHit hit;
        Player currentFind = null;
        for (int i = 0; i < clients.Count; i++)
        {
            //Check for players within view
            if (Physics.Raycast(headset.position, clients[i].GetTracker().GetCamera().position - headset.position, out hit))
            {
                Player collidePlayer = hit.collider.GetComponent<Player>();
                if (collidePlayer != null)
                {
                    if (currentFind == null)
                    {
                        currentFind = collidePlayer;
                    }
                    else
                    {
                        //If an enemy is within seeable players, prioritize them
                        bool currentlyOnlyFriendlies = currentFind.GetTeam() == player.GetTeam();
                        bool isEnemy = collidePlayer.GetTeam() != player.GetTeam();
                        if ((currentlyOnlyFriendlies && !isEnemy) || (!currentlyOnlyFriendlies && isEnemy))
                        {
                            //Prioritize closest player
                            if (Vector3.Distance(headset.position, collidePlayer.GetTracker().GetCamera().position) < Vector3.Distance(headset.position, currentFind.GetTracker().GetCamera().position))
                            {
                                currentFind = collidePlayer;
                            }
                        }
                        else if(currentlyOnlyFriendlies && isEnemy)
                        {
                            currentFind = collidePlayer;
                        }
                    }
                }
            }
        }
        if(currentFind == null)
        {
            //When cant find a target, looks at random player in the map
            target = clients[Random.Range(0, gm.GetClients().Count)];
            targetHeadset = target.GetTracker().GetCamera();
            isTargetEnemy = false;
        }
        else
        {
            target = currentFind;
            targetHeadset = target.GetTracker().GetCamera();
            isTargetEnemy = currentFind.GetTeam() != player.GetTeam();
        }
    }
}
