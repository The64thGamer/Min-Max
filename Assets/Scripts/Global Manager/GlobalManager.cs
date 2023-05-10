using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class GlobalManager : NetworkBehaviour
{
    [Header("Server Settings")]
    [SerializeField] TeamList team1 = TeamList.gray;
    [SerializeField] TeamList team2 = TeamList.gray;
    [SerializeField] LayerMask vrLayers;
    [SerializeField] List<PlayerPosData> playerPosRPCData = new List<PlayerPosData>();
    [SerializeField] NetworkVariable<int> ServerTickRate = new NetworkVariable<int>(10);
    [SerializeField] NetworkVariable<int> ClientInputTickRate = new NetworkVariable<int>(10);

    [Header("Global Prefabs")]
    [SerializeField] GameObject clientPrefab;

    [Header("Lists")]
    [SerializeField] Player host;
    [SerializeField] List<Player> clients;
    [SerializeField] Transform team1Spawns;
    [SerializeField] Transform team2Spawns;
    [SerializeField] Transform particleList;

    //Network
    List<NetworkVariable<PlayerNetworkDataServer>> playerData;

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
                SelectTeams();
                Debug.Log("Started Host");
                break;
            case 1:
                NetworkManager.Singleton.StartHost();
                SelectTeams();
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
        /*
        //new
        if (!IsHost && tickTimer > 1.0f / (float)ClientInputTickRate.Value)
        {
            tickTimer = 0;
            SendJoystickServerRpc(GetJoyStickInput(), NetworkManager.Singleton.LocalClientId);
            return;
        }
        else if (IsHost && tickTimer > 1.0f / (float)ServerTickRate.Value)
        {
            tickTimer = 0;
            for (int i = 0; i < playerPosRPCData.Count; i++)
            {
                playerPosRPCData[i] = new PlayerPosData()
                {
                    id = playerPosRPCData[i].id,
                    pos = clients[i].transform.position
                };
            }
            SendPosClientRpc(playerPosRPCData.ToArray());
            SendJoystickServerRpc(GetJoyStickInput(), NetworkManager.Singleton.LocalClientId);
            return;
        }
        tickTimer += Time.deltaTime;


        //old
        for (int i = 0; i < clients.Count; i++)
        {
            CheckAllPlayerInputs(clients[i]);
            clients[i].GetTracker().UpdatePlayerPositions(host.GetTracker().GetCamera(), host.GetTracker().GetRightHand(), host.GetTracker().GetLeftHand(), host.GetTracker().GetForwardRoot(), al.GetClassStats(host.GetCurrentClass()).trackingScale);
        }
        */
        CheckAllPlayerInputs(host);
        
    }

    void SelectTeams()
    {
        //Random
        int teamInt1 = Random.Range(0, 7);
        int teamInt2 = Random.Range(0, 7);
        while (teamInt2 != teamInt1 && teamInt2 != teamInt1 - 1 && teamInt2 != teamInt1 + 1 && (team2 == 0 && teamInt1 == 5) && (teamInt2 == 5 && teamInt1 == 0) && (teamInt1 == 1 && teamInt2 == 6))
        {
            teamInt2 = Random.Range(0, 7);
            if (teamInt1 == 6 && teamInt2 == 7)
            {
                break;
            }
            if (teamInt1 == 7 && teamInt2 == 6)
            {
                break;
            }
        }
        team1 = (TeamList)teamInt1;
        team2 = (TeamList)teamInt2;
        Debug.Log("Teams Selected: " + team1 + ", " + team2);
    }

    void CheckAllPlayerInputs(Player player)
    {
        if (player != null)
        {
            if (player.GetTracker().GetTriggerR() == PlayerTracker.ButtonState.started || player.GetTracker().GetTriggerR() == PlayerTracker.ButtonState.on)
            {
                host.GetCurrentGun().Fire();
                for (int i = 0; i < clients.Count; i++)
                {
                    clients[i].GetCurrentGun().Fire();
                }
            }
            host.GetTracker().MovePlayer(host.GetTracker().GetMoveAxis());
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].GetTracker().MovePlayer(host.GetTracker().GetMoveAxis());
            }
        }
    }

    //Exploit: Hit needs to be parsed to ensure extreme angles aren't achievable.
    public void SpawnProjectile(Player player)
    {
        GunProjectiles fp = al.SearchGuns(player.GetCurrentGun().GetNameKey());
        if (fp.firePrefab != null)
        {
            GameObject currentProjectile = GameObject.Instantiate(fp.firePrefab);
            currentProjectile.transform.parent = particleList;
            Vector3 fireAngle = CalculateFireAngle(player);
            currentProjectile.GetComponent<Projectile>().SetProjectile(player.GetTracker().GetRightHand().position, fireAngle, player.GetCurrentGun().SearchStats(ChangableWeaponStats.bulletSpeed), player.GetTeamLayer(), CalculcateFirePosition(fireAngle, player));
        }
    }

    //Crosshair doesn't recalculate if it doesn't collide with a wall, fix it.
    public Vector3 CalculateFireAngle(Player player)
    {
        RaycastHit hit;
        Vector3 startCast = player.GetTracker().GetCamera().position + (player.GetTracker().GetCamera().forward * SPHERESIZE);
        Vector3 finalAngle = Vector3.one;

        LayerMask layermask = GetIgnoreTeamAndVRLayerMask(player);

        Transform rHand = player.GetTracker().GetRightHand();
        Vector3 firePoint = rHand.position; //+ rHand.TransformPoint(al.SearchGuns(player.GetCurrentGun().GetNameKey()).firepoint);
        Vector3 fpForward = rHand.forward;

        if (Physics.SphereCast(startCast, SPHERESIZE, player.GetTracker().GetCamera().forward, out hit, MAXSPHERECASTDISTANCE, layermask))
        {
            finalAngle = ((startCast + (player.GetTracker().GetCamera().forward * hit.distance)) - firePoint);
        }
        else
        {
            finalAngle = player.GetTracker().GetCamera().forward;
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

    public Vector3 CalculcateFirePosition(Vector3 fireAngle, Player player)
    {
        RaycastHit hit;
        Transform rHand = player.GetTracker().GetRightHand();
        Vector3 firePoint = rHand.position; // + rHand.TransformPoint(al.SearchGuns(player.GetCurrentGun().GetNameKey()).firepoint);
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

    public bool IsPlayerHost(Player player)
    {
        if (player == host)
        {
            return true;
        }
        return false;
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

    public struct PlayerNetworkDataServer : INetworkSerializable
    {
        public int playerNumber;

        //Player
        public Vector3 positionWorld;
        public Vector3 velocity;

        //Headset
        public Vector3 headsetPosWorld;
        public Quaternion headsetRotWorld;

        //Right Hand
        public Vector3 rHandPosWorld;
        public Quaternion rHandRotWorld;

        //Left Hand
        public Vector3 lHandPosWorld;
        public Quaternion lHandRotWorld;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref playerNumber);
            serializer.SerializeValue(ref positionWorld);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref headsetPosWorld);
            serializer.SerializeValue(ref headsetRotWorld);
            serializer.SerializeValue(ref rHandPosWorld);
            serializer.SerializeValue(ref rHandRotWorld);
            serializer.SerializeValue(ref lHandPosWorld);
            serializer.SerializeValue(ref lHandRotWorld);
        }
    }

    public struct PlayerNetworkDataClient : INetworkSerializable
    {
        //Headset
        public Vector3 headsetPosLocal;
        public Quaternion headsetRotLocal;

        //Right Hand
        public Vector3 rHandPosLocal;
        public Quaternion rHandRotLocal;

        //Left Hand
        public Vector3 lHandPosLocal;
        public Quaternion lHandRotLocal;

        //Controls
        public Vector2 rightJoystick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref headsetPosLocal);
            serializer.SerializeValue(ref headsetRotLocal);
            serializer.SerializeValue(ref rHandPosLocal);
            serializer.SerializeValue(ref rHandRotLocal);
            serializer.SerializeValue(ref lHandPosLocal);
            serializer.SerializeValue(ref lHandRotLocal);
            serializer.SerializeValue(ref rightJoystick);
        }
    }

    public void SpawnNewPlayerHost(ulong id)
    {
        if (!IsHost) { return; }
        Debug.Log("Host Player Spaned");
        GameObject client = GameObject.Instantiate(clientPrefab, Vector3.zero, Quaternion.identity);
        client.GetComponent<NetworkObject>().SpawnWithOwnership(id);
        Player clientPlayer = client.GetComponent<Player>();
        host = clientPlayer;
        clientPlayer.SetPlayerID(id);
    }

    /// <summary>
    /// "Client" also refers to Host Client.
    /// </summary>
    /// <param name="player"></param>
    public void AssignNewPlayerClient(Player player)
    {
        //Player lists are always sorted by ID to prevent searching in RPC
        clients.Add(player);
        clients.Sort((p1, p2) => p1.OwnerClientId.CompareTo(p2.OwnerClientId));
        playerPosRPCData.Add(
            new PlayerPosData()
            {
                id = player.OwnerClientId,
                pos = player.transform.position
            });
        playerPosRPCData.Sort((p1, p2) => p1.id.CompareTo(p2.id));

        if (!IsHost) { return; }

        //Client Object Spawning
        bool team = clients.Count % 2 != 0;
        Vector3 spawnPos;
        if (team)
        {
            spawnPos = team1Spawns.GetChild(team1SpawnIndex).position;
            team1SpawnIndex = (team1SpawnIndex + 1) % team1Spawns.childCount;
        }
        else
        {
            spawnPos = team2Spawns.GetChild(team2SpawnIndex).position;
            team2SpawnIndex = (team2SpawnIndex + 1) % team2Spawns.childCount;
        }

        if (team)
        {
            player.SetTeam(Team.team1);
        }
        else
        {
            player.SetTeam(Team.team2);
        }

        player.transform.position = spawnPos;

        TeamList debugList = TeamList.gray;
        switch (player.GetTeam())
        {
            case Team.team1:
                debugList = GetTeam1();
                break;
            case Team.team2:
                debugList = GetTeam2();
                break;
            default:
                break;
        }
        Debug.Log("New Player Joined (#" + clients.Count + "), Team " + debugList);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendJoystickServerRpc(Vector2 joystick, ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].OwnerClientId == id)
            {
                //players[i].UpdateJoystick(joystick);
                return;
            }
        }
    }

    /// <summary>
    /// Client Data needs to be sorted by ID
    /// </summary>
    /// <param name="data"></param>
    [ClientRpc]
    private void SendPosClientRpc(PlayerPosData[] data)
    {
        if (data.Length != clients.Count) { return; }
        for (int i = 0; i < clients.Count; i++)
        {
            //Check run incase of player disconnect+reconnect inside same tick.
            if (clients[i].OwnerClientId == data[i].id)
            {
                clients[i].GetTracker().SetNewClientPosition(data[i].pos);
            }
        }
    }

    Vector2 GetJoyStickInput()
    {
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

    }

    public void UpdateTickrates(int server, int client)
    {
        ServerTickRate.Value = server;
        ClientInputTickRate.Value = client;
    }

    void ServerStarted()
    {
        serverStarted = true;
    }

    public bool GetServerStatus()
    {
        return serverStarted;
    }
}

public struct PlayerPosData : INetworkSerializable
{
    public ulong id;
    public Vector3 headsetPos;
    public Vector3 rHandPos;
    public Vector3 lHandPos;
    public Vector3 pos;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref headsetPos);
        serializer.SerializeValue(ref rHandPos);
        serializer.SerializeValue(ref lHandPos);
        serializer.SerializeValue(ref pos);
    }
}

