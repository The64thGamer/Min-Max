using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Windows;

public class AiPlayer : NetworkBehaviour
{
    Player player;
    Transform headset;
    Transform rhand;
    GlobalManager gm;
    PlayerTracker tracker;
    float height;


    //Target
    Player target;
    Transform targetHeadset;
    bool isTargetEnemy;

    //Timers
    float timeToSwap;
    float timeToChangePos;

    //Navmesh
    NavMeshPath path;
    int currentNavCorner = 0;

    //Const
    const float maxNavRange = 15;
    const float maxEnemyNavRange = 20;
    const ulong botID = 64646464646464;


    private void Start()
    {
        player = GetComponent<Player>();
        if (player.GetPlayerID() < botID || !IsHost)
        {
            Destroy(this);
        }
        else
        {
            path = new NavMeshPath();
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            headset = player.GetTracker().GetCamera();
            rhand = player.GetTracker().GetRightHand();
            tracker = player.GetTracker();
            height = PlayerPrefs.GetFloat("Settings: PlayerHeight") - 0.127f;

        }
    }

    void Update()
    {
        //Swapping Target
        if (timeToSwap <= 0)
        {
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
                if (NavMesh.SamplePosition(target.transform.position + (Random.insideUnitSphere * maxEnemyNavRange), out hit, maxNavRange, 1))
                {
                    setDestination = hit.position;
                }
            }
            else
            {
                //Slowly converges on parent wire of random enemy team by calculating halfway point and scrambling pos;
                NavMeshHit hit;

                if(NavMesh.SamplePosition((Random.insideUnitSphere * maxNavRange) + gm.GetMatchFocalPoint(player.GetTeam()), out hit, maxNavRange, 1))
                {
                    setDestination = hit.position;
                }
            }
            if (setDestination != Vector3.zero)
            {
                NavMesh.CalculatePath(transform.position, setDestination, NavMesh.AllAreas, path);
            }
            currentNavCorner = 0;
        }
        timeToChangePos -= Time.deltaTime;


        //Data
        PlayerDataSentToServer data = tracker.GetPlayerNetworkData();

        if (target != null && targetHeadset.position - rhand.position != Vector3.zero && targetHeadset.position - headset.position != Vector3.zero)
        {
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
        }
        else
        {
            data.shoot = false;
        }

        data.headsetPos = new Vector3(0, height, 0);
        data.rHandPos = new Vector3(0, height - 0.5f, 0) + (headset.right * 0.35f);
        data.lHandPos = new Vector3(0, height - 0.5f, 0) + (headset.right * -0.35f);

        if (path != null && path.corners.Length != 0)
        {
            if(currentNavCorner >= path.corners.Length)
            {
                currentNavCorner = path.corners.Length-1;
            }
            Vector3 currentPath = path.corners[currentNavCorner] - transform.position;
            Vector3 camForward = headset.forward;
            Vector3 camRight = headset.right;
            if (currentPath.magnitude <= 0.25f)
            {
                currentNavCorner = Mathf.Min(path.corners.Length, currentNavCorner + 1);
            }
            currentPath.Normalize();
            camForward.y = 0;
            camRight.y = 0;
            data.rightJoystick.y = Vector3.Dot(currentPath, camForward);
            data.rightJoystick.x = Vector3.Dot(currentPath, camRight);

        }
        tracker.ServerSyncPlayerInputs(data);

    }

    private void OnDrawGizmosSelected()
    {
        if (path != null)
        {
            List<Vector3> lines = path.corners.ToList();
            if(lines.Count % 2 == 1)
            {
                lines.Insert(0,transform.position);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                Gizmos.DrawLine(lines[i], lines[i + 1]);
                i++;
            }
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
            if (Physics.Raycast(headset.position, clients[i].GetTracker().GetCamera().position + (Vector3.up * -0.4f) - headset.position, out hit))
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
            if (currentFind.GetHealth() > 0)
            {
                target = currentFind;
                targetHeadset = target.GetTracker().GetCamera();
                isTargetEnemy = currentFind.GetTeam() != player.GetTeam();
                timeToSwap = Random.Range(0.3f, 2f);
                timeToChangePos = 0;
            }
        }
        else
        {
            target = null;
            targetHeadset = null;
            isTargetEnemy = false;
            timeToSwap = Random.Range(0.1f, 1);
        }
    }
}
