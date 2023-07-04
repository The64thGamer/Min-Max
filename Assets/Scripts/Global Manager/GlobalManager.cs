using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;

public class GlobalManager : NetworkBehaviour
{
    [Header("Server Settings")]
    List<TeamInfo> teams = new List<TeamInfo>();
    List<PlayerDataSentToClient> playerPosRPCData = new List<PlayerDataSentToClient>();
    [SerializeField] NetworkVariable<int> ServerTickRate = new NetworkVariable<int>(10);

    [Header("Lists")]
    [SerializeField] Cosmetics co;
    [SerializeField] GameObject clientPrefab;
    List<Player> clients = new List<Player>();
    [SerializeField] List<Transform> teamSpawns;
    [SerializeField] List<Transform> teamGeometry;
    [SerializeField] List<Wire> teamWires;
    [SerializeField] List<Transform> teamBlocks;
    List<int> damageHashes = new List<int>();

    //Ect
    AllStats al;
    AudioSource au;
    NetworkManager m_NetworkManager;
    float tickTimer;
    bool serverStarted;
    GenericGamemode currentGamemode;

    //Const
    const ulong botID = 64646464646464;

    private void Start()
    {
        //Pinging
        m_NetworkManager = GameObject.Find("Transport").GetComponent<NetworkManager>();
        m_NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes("C");
        m_NetworkManager.ConnectionApprovalCallback = ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += ServerStarted;
        NetworkManager.Singleton.OnClientDisconnectCallback += Disconnect;
        NetworkManager.Singleton.OnServerStopped += Disconnect;
        al = GetComponent<AllStats>();
        au = GetComponent<AudioSource>();
        currentGamemode = GetComponent<GenericGamemode>();

        //Settings
        m_NetworkManager.GetComponent<UnityTransport>().ConnectionData.Port = (ushort)PlayerPrefs.GetInt("ServerPort");

        switch (PlayerPrefs.GetInt("LoadMapMode"))
        {
            case 0:
                //Start Local
                NetworkManager.Singleton.StartHost();
                currentGamemode.SetTeams();
                if (PlayerPrefs.GetInt("SpawnBotsInEmpty") > 0)
                {
                    for (int i = 0; i < PlayerPrefs.GetInt("ServerMaxPlayers"); i++)
                    {
                        //Probably a bad idea for 24/7 servers, though what's a player gonna gain out of controlling bots?
                        SpawnNewPlayerHostServerRpc(botID + (uint)i);
                    }
                }
                Debug.Log("Started Local Host");
                break;
            case 1:
                //Start Server
                StartServer();
                break;
            case 2:
                //Join Server
                JoinServer(PlayerPrefs.GetString("JoinCode"));
                break;
            case 3:
                //Join Local
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started Local Client");
                break;
            default:
                break;
        }


    }

    void Disconnect(ulong u)
    {
        m_NetworkManager.Shutdown();
        SceneManager.LoadScene("Startup");
    }
    public void Disconnect(bool u)
    {
        m_NetworkManager.Shutdown();
        SceneManager.LoadScene("Startup");
    }

    async void StartServer()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(PlayerPrefs.GetInt("ServerMaxPlayers") - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            m_NetworkManager.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            m_NetworkManager.StartHost();
            currentGamemode.SetTeams();
            if (PlayerPrefs.GetInt("SpawnBotsInEmpty") > 0)
            {
                for (int i = 0; i < PlayerPrefs.GetInt("ServerMaxPlayers"); i++)
                {
                    //Probably a bad idea for 24/7 servers, though what's a player gonna gain out of controlling bots?
                    SpawnNewPlayerHostServerRpc(botID + (uint)i);
                }
            }
            GUIUtility.systemCopyBuffer = joinCode;
            Debug.Log("Started Server Host, Code: " + joinCode);
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    async void JoinServer(string joinCode)
    {
        await UnityServices.InitializeAsync();
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception)
        {
            Debug.Log("Already Signed In");
        }
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");

            m_NetworkManager.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            m_NetworkManager.StartClient();
            Debug.Log("Started Server Client");
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
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

            PlayerDataSentToServer inputs = clients[i].GetTracker().GetPlayerNetworkData();
            clients[i].GetController().MovePlayer(inputs.rightJoystick, inputs.jump, inputs.crouch);
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

        if (damageHashes.Count > 0)
        {
            damageHashes = new List<int>();
        }
    }


