using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Windows;

public class GlobalManager : NetworkBehaviour
{
    [Header("Server Settings")]
    List<TeamInfo> teams = new List<TeamInfo>();
    List<PlayerDataSentToClient> playerPosRPCData = new List<PlayerDataSentToClient>();
    [SerializeField] NetworkVariable<int> ServerTickRate = new NetworkVariable<int>(10);
    [SerializeField] NetworkManager m_NetworkManager;

    [Header("Lists")]
    [SerializeField] GameObject clientPrefab;
    List<Player> clients = new List<Player>();
    [SerializeField] List<Transform> teamSpawns;
    [SerializeField] List<Transform> teamGeometry;
    [SerializeField] List<Wire> teamWires;

    //Ect
    AllStats al;
    float tickTimer;
    bool serverStarted;
    GenericGamemode currentGamemode;

    //Const
    const ulong botID = 64646464646464;

    private void Start()
    {        //Pinging
        if (m_NetworkManager != null)
        {
            m_NetworkManager.ConnectionApprovalCallback = ApprovalCheck;
        }
        NetworkManager.Singleton.OnServerStarted += ServerStarted;
        al = GetComponent<AllStats>();
        currentGamemode = GetComponent<GenericGamemode>();

        //Settings
        NetworkManager.GetComponent<UnityTransport>().ConnectionData.Port = (ushort)PlayerPrefs.GetInt("ServerPort");

        //Local Host, Server Relay Host, Client Connect
        switch (PlayerPrefs.GetInt("LoadMapMode"))
        {
            case 0:
                NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
                NetworkManager.Singleton.StartHost();
                currentGamemode.SetTeams();
                Debug.Log("Started Host");
                break;
            case 1:
                NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
                NetworkManager.Singleton.StartHost();
                currentGamemode.SetTeams();
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

        if (PlayerPrefs.GetInt("SpawnBotsInEmpty") > 0)
        {
            for (int i = 0; i < PlayerPrefs.GetInt("ServerMaxPlayers"); i++)
            {
                //Probably a bad idea for 24/7 servers, though what's a player gonna gain out of controlling bots?
                SpawnNewPlayerHostServerRpc(botID + (uint)i);
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < clients.Count; i++)
        {
            //Client Networking
            if (clients[i].IsOwner)
            {
                SendJoystickServerRpc(clients[i].GetTracker().GetPlayerNetworkData(), clients[i].GetPlayerID());
            }

            //Game Logic
            if (clients[i].GetTracker().GetTriggerR())
            {
                clients[i].GetCurrentGun().Fire();
            }
        }
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < clients.Count; i++)
        {
            PlayerDataSentToServer inputs = clients[i].GetTracker().GetPlayerNetworkData();
            clients[i].GetController().MovePlayer(inputs.rightJoystick, inputs.jump,inputs.crouch);
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
                for (int i = 0; i < clients.Count; i++)
                {
                    playerPosRPCData[i] = clients[i].GetTracker().GetPlayerPosData();
                }
                SendPosClientRpc(playerPosRPCData.ToArray());
            }
        }
        tickTimer += Time.deltaTime;
    }


    public void ModifyTeamsAcrossServer()
    {
        if (IsHost)
        {
            UpdateClientMapDataClientRpc(teams.ToArray(),null);
        }
        for (int e = 0; e < teams.Count; e++)
        {
            float team1Final = (float)teams[e].teamColor + 1;
            Renderer[] meshes = teamGeometry[teams[e].spawns].GetComponentsInChildren<Renderer>();
            for (int i = 0; i < meshes.Length; i++)
            {
                Material[] mats = meshes[i].sharedMaterials;
                for (int r = 0; r < mats.Length; r++)
                {
                    mats[r].SetFloat("_Team_1", team1Final);
                }
            }
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].UpdateTeamColor();
            }
            if (teamWires[teams[e].spawns] != null)
            {
                teamWires[teams[e].spawns].SetTeam(teams[e].teamColor);
            }
        }
    }

    public List<Transform> GetTeamSpawns()
    {
        return teamSpawns;
    }

    public LayerMask GetIgnoreTeamAndVRLayerMask(Player player)
    {
        LayerMask mask;
        switch (player.GetTeam())
        {
            case TeamList.orange:
                mask = 1 << LayerMask.NameToLayer("OrangeTeam");
                break;
            case TeamList.yellow:
                mask = 1 << LayerMask.NameToLayer("YellowTeam");
                break;
            case TeamList.green:
                mask = 1 << LayerMask.NameToLayer("GreenTeam");
                break;
            case TeamList.lightBlue:
                mask = 1 << LayerMask.NameToLayer("LightBlueTeam");
                break;
            case TeamList.blue:
                mask = 1 << LayerMask.NameToLayer("BlueTeam");
                break;
            case TeamList.purple:
                mask = 1 << LayerMask.NameToLayer("PurpleTeam");
                break;
            case TeamList.beige:
                mask = 1 << LayerMask.NameToLayer("BeigeTeam");
                break;
            case TeamList.brown:
                mask = 1 << LayerMask.NameToLayer("BrownTeam");
                break;
            case TeamList.gray:
                mask = 1 << LayerMask.NameToLayer("GrayTeam");
                break;
            default:
                mask = 1 << LayerMask.NameToLayer("GrayTeam");
                break;
        }
        mask = mask | 805306368; //VR Layermask
        mask = ~mask;
        return mask;
    }

    public List<Player> GetClients()
    {
        return clients;
    }

    public void AddNewTeam(TeamInfo newTeam)
    {
        teams.Add(newTeam);
        ModifyTeamsAcrossServer();
    }

    public void ClearTeams()
    {
        teams = new List<TeamInfo>();
    }

    public List<TeamInfo> GetTeams()
    {
        return teams;
    }

    public List<TeamList> GetTeamColors(bool minusGrayTeam)
    {
        List<TeamList> colors = new List<TeamList>();

        for (int i = 0; i < teams.Count; i++)
        {
            if (!(minusGrayTeam && teams[i].teamColor == TeamList.gray))
            {
                colors.Add(teams[i].teamColor);
            }
        }

        return colors;
    }

    public AllStats GetAllStats()
    {
        return al;
    }

    public Wire GetWire(TeamList team)
    {
        for (int e = 0; e < teams.Count; e++)
        {
            if(teams[e].teamColor == team)
            {
                return teamWires[teams[e].spawns];
            }
        }
        return null;
    }

    public void DisconnectClient(Player player)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i] == player)
            {
                Wire.WirePoint wireHeld = clients[i].GetWirePoint();
                if (wireHeld != null)
                {
                    RemoveClientWireClientRpc(player.GetPlayerID(), wireHeld.point);
                    player.RemoveHeldWire(wireHeld.point);
                }

                Destroy(clients[i].gameObject);
                clients.RemoveAt(i);
                playerPosRPCData = new List<PlayerDataSentToClient>();
                for (int e = 0; e < clients.Count; e++)
                {
                    playerPosRPCData.Add(new PlayerDataSentToClient());
                }
                return;
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

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Additional connection data defined by user code
        string payload = System.Text.Encoding.ASCII.GetString(request.Payload);

        if (payload[0] == 'P')
        {
            response.Approved = false;
            response.Reason = 'P' + clients.Count + 0x1c + PlayerPrefs.GetInt("ServerMaxPlayers") + 0x1c + PlayerPrefs.GetString("ServerName") + 0x1c + PlayerPrefs.GetInt("ServerMapName");
        }
        else
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = null;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendJoystickServerRpc(PlayerDataSentToServer serverData, ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id && !clients[i].IsOwner)
            {
                clients[i].GetTracker().ServerSyncPlayerInputs(serverData);
                return;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnNewPlayerHostServerRpc(ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            ulong finalId = clients[i].GetPlayerID();
            if (finalId == id && finalId < botID)
            {
                Debug.Log("Player " + id + " attempted to request more than one player object.");
                return;
            }
        }
        if (clients.Count >= PlayerPrefs.GetInt("ServerMaxPlayers"))
        {
            bool goodToGo = false;
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].GetPlayerID() >= botID)
                {
                    Debug.Log("Bot " + id + " removed from playerlist to make room for new player.");
                    DisconnectClient(clients[i]);
                    goodToGo = true;
                    break;
                }
            }
            if (!goodToGo)
            {
                Debug.Log("Player could not connect, server full.");
                return;
            }
        }
        Debug.Log("Player Spawned On Host");
        GameObject client = GameObject.Instantiate(clientPrefab, Vector3.zero, Quaternion.identity);
        client.GetComponent<NetworkObject>().SpawnWithOwnership(id);

        //Client Object Spawning
        TeamList debugList = currentGamemode.DecideWhichPlayerTeam();

        //Auto Team
        ClassList autoClass = ClassList.programmer;
        int random = Random.Range(0, 2);
        if (random == 0)
        {
            autoClass = ClassList.programmer;
        }
        else
        {
            autoClass = ClassList.fabricator;
        }

        Debug.Log("New Player Joined (#" + clients.Count + "), Team " + debugList);
        SendNewPlayerDataBackClientRpc(id, debugList, autoClass);
        RespawnPlayerClientRpc(id, debugList);
        PlayerInfoSentToClient[] data = new PlayerInfoSentToClient[clients.Count];
        for (int i = 0; i < clients.Count; i++)
        {
            data[i] = new PlayerInfoSentToClient
            {
                id = clients[i].GetPlayerID(),
                currentTeam = clients[i].GetTeam(),
                currentClass = clients[i].GetCurrentClass(),
            };
        }
        SendAllPlayerDataToNewPlayerClientRpc(data, id);

        List<Wire.WirePointData> wireData = new List<Wire.WirePointData>();
        for (int i = 0; i < teamWires.Count; i++)
        {
            if (teamWires[i] != null)
            {
                List<Wire.WirePointData> teamWData = teamWires[i].ConvertWiresToDataArray(i);
                for (int e = 0; e < teamWData.Count; e++)
                {
                    wireData.Add(teamWData[e]);
                }
            }
        }
        UpdateClientMapDataClientRpc(teams.ToArray(),wireData.ToArray());
    }

    /// <summary>
    /// Client Data needs to be sorted by ID
    /// </summary>
    /// <param name="data"></param>
    [ClientRpc]
    private void SendPosClientRpc(PlayerDataSentToClient[] data)
    {
        if (IsHost) { return; }

        playerPosRPCData = data.ToList<PlayerDataSentToClient>();
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
                        clients[e].GetTracker().ClientSyncPlayerInputs(playerPosRPCData[i]);
                    }
                }
            }

        }
    }

    [ClientRpc]
    void SendNewPlayerDataBackClientRpc(ulong id, TeamList team, ClassList autoClass)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].SetTeam(team);
                clients[i].SetClass(autoClass);
                return;
            }
        }
    }

    [ClientRpc]
    public void GiveClientWireClientRpc(ulong id, uint wireID, uint parentID, TeamList teamWireNeeded)
    {
        if (IsHost) { return; }
        Wire neededWire = null;
        for (int e = 0; e < teams.Count; e++)
        {
            if (teams[e].teamColor == teamWireNeeded)
            {
                neededWire = teamWires[teams[e].spawns];
                break;
            }
        }
        Debug.Log(neededWire);
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                Debug.Log("??????");
                clients[i].SetWirePoint(neededWire.CreateNewClientWire(wireID, parentID));
                return;
            }
        }
    }

    [ClientRpc]
    public void RemoveClientWireClientRpc(ulong id, Vector3 finalPos)
    {
        if (IsHost) { return; }
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].RemoveHeldWire(finalPos);
                return;
            }
        }
    }

    [ClientRpc]
    /// <summary>
    /// "Team" is required due to clients possibly being out of sync with player team.
    /// </summary>
    /// <param name="data"></param>
    public void RespawnPlayerClientRpc(ulong id, TeamList team)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                //Refresh Stats
                clients[i].ResetClassStats();

                //Spawning
                for (int e = 0; e < teams.Count; e++)
                {
                    if (teams[e].teamColor == team)
                    {
                        Vector3 spawnPos = teamSpawns[teams[e].spawns].GetChild(Random.Range(0, teamSpawns[teams[e].spawns].childCount)).position;
                        clients[i].GetTracker().ForceNewPosition(spawnPos);
                        Debug.Log("Player " + id + " respawned in " + team.ToString() + " spawn room");
                        return;
                    }
                }
            }
        }
    }


    [ClientRpc]
    void SendAllPlayerDataToNewPlayerClientRpc(PlayerInfoSentToClient[] data, ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            //This looks dumb but its to make sure redundant data isn't sent to all clients
            if (clients[i].GetPlayerID() == id)
            {
                for (int e = 0; e < clients.Count; e++)
                {
                    for (int j = 0; j < data.Length; j++)
                    {
                        if (clients[e].GetPlayerID() == data[j].id)
                        {
                            clients[e].SetClass(data[j].currentClass);
                            clients[e].SetTeam(data[j].currentTeam);
                        }
                    }
                }
                return;
            }
        }
    }

    [ClientRpc]
    private void UpdateClientMapDataClientRpc(TeamInfo[] a, Wire.WirePointData[] b)
    {
        if (IsHost) { return; }
        ClearTeams();
        for (int i = 0; i < a.Length; i++)
        {
            AddNewTeam(a[i]);
        }
        if (b != null)
        {
            for (int i = 0; i < b.Length; i++)
            {
                teamWires[b[i].teamNum].CreateNewClientWire(b[i]);
            }
        }
    }

    [ClientRpc]
    public void SpawnProjectileClientRpc(ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].GetCurrentGun().SpawnProjectile(clients[i]);
            }
        }
    }

    [ClientRpc]
    public void PlayerTookDamageClientRpc(ulong id, int currentHealth, ulong idOfKiller)
    {
        if (currentHealth <= 0)
        {
            Debug.Log("Player " + id + " was killed (" + currentHealth + " HP) by Player" + idOfKiller);
        }
        else
        {
            Debug.Log("Player " + id + " took damage (" + currentHealth + " HP) by Player" + idOfKiller);
        }
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].SetHealth(currentHealth);
            }
        }
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
public struct TeamInfo : INetworkSerializable
{
    public TeamList teamColor;
    public int spawns;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref teamColor);
        serializer.SerializeValue(ref spawns);
    }
}

[System.Serializable]
public struct PlayerInfoSentToClient : INetworkSerializable
{
    public ulong id;
    public ClassList currentClass;
    public TeamList currentTeam;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref currentClass);
        serializer.SerializeValue(ref currentTeam);
    }
}

[System.Serializable]
public struct PlayerDataSentToServer : INetworkSerializable
{
    public float predictionTime;
    public Vector3 headsetPos;
    public Quaternion headsetRot;
    public Vector3 rHandPos;
    public Quaternion rHandRot;
    public Vector3 lHandPos;
    public Quaternion lHandRot;

    //Controls
    public Vector2 rightJoystick;
    public bool jump;
    public bool shoot;
    public bool crouch;


    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref predictionTime);
        serializer.SerializeValue(ref headsetPos);
        serializer.SerializeValue(ref headsetRot);
        serializer.SerializeValue(ref rHandPos);
        serializer.SerializeValue(ref rHandRot);
        serializer.SerializeValue(ref lHandPos);
        serializer.SerializeValue(ref lHandRot);
        serializer.SerializeValue(ref rightJoystick);
        serializer.SerializeValue(ref jump);
        serializer.SerializeValue(ref shoot);
        serializer.SerializeValue(ref crouch);
    }
}