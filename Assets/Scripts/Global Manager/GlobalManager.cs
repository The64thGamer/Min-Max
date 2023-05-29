using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class GlobalManager : NetworkBehaviour
{
    [Header("Server Settings")]
    [SerializeField] TeamList team1 = TeamList.gray;
    [SerializeField] TeamList team2 = TeamList.gray;
    [SerializeField] LayerMask vrLayers;
    [SerializeField] List<PlayerDataSentToClient> playerPosRPCData = new List<PlayerDataSentToClient>();
    [SerializeField] NetworkVariable<int> ServerTickRate = new NetworkVariable<int>(10);

    [Header("Lists")]
    [SerializeField] GameObject clientPrefab;
    [SerializeField] List<Player> clients;
    [SerializeField] Transform team1Spawns;
    [SerializeField] Transform team2Spawns;
    [SerializeField] Transform particleList;

    [Header("The Map")]
    [SerializeField] Transform mapProps;
    [SerializeField] Transform mapGeometry;

    //Ect
    AllStats al;
    int team1SpawnIndex = 0;
    int team2SpawnIndex = 0;
    float tickTimer;
    bool serverStarted;

    //Constants
    const float MINANGLE = 0.8f;
    const float SPHERESIZE = 0.4f;
    const float MAXSPHERECASTDISTANCE = 20;
    const float MAXRAYCASTDISTANCE = 1000;

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += ServerStarted;
        al = GetComponent<AllStats>();

        //Settings
        NetworkManager.GetComponent<UnityTransport>().ConnectionData.Port = (ushort)PlayerPrefs.GetInt("ServerPort");

        //Local Host, Server Relay Host, Client Connect
        switch (PlayerPrefs.GetInt("LoadMapMode"))
        {
            case 0:
                NetworkManager.Singleton.StartHost();
                team1 = SelectTeams(team1, team2, PlayerPrefs.GetInt("Team1Setting"));
                team2 = SelectTeams(team2, team1, PlayerPrefs.GetInt("Team2Setting"));
                ModifyTeamsAcrossServer();
                Debug.Log("Started Host");
                break;
            case 1:
                NetworkManager.Singleton.StartHost();
                team1 = SelectTeams(team1, team2, PlayerPrefs.GetInt("Team1Setting"));
                team2 = SelectTeams(team2, team1, PlayerPrefs.GetInt("Team2Setting"));
                ModifyTeamsAcrossServer();
                Debug.Log("Started Host");
                break;
            case 2:
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started Client");
                break;
            case 3:
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started Client");
                break;
            default:
                break;
        }
    }

    private void Update()
    {
        for (int i = 0; i < clients.Count; i++)
        {
            //Game Logic
            CheckAllPlayerInputs(clients[i]);
            clients[i].GetController().MovePlayer(clients[i].GetTracker().GetMoveAxis(), clients[i].GetTracker().GetRHandAButton());

            //Client Networking
            if (clients[i].IsOwner)
            {
                SendJoystickServerRpc(clients[i].GetTracker().GetPlayerNetworkData(), clients[i].GetPlayerID());
            }
        }
    }
    void LateUpdate()
    {
        //Server Networking
        if (tickTimer > 1.0f / (float)ServerTickRate.Value)
        {
            tickTimer = 0;
            if (IsHost)
            {
                for (int i = 0; i < playerPosRPCData.Count; i++)
                {
                    playerPosRPCData[i] = clients[i].GetTracker().GetPlayerPosData();
                }
                SendPosClientRpc(playerPosRPCData.ToArray());
            }
        }
        tickTimer += Time.deltaTime;
    }

    TeamList SelectTeams(TeamList teamSet, TeamList teamRef, int setting)
    {
        TeamList[] otherOptions = new TeamList[0];
        switch (setting)
        {
            case 0:
                switch (teamRef)
                {
                    case TeamList.orange:
                        otherOptions = new TeamList[]{TeamList.green,TeamList.lightBlue,TeamList.blue,TeamList.purple,TeamList.brown};
                        break;
                    case TeamList.yellow:
                        otherOptions = new TeamList[] {TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.brown};
                        break;
                    case TeamList.green:
                        otherOptions = new TeamList[] { TeamList.orange,TeamList.blue, TeamList.purple, TeamList.beige, TeamList.brown};
                        break;
                    case TeamList.lightBlue:
                        otherOptions = new TeamList[] { TeamList.orange, TeamList.yellow,TeamList.purple, TeamList.beige, TeamList.brown};
                        break;
                    case TeamList.blue:
                        otherOptions = new TeamList[] { TeamList.orange, TeamList.yellow, TeamList.green,TeamList.beige, TeamList.brown};
                        break;
                    case TeamList.purple:
                        otherOptions = new TeamList[] {TeamList.yellow, TeamList.green, TeamList.lightBlue,TeamList.beige, TeamList.brown};
                        break;
                    case TeamList.beige:
                        otherOptions = new TeamList[] {TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.brown};
                        break;
                    case TeamList.brown:
                        otherOptions = new TeamList[] {TeamList.yellow, TeamList.green, TeamList.lightBlue, TeamList.blue, TeamList.purple, TeamList.beige};
                        break;
                    case TeamList.gray:
                        break;
                    default:
                        break;
                }
                teamSet = otherOptions[Random.Range(0, otherOptions.Length)];
                break;
            case 1:
                teamSet = TeamList.orange;
                break;
            case 2:
                teamSet = TeamList.yellow;
                break;
            case 3:
                teamSet = TeamList.green;
                break;
            case 4:
                teamSet = TeamList.lightBlue;
                break;
            case 5:
                teamSet = TeamList.blue;
                break;
            case 6:
                teamSet = TeamList.purple;
                break;
            case 7:
                teamSet = TeamList.beige;
                break;
            case 8:
                teamSet = TeamList.brown;
                break;
            default:
                teamSet = TeamList.gray;
                break;
        }
        return teamSet;
    }

    public void ChangeTeams(TeamList teamOne, TeamList teamTwo)
    {
        team1 = teamOne;
        team2 = teamTwo;
        ModifyTeamsAcrossServer();
    }

    void ModifyTeamsAcrossServer()
    {
        if (IsHost)
        {
            UpdateClientTeamColorsClientRpc(team1, team2);
        }

        float team1Final = (float)GetTeam1() + 1;
        float team2Final = (float)GetTeam2() + 1;
        Renderer[] meshes = mapProps.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < meshes.Length; i++)
        {
            Material[] mats = meshes[i].sharedMaterials;
            for (int r = 0; r < mats.Length; r++)
            {
                mats[r].SetFloat("_Team_1", team1Final);
                mats[r].SetFloat("_Team_2", team2Final);
            }
        }
        meshes = mapGeometry.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < meshes.Length; i++)
        {
            Material[] mats = meshes[i].sharedMaterials;
            for (int r = 0; r < mats.Length; r++)
            {
                mats[r].SetFloat("_Team_1", team1Final);
                mats[r].SetFloat("_Team_2", team2Final);
            }
        }
        for (int i = 0; i < clients.Count; i++)
        {
            clients[i].UpdateTeamColor();
        }
        Debug.Log("Teams Selected: " + team1 + ", " + team2);
    }

    void CheckAllPlayerInputs(Player player)
    {
        if (player != null)
        {
            if (player.GetTracker().GetTriggerR())
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    if (clients[i].GetCurrentGun())
                    {
                        clients[i].GetCurrentGun().Fire();
                    }
                }
            }
        }
    }

    //Exploit: Hit needs to be parsed to ensure extreme angles aren't achievable.
    public void SpawnProjectile(Player player)
    {
        GunProjectiles fp = al.SearchGuns(player.GetCurrentGun().GetNameKey()); ;
        if (fp.firePrefab != null)
        {
            Vector3 firepos = player.GetTracker().GetRightHandFirePos(fp.firepoint);
            GameObject currentProjectile = GameObject.Instantiate(fp.firePrefab);
            currentProjectile.transform.parent = particleList;
            Vector3 fireAngle = CalculateFireAngle(player, firepos);
            currentProjectile.GetComponent<Projectile>().SetProjectile(firepos, fireAngle, player.GetCurrentGun().SearchStats(ChangableWeaponStats.bulletSpeed), player.GetTeamLayer(), CalculcateFirePosition(fireAngle, player, firepos));
        }
    }

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    public Vector3 CalculateFireAngle(Player player, Vector3 firePoint)
    {
        Transform cam = player.GetTracker().GetCamera();
        RaycastHit hit;
        Vector3 startCast = cam.position + (cam.forward * SPHERESIZE);
        Vector3 finalAngle = Vector3.one;

        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);

        Vector3 fpForward = player.GetTracker().GetRightHandSafeForward();

        if (Physics.SphereCast(startCast, SPHERESIZE, cam.forward, out hit, MAXSPHERECASTDISTANCE, layermask))
        {
            finalAngle = ((startCast + (cam.forward * hit.distance)) - firePoint);
        }
        else
        {
            finalAngle = cam.forward;
        }
        float dotAngle = Vector3.Dot(fpForward, finalAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            float percentage = (dotAngle - MINANGLE) / (1 - MINANGLE);
            finalAngle = Vector3.Slerp(fpForward, finalAngle, percentage);
            return finalAngle;
        }
        return fpForward;
    }

    public Vector3 CalculcateFirePosition(Vector3 fireAngle, Player player, Vector3 firePoint)
    {
        RaycastHit hit;
        Transform rHand = player.GetTracker().GetRightHand();
        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);
        float dotAngle = Vector3.Dot(rHand.forward, fireAngle.normalized);
        if (dotAngle > MINANGLE)
        {
            if (Physics.Raycast(firePoint, fireAngle, out hit, MAXRAYCASTDISTANCE, layermask))
            {
                return hit.point;
            }
        }
        return firePoint + (100 * fireAngle.normalized);
    }

    public LayerMask GetIgnoreTeamAndVRLayerMask(Player player)
    {
        LayerMask mask;
        switch (player.GetTeam())
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

    public TeamList GetTeam1()
    {
        return team1;
    }

    public TeamList GetTeam2()
    {
        return team2;
    }

    public AllStats GetAllStats()
    {
        return al;
    }

    public void DisconnectClient(Player player)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i] == player)
            {
                clients.RemoveAt(i);
            }
        }
    }

    public void UpdateTickrates(int server)
    {
        ServerTickRate.Value = server;
    }

    void ServerStarted()
    {
        serverStarted = true;
    }

    public bool GetServerStatus()
    {
        return serverStarted;
    }

    public void AddPlayerToClientList(Player player)
    {
        clients.Add(player);
        playerPosRPCData.Add(new PlayerDataSentToClient());
    }

    [ServerRpc(RequireOwnership = false)]
    void SendJoystickServerRpc(PlayerDataSentToServer serverData, ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].OwnerClientId == id && !clients[i].IsOwner)
            {
                clients[i].GetTracker().UpdatePlayerPositions(serverData);
                clients[i].GetTracker().SetPlayerMoveAxis(serverData.rightJoystick);
                return;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AssignNewClientServerRpc(ulong id)
    {
        //Client Object Spawning
        bool team = clients.Count % 2 != 0;
        Vector3 spawnPos;
        TeamList debugList = TeamList.gray;
        if (team)
        {
            spawnPos = team1Spawns.GetChild(team1SpawnIndex).position;
            team1SpawnIndex = (team1SpawnIndex + 1) % team1Spawns.childCount;
            debugList = GetTeam1();
        }
        else
        {
            spawnPos = team2Spawns.GetChild(team2SpawnIndex).position;
            team2SpawnIndex = (team2SpawnIndex + 1) % team2Spawns.childCount;
            debugList = GetTeam2();
        }

        Debug.Log("New Player Joined (#" + clients.Count + "), Team " + debugList);
        SendNewPlayerDataBackClientRpc(id, team, spawnPos);
        UpdateClientTeamColorsClientRpc(team1, team2);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnNewPlayerHostServerRpc(ulong id)
    {
        Debug.Log("Player Spawned On Host");
        GameObject client = GameObject.Instantiate(clientPrefab, Vector3.zero, Quaternion.identity);
        client.GetComponent<NetworkObject>().SpawnWithOwnership(id);
    }

    /// <summary>
    /// Client Data needs to be sorted by ID
    /// </summary>
    /// <param name="data"></param>
    [ClientRpc]
    private void SendPosClientRpc(PlayerDataSentToClient[] data)
    {
        playerPosRPCData = data.ToList<PlayerDataSentToClient>();
        if (IsHost) { return; }
        for (int e = 0; e < clients.Count; e++)
        {
            for (int i = 0; i < playerPosRPCData.Count; i++)
            {
                //Check run incase of player disconnect+reconnect inside same tick.
                if (clients[e].GetPlayerID() == playerPosRPCData[i].id)
                {
                    clients[e].GetTracker().SetNewClientPosition(playerPosRPCData[i].pos, playerPosRPCData[i].velocity, playerPosRPCData[i].predictionTime);
                    if (!IsOwner)
                    {
                        clients[e].GetTracker().UpdatePlayerPositions(playerPosRPCData[i]);
                    }
                }
            }

        }
    }

    [ClientRpc]
    void SendNewPlayerDataBackClientRpc(ulong id, bool team, Vector3 spawnPos)
    {
        Player player = null;
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                player = clients[i];
            }
        }
        if (player != null)
        {
            if (team)
            {
                player.SetTeam(Team.team1);
            }
            else
            {
                player.SetTeam(Team.team2);
            }
            player.GetTracker().ForceNewPosition(spawnPos);
        }
    }

    [ClientRpc]
    private void UpdateClientTeamColorsClientRpc(TeamList a, TeamList b)
    {
        if (IsHost) { return; }
        ChangeTeams(a, b);
    }
}