    public void ModifyTeamsAcrossServer()
    {
        if (IsHost)
        {
            UpdateClientMapDataClientRpc(teams.ToArray(), null);
        }
        for (int e = 0; e < teams.Count; e++)
        {
            float team1Final = (float)teams[e].teamColor + 1;
            Renderer[] meshes = teamGeometry[teams[e].spawns].GetComponentsInChildren<Renderer>();
            for (int i = 0; i < meshes.Length; i++)
            {
                Material[] mats = meshes[i].materials;
                for (int r = 0; r < mats.Length; r++)
                {
                    mats[r].SetFloat("_Team_1", team1Final);
                }
            }

            if (teamWires[teams[e].spawns] != null)
            {
                teamWires[teams[e].spawns].SetTeam(teams[e].teamColor);
            }
            if (teamBlocks[teams[e].spawns] != null)
            {
                int layer = 0;
                switch (teams[e].teamColor)
                {
                    case TeamList.orange:
                        layer = LayerMask.NameToLayer("OrangeTeam");
                        break;
                    case TeamList.yellow:
                        layer = LayerMask.NameToLayer("YellowTeam");
                        break;
                    case TeamList.green:
                        layer = LayerMask.NameToLayer("GreenTeam");
                        break;
                    case TeamList.lightBlue:
                        layer = LayerMask.NameToLayer("LightBlueTeam");
                        break;
                    case TeamList.blue:
                        layer = LayerMask.NameToLayer("BlueTeam");
                        break;
                    case TeamList.purple:
                        layer = LayerMask.NameToLayer("PurpleTeam");
                        break;
                    case TeamList.beige:
                        layer = LayerMask.NameToLayer("BeigeTeam");
                        break;
                    case TeamList.brown:
                        layer = LayerMask.NameToLayer("BrownTeam");
                        break;
                    case TeamList.gray:
                        layer = LayerMask.NameToLayer("GrayTeam");
                        break;
                    default:
                        break;
                }
                foreach (Transform child in teamBlocks[teams[e].spawns].transform)
                {
                    child.gameObject.layer = layer;
                }
            }
        }
        for (int i = 0; i < clients.Count; i++)
        {
            clients[i].UpdateTeamColor();
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

    public Cosmetics GetCosmetics()
    {
        return co;
    }

    public Wire GetWire(TeamList team)
    {
        for (int e = 0; e < teams.Count; e++)
        {
            if (teams[e].teamColor == team)
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

    public AudioSource GetGlobalAudioSource()
    {
        return au;
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
            Debug.Log("Player is Pinging Server, Sending Data");
            response.Approved = false;
            response.Reason = "P"
                + clients.Count + "😂"
                + PlayerPrefs.GetInt("ServerMaxPlayers") + "😂"
                + PlayerPrefs.GetString("ServerName") + "😂"
                + PlayerPrefs.GetInt("ServerMapName") + "😂"
                + m_NetworkManager.NetworkConfig.ProtocolVersion;
        }
        else if (payload[0] == 'C')
        {
            Debug.Log("Player is attempting to connect, Approved");
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = null;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }
        else
        {
            Debug.Log("Player is attempting to connect, Denied: Server was sent corrupted instructions");
            response.Approved = false;
            response.Reason = "E" + "Server was sent corrupted instructions";
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
        client.name = "Client #" + id;
        client.GetComponent<NetworkObject>().SpawnWithOwnership(id);

        //Client Object Spawning
        TeamList decidedTeam = currentGamemode.DecideWhichPlayerTeam();

        //Auto Team
        ClassList autoClass = ClassList.programmer;
        string autoGun = "W.I.P.";
        int random = UnityEngine.Random.Range(0, 3);
        switch (random)
        {
            case 0:
                autoClass = ClassList.programmer;
                autoGun = "W.I.P.";
                break;
            case 1:
                autoClass = ClassList.computer;
                autoGun = "W.I.P.";
                break;
            case 2:
                autoClass = ClassList.fabricator;
                autoGun = "O-S F-O";
                break;
            default:
                break;
        }


        Debug.Log("New Player Joined (#" + clients.Count + "), Team " + decidedTeam);

        //Random cosmetics
        List<int> cos = new List<int>();
        switch (autoClass)
        {
            case ClassList.programmer:
                int randoP = UnityEngine.Random.Range(0, 4);
                switch (randoP)
                {
                    case 0:
                        cos.Add(5);
                        break;
                    default:
                        break;
                }
                break;
            case ClassList.fabricator:
                int randoF = UnityEngine.Random.Range(0, 4);
                switch (randoF)
                {
                    case 0:
                        cos.Add(0);
                        cos.Add(1);
                        break;
                    case 1:
                        cos.Add(0);
                        cos.Add(6);
                        break;
                    case 2:
                        cos.Add(1);
                        cos.Add(5);
                        break;
                    case 3:
                        cos.Add(0);
                        cos.Add(5);
                        break;
                    default:
                        break;
                }
                break;
            case ClassList.computer:
                cos.Add(0);
                cos.Add(1);
                cos.Add(2);
                break;
            default:
                break;
        }

        PlayerInfoSentToClient pdstc = new PlayerInfoSentToClient
        {
            id = id,
            currentClass = autoClass,
            currentTeam = decidedTeam,
            cosmetics = cos.ToArray(),
            gunName = autoGun,
        };
        AssignPlayerClassAndTeamClientRpc(pdstc);
        RespawnPlayerClientRpc(id, decidedTeam);

        PlayerInfoSentToClient[] data = new PlayerInfoSentToClient[clients.Count];
        for (int i = 0; i < clients.Count; i++)
        {
            data[i] = new PlayerInfoSentToClient
            {
                id = clients[i].GetPlayerID(),
                currentTeam = clients[i].GetTeam(),
                currentClass = clients[i].GetCurrentClass(),
                cosmetics = clients[i].GetCosmeticInts(),
                gunName = clients[i].GetCurrentGun().name,
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
        UpdateClientMapDataClientRpc(teams.ToArray(), wireData.ToArray());
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
    public void RemoveAllWiresClientRpc()
    {
        for (int i = 0; i < clients.Count; i++)
        {
            clients[i].RemoveHeldWire(Vector3.zero);
        }
        for (int i = 0; i < teamWires.Count; i++)
        {
            if (teamWires[i] != null)
            {
                teamWires[i].RemoveAllWires();
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

                if (IsHost && clients[i].GetWirePoint() != null)
                {
                    RemoveClientWireClientRpc(clients[i].GetPlayerID(), clients[i].GetWirePoint().point);
                }

                //Spawning
                for (int e = 0; e < teams.Count; e++)
                {
                    if (teams[e].teamColor == team)
                    {
                        Vector3 spawnPos = teamSpawns[teams[e].spawns].GetChild(UnityEngine.Random.Range(0, teamSpawns[teams[e].spawns].childCount)).position;
                        clients[i].GetTracker().ForceNewPosition(spawnPos);
                        Debug.Log("Player " + id + " respawned in " + team.ToString() + " spawn room");
                        return;
                    }
                }
            }
        }
    }

    [ClientRpc]
    public void AssignPlayerClassAndTeamClientRpc(PlayerInfoSentToClient data)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == data.id)
            {
                clients[i].SetClass(data.currentClass, data.cosmetics);
                clients[i].SetTeam(data.currentTeam);
                clients[i].SetGun(al.SearchGuns(data.gunName));
                return;
            }
        }
    }

    [ClientRpc]
    public void SetPlayerTeamClientRpc(ulong id, TeamList team)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].SetTeam(team);
                return;
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
                            clients[e].SetClass(data[j].currentClass, data[j].cosmetics);
                            clients[e].SetTeam(data[j].currentTeam);
                            clients[e].SetGun(al.SearchGuns(data[j].gunName));
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
    public void PlayerTookDamageClientRpc(ulong id, int currentHealth, ulong idOfKiller, int idHash)
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
            if (clients[i].GetPlayerID() == idOfKiller && clients[i].IsOwner && id != idOfKiller)
            {
                if (currentHealth <= 0)
                {
                    //Ensures a gun firing 10 bullets doesn't play 10 hitsounds
                    bool isntDuplicate = true;
                    for (int e = 0; e < damageHashes.Count; e++)
                    {
                        if (damageHashes[e] == idHash)
                        {
                            isntDuplicate = false;
                        }
                    }
                    if (isntDuplicate)
                    {
                        au.PlayOneShot((AudioClip)Resources.Load("Sounds/Damage/killsound", typeof(AudioClip)));
                        damageHashes.Add(idHash);
                    }
                }
                else
                {
                    au.PlayOneShot((AudioClip)Resources.Load("Sounds/Damage/hitsound", typeof(AudioClip)));
                }
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
    public string gunName;
    public int[] cosmetics;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref currentClass);
        serializer.SerializeValue(ref currentTeam);
        serializer.SerializeValue(ref cosmetics);
        serializer.SerializeValue(ref gunName);
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