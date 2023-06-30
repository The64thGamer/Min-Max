using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class AiPlayer : NetworkBehaviour
{
    Player player;
    Transform headset;
    Transform rhand;
    GlobalManager gm;
    const ulong botID = 64646464646464;
    PlayerTracker tracker;
    NavMeshAgent agent;

    //Target
    Player target;
    Transform targetHeadset;
    bool isTargetEnemy;

    //Timers
    float timeToSwap;
    float timeToChangePos;

    //Const
    const float maxNavRange = 40;

    private void Start()
    {
        player = GetComponent<Player>();
        if (player.GetPlayerID() < botID || !IsHost)
        {
            Destroy(this);
        }
        else
        {
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            headset = player.GetTracker().GetCamera();
            rhand = player.GetTracker().GetRightHand();
            tracker = player.GetTracker();
            agent = this.AddComponent<NavMeshAgent>();
        }
    }

    void Update()
    {
        //Swapping Target
        if (timeToSwap <= 0)
        {
            timeToSwap = Random.Range(1, 10);
            ChangeFocus();
        }
        timeToSwap -= Time.deltaTime;

        //Swapping Target Position
        if (timeToChangePos <= 0)
        {
            timeToChangePos = Random.Range(0.1f, 5.0f);

            Vector3 setDestination = Vector3.zero;
            if (target != null && isTargetEnemy)
            {
                NavMeshHit hit;
                NavMesh.SamplePosition(target.transform.position, out hit, maxNavRange, 1);
                setDestination = hit.position;
            }
            else
            {
                NavMeshHit hit;
                NavMesh.SamplePosition((Random.insideUnitSphere * maxNavRange) + transform.position, out hit, maxNavRange, 1);
                setDestination = hit.position;
            }
            agent.SetDestination(setDestination);
        }
        timeToChangePos -= Time.deltaTime;

        if (targetHeadset != null)
        {
            PlayerDataSentToServer data = tracker.GetPlayerNetworkData();
            data.rHandRot = Quaternion.Lerp(rhand.rotation, Quaternion.LookRotation(targetHeadset.position - rhand.position, Vector3.up), Time.deltaTime * 20);
            data.headsetRot = Quaternion.Lerp(headset.rotation, Quaternion.LookRotation(targetHeadset.position - headset.position, Vector3.up), Time.deltaTime * 10);
            if (isTargetEnemy)
            {
                data.shoot = true;
            }
            else
            {
                data.shoot = false;
            }
            tracker.ServerSyncPlayerInputs(data);
        }
        else
        {
            PlayerDataSentToServer data = tracker.GetPlayerNetworkData();
            data.shoot = false;
            tracker.ServerSyncPlayerInputs(data);
        }
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
                        else if (currentlyOnlyFriendlies && isEnemy)
                        {
                            currentFind = collidePlayer;
                        }
                    }
                }
            }
        }
        if (currentFind != null)
        {
            target = currentFind;
            targetHeadset = target.GetTracker().GetCamera();
            isTargetEnemy = currentFind.GetTeam() != player.GetTeam();
        }
        else
        {
            target = null;
            targetHeadset = null;
            isTargetEnemy = false;
        }
    }
}