[System.Serializable]
public struct PlayerDataSentToClient : INetworkSerializable
{
    public ulong id;
    public float predictionTime;
    public Vector3 headsetPos;
    public Quaternion headsetRot;
    public Vector3 rHandPos;
    public Quaternion rHandRot;
    public Vector3 lHandPos;
    public Quaternion lHandRot;
    public Vector3 pos;
    public Vector3 velocity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref predictionTime);
        serializer.SerializeValue(ref headsetPos);
        serializer.SerializeValue(ref headsetRot);
        serializer.SerializeValue(ref rHandPos);
        serializer.SerializeValue(ref rHandRot);
        serializer.SerializeValue(ref lHandPos);
        serializer.SerializeValue(ref lHandRot);
        serializer.SerializeValue(ref pos);
        serializer.SerializeValue(ref velocity);
    }
}

[System.Serializable]
public struct PlayerDataSentToServer : INetworkSerializable
{
    public Vector3 headsetPos;
    public Quaternion headsetRot;
    public Vector3 rHandPos;
    public Quaternion rHandRot;
    public Vector3 lHandPos;
    public Quaternion lHandRot;

    //Controls
    public Vector2 rightJoystick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref headsetPos);
        serializer.SerializeValue(ref headsetRot);
        serializer.SerializeValue(ref rHandPos);
        serializer.SerializeValue(ref rHandRot);
        serializer.SerializeValue(ref lHandPos);
        serializer.SerializeValue(ref lHandRot);
        serializer.SerializeValue(ref rightJoystick);
    }
}