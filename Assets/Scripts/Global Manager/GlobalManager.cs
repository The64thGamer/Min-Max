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

public class GlobalManager : NetworkBehaviour
{
    [Header("Server Settings")]
    List<TeamInfo> teams = new List<TeamInfo>();
    List<PlayerDataSentToClient> playerPosRPCData = new List<PlayerDataSentToClient>();
    [SerializeField] NetworkVariable<int> ServerTickRate = new NetworkVariable<int>(10);

    [Header("Lists")]
    [SerializeField] Cosmetics co;
    [SerializeField] Achievements achievments;
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
    List<Vector3> matchFocalPoint;
    float timeStartedPlaying;

    //Const
    const ulong botID = 64646464646464;

    private void Start()
    {
        //Pinging
        m_NetworkManager = GameObject.Find("Transport").GetComponent<NetworkManager>();
        m_NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes("C");
        m_NetworkManager.ConnectionApprovalCallback = ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += ServerStarted;
        NetworkManager.Singleton.OnClientDisconnectCallback += PlayerDisconnected;
        al = GetComponent<AllStats>();
        au = GetComponent<AudioSource>();
        currentGamemode = GetComponent<GenericGamemode>();
        timeStartedPlaying = Time.time;

        //Settings
        m_NetworkManager.GetComponent<UnityTransport>().ConnectionData.Port = (ushort)PlayerPrefs.GetInt("ServerPort");

        switch (PlayerPrefs.GetString("Connection Setting"))
        {
            case "Create LAN Server":
                NetworkManager.Singleton.StartHost();
                currentGamemode.SetTeams();
                if (PlayerPrefs.GetInt("SpawnBotsInEmpty") > 0)
                {
                    for (int i = 0; i < PlayerPrefs.GetInt("ServerMaxPlayers"); i++)
                    {
                        //Probably a bad idea for 24/7 servers, though what's a player gonna gain out of controlling bots?
                        SpawnPlayer(botID + (uint)i, "Bot # " + i, UnityEngine.Random.Range(3, 6), new int[0]);
                    }
                }
                Debug.Log("Started Local Host");
                break;
            case "Create Online Server":
                StartServer();
                break;
            case "Join Online Server":
                JoinServer(PlayerPrefs.GetString("JoinCode"));
                break;
            case "Join LAN Server":
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started Local Client");
                break;
            default:
                Debug.LogError("Unknown Server Setting");
                break;
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("Runtime: " + Mathf.Max(0, Time.time - timeStartedPlaying) + " sec");
        achievments.AddToValue("Achievement: Total Match Runtime", Mathf.Max(0, Time.time - timeStartedPlaying));
        timeStartedPlaying = Time.time;
        achievments.SaveAchievements();
    }

    public void DisconnectToTitleScreen(bool unused)
    {
        if (IsHost && !serverStarted)
        {
            return;
        }
        Debug.Log("Disconnecting to Title Screen");
        Debug.Log("Runtime: " + Mathf.Max(0, Time.time - timeStartedPlaying) + " sec");
        achievments.AddToValue("Achievement: Total Match Runtime", Mathf.Max(0, Time.time - timeStartedPlaying));
        timeStartedPlaying = Time.time;
        achievments.SaveAchievements();
        m_NetworkManager.Shutdown();
        if (PlayerPrefs.GetInt("IsVREnabled") == 1)
        {
            SceneManager.LoadScene("VR Title Screen");
        }
        else
        {
            SceneManager.LoadScene("Title Screen");
        }
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
                    SpawnPlayer(botID + (uint)i, "Bot # " + i, UnityEngine.Random.Range(3, 6), new int[0]);
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
        clients.RemoveAll(item => item == null);
        for (int i = 0; i < clients.Count; i++)
        {
            //Client Networking
            if (clients[i].IsOwner)
            {
                SendJoystickServerRpc(clients[i].GetTracker().GetPlayerNetworkData());
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

    public Vector3 GetMatchFocalPoint(TeamList team)
    {
        for (int e = 0; e < teams.Count; e++)
        {
            if (teams[e].teamColor == team)
            {
                return matchFocalPoint[e];
            }
        }
        return Vector3.zero;
    }

    public void UpdateMatchFocalPoint(TeamList team)
    {
        for (int e = 0; e < teams.Count; e++)
        {
            if (teams[e].teamColor == team)
            {
                matchFocalPoint[e] = currentGamemode.GetCurrentMatchFocalPoint(e - 1);
                return;
            }
        }
    }

    public List<Transform> GetTeamSpawns()
    {
        return teamSpawns;
    }

    public List<Player> GetClients()
    {
        return clients;
    }

    public void AddNewTeam(TeamInfo newTeam)
    {
        teams.Add(newTeam);
        matchFocalPoint.Add(Vector3.zero);
        ModifyTeamsAcrossServer();
    }

    public void ClearTeams()
    {
        teams = new List<TeamInfo>();
        matchFocalPoint = new List<Vector3>();
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

    public Achievements GetAchievements()
    {
        return achievments;
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
        Debug.Log("Server Started");
        serverStarted = true;
    }

    public void RespawnPlayer(ulong id, TeamList team, bool instant)
    {
        if (IsHost)
        {
            float timer = 10;

            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].GetPlayerID() == id)
                {
                    if (!instant)
                    {
                        timer = currentGamemode.RequestPlayerRespawnTimer(i);
                    }
                    else
                    {
                        timer = 0;
                    }
                    if (clients[i].GetWirePoint() != null)
                    {
                        Debug.Log("RemoveClientWire Sent to Clients");
                        RemoveClientWireClientRpc(clients[i].GetPlayerID(), clients[i].GetWirePoint().point, false);
                    }
                }
            }

            //Spawning
            Vector3 spawnPos = Vector3.zero;
            for (int e = 0; e < teams.Count; e++)
            {
                if (teams[e].teamColor == team)
                {
                    spawnPos = teamSpawns[teams[e].spawns].GetChild(UnityEngine.Random.Range(0, teamSpawns[teams[e].spawns].childCount)).position;
                    break;
                }
            }

            Debug.Log("RespawnPlayer Sent to Clients");
            RespawnPlayerClientRpc(id, team, spawnPos, timer);
        }
    }

    public bool GetServerStatus()
    {
        return serverStarted;
    }

    void PlayerDisconnected(ulong id)
    {
        if (IsHost)
        {
            KickPlayerClientRpc(id, "Player " + id + " Left");
        }
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

    string AutoGun(ClassList currentClassSelected)
    {
        //Auto Guns
        string autoGun = "W.I.P.";
        switch (currentClassSelected)
        {
            case ClassList.programmer:
                autoGun = "W.I.P.";
                break;
            case ClassList.computer:
                autoGun = "W.I.P.";
                break;
            case ClassList.fabricator:
                autoGun = "O-S F-O";
                break;
            default:
                break;
        }
        return autoGun;
    }

    [ServerRpc(RequireOwnership = false)]
    void SendJoystickServerRpc(PlayerDataSentToServer serverData, ServerRpcParams serverRpcParams = default)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == serverRpcParams.Receive.SenderClientId && !clients[i].IsOwner)
            {
                clients[i].GetTracker().ServerSyncPlayerInputs(serverData);
                clients[i].GetController().RecalculateServerPosition(serverData);
                return;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnNewPlayerHostServerRpc(string playerName, int initialClass, int[] cosmetics, ServerRpcParams serverRpcParams = default)
    {
        SpawnPlayer(serverRpcParams.Receive.SenderClientId, playerName, initialClass, cosmetics);
    }

    void SpawnPlayer(ulong id, string playerName, int initialClass, int[] cosmetics)
    {
        initialClass = Mathf.Clamp(initialClass, 3, 5);  //Can only pick current classes

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
                    KickPlayerClientRpc(clients[i].GetPlayerID(), "Bot " + clients[i].GetPlayerID() + " removed from player list to make room for new player)");
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
        Debug.Log("Client name: " + playerName);
        GameObject client = GameObject.Instantiate(clientPrefab, Vector3.zero, Quaternion.identity);
        client.GetComponent<NetworkObject>().SpawnWithOwnership(id);

        //Client Object Spawning
        TeamList decidedTeam = currentGamemode.DecideWhichPlayerTeam();

        //Auto Guns
        string autoGun = AutoGun((ClassList)initialClass);

        //Random cosmetics
        if (id >= botID)
        {
            List<int> cos = new List<int>();
            int rando = UnityEngine.Random.Range(0, 6);

            switch ((ClassList)initialClass)
            {
                case ClassList.programmer:
                    switch (rando)
                    {
                        case 0:
                            cos.Add(5);
                            break;
                        case 1:
                            cos.Add(6);
                            cos.Add(7);
                            break;
                        case 2:
                            cos.Add(7);
                            break;
                        default:
                            break;
                    }
                    break;
                case ClassList.fabricator:
                    switch (rando)
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
                    switch (rando)
                    {
                        case 0:
                            cos.Add(3);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
            cosmetics = cos.ToArray();
        }

        PlayerInfoSentToClient pdstc = new PlayerInfoSentToClient
        {
            id = id,
            currentClass = (ClassList)initialClass,
            currentTeam = decidedTeam,
            cosmetics = cosmetics,
            gunName = autoGun,
            playerName = playerName,
        };
        AssignPlayerClassAndTeamClientRpc(pdstc);
        RespawnPlayer(id, decidedTeam, true);

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
                playerName = clients[i].GetPlayerName(),
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

        Debug.Log("New Player Joined (#" + clients.Count + "), Team " + decidedTeam);
    }


    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerStatusOnSwitchedClassesServerRpc(bool yesChangeClass, int initialClass, int[] cosmetics, ServerRpcParams serverRpcParams = default)
    {

        initialClass = Mathf.Clamp(initialClass, 3, 5);  //Can only pick current classes

        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == serverRpcParams.Receive.SenderClientId)
            {
                if (!yesChangeClass)
                {
                    clients[i].SendPlayerSwitchClassStatusRejection();
                    return;
                }
                else
                {
                    if(clients[i].SendPlayerSwitchClassStatus())
                    {
                        PlayerInfoSentToClient pdstc = new PlayerInfoSentToClient
                        {
                            id = serverRpcParams.Receive.SenderClientId,
                            currentClass = (ClassList)initialClass,
                            currentTeam = clients[i].GetTeam(),
                            cosmetics = cosmetics,
                            gunName = AutoGun((ClassList)initialClass),
                            playerName = clients[i].GetPlayerName(),
                        };
                        AssignPlayerClassAndTeamClientRpc(pdstc);
                    }
                }
                return;
            }
        }
    }

    [ClientRpc]
    public void RequestPlayerStatusOnSwitchedClassesClientRpc(ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                if (clients[i].IsOwner && clients[i].GetPlayerID() < botID)
                {
                    if(Convert.ToBoolean(PlayerPrefs.GetInt("SendToServerSwitchClass")))
                    {
                        PlayerPrefs.SetInt("SendToServerSwitchClass", 0);
                        List<int> newCosInts = new List<int>();

                        for (int e = 0; e < 11; e++)
                        {
                            int check = PlayerPrefs.GetInt("Loadout " + PlayerPrefs.GetInt("Selected Class") + " Var: " + PlayerPrefs.GetInt("Selected Loadout") + " Type: " + e) - 1;
                            if (check >= 0)
                            {
                                newCosInts.Add(check);
                            }
                        }

                        SendPlayerStatusOnSwitchedClassesServerRpc(true, PlayerPrefs.GetInt("Selected Class"), newCosInts.ToArray());
                    }
                    else
                    {
                        SendPlayerStatusOnSwitchedClassesServerRpc(false,0,new int[0]);
                    }
                }
                return;
            }
        }
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
                    clients[e].GetController().RecalculateClientPosition(playerPosRPCData[i]);
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
            clients[i].RemoveHeldWire(Vector3.zero, false);
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
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].SetWirePoint(neededWire.CreateNewClientWire(wireID, parentID), true);
                return;
            }
        }
    }

    [ClientRpc]
    public void RemoveClientWireClientRpc(ulong id, Vector3 finalPos, bool playSound)
    {
        Debug.Log("RemoveClientWireClientRpc");
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].RemoveHeldWire(finalPos, playSound);
                break;
            }
        }
    }

    [ClientRpc]
    public void SegmentClientWireClientRpc(ulong id, Vector3 finalPos, uint wireID, uint parentID, TeamList teamWireNeeded)
    {
        if (IsHost) { return; }

        Debug.Log("SegmentClientWireClientRpc");
        Wire neededWire = null;
        for (int e = 0; e < teams.Count; e++)
        {
            if (teams[e].teamColor == teamWireNeeded)
            {
                neededWire = teamWires[teams[e].spawns];
                break;
            }
        }
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                clients[i].RemoveHeldWire(finalPos, false);
                clients[i].SetWirePoint(neededWire.CreateNewClientWire(wireID, parentID), false);
                return;
            }
        }
    }


    [ClientRpc]
    void RespawnPlayerClientRpc(ulong id, TeamList team, Vector3 spawnPos, float respawnTimer)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                if (clients[i].IsOwner && clients[i].GetPlayerID() < botID)
                {
                    achievments.SaveAchievements();
                }
                //Refresh Stats
                clients[i].RespawnPlayer(spawnPos, respawnTimer);
                UpdateMatchFocalPoint(clients[i].GetTeam());
                return;
            }
        }
    }

    [ClientRpc]
    void KickPlayerClientRpc(ulong id, string reason)
    {
        Debug.Log("Disconnect: " + reason);
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                if (clients[i].IsOwner && clients[i].GetPlayerID() < botID)
                {
                    PlayerPrefs.SetString("Disconnect Reason", reason);
                }

                Wire.WirePoint wireHeld = clients[i].GetWirePoint();
                if (wireHeld != null && IsHost)
                {
                    RemoveClientWireClientRpc(id, wireHeld.point, false);
                }

                bool isowner = clients[i].IsOwner;
                Destroy(clients[i].gameObject);
                clients.RemoveAt(i);
                playerPosRPCData = new List<PlayerDataSentToClient>();
                for (int e = 0; e < clients.Count; e++)
                {
                    playerPosRPCData.Add(new PlayerDataSentToClient());
                }
                if (isowner && !IsHost)
                {
                    DisconnectToTitleScreen(false);
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
                clients[i].SetName(data.playerName);
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
                            clients[e].SetName(data[j].playerName);
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
                if (clients[i].GetCurrentGun() != null)
                {
                    clients[i].GetCurrentGun().SpawnProjectile(clients[i]);
                }
            }
        }
    }

    [ClientRpc]
    public void PlayerTookDamageClientRpc(ulong id, int currentHealth, ulong idOfKiller, int idHash)
    {
        int foundClient = 0;
        int foundKiller = 0;
        bool damagedIsCurrentPC = false;
        bool killerIsCurrentPC = false;

        //Search for client first
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                foundClient = i;
                if (clients[foundClient].IsOwner && id < botID)
                {
                    damagedIsCurrentPC = true;
                }
            }
            if (clients[i].GetPlayerID() == idOfKiller)
            {
                foundKiller = i;
                if (clients[foundKiller].IsOwner && idOfKiller < botID)
                {
                    killerIsCurrentPC = true;
                }
            }
        }

        //Keep this where it is
        int damageTaken = clients[foundClient].GetHealth() - Mathf.Max(currentHealth, 0);
        if (clients[foundClient].GetHealth() <= 0 || damageTaken == 0)
        {
            return;
        }

        if (damagedIsCurrentPC)
        {
            if (currentHealth <= 0)
            {
                if (killerIsCurrentPC)
                {
                    achievments.AddToValue("Achievement: Total Suicides", 1);
                    Debug.Log("Player " + id + " Suicided");
                }
                else
                {
                    Debug.Log("Player " + id + " was killed (" + clients[foundClient].GetHealth() + " -> " + currentHealth + " HP) by Player" + idOfKiller);
                }
                achievments.AddToValue("Achievement: Total Deaths", 1);
            }
            else
            {
                if (killerIsCurrentPC)
                {
                    achievments.AddToValue("Achievement: Total Self-Healing", Mathf.Max(0, -damageTaken));
                }
            }
        }
        if (killerIsCurrentPC && !damagedIsCurrentPC)
        {
            Debug.Log("Player " + id + " took " + damageTaken + " damage (" + clients[foundClient].GetHealth() + " -> " + currentHealth + " HP) by Player" + idOfKiller);
            achievments.AddToValue("Achievement: Total Damage", Mathf.Max(0, damageTaken));
            achievments.AddToValue("Achievement: Total Healing", Mathf.Max(0, -damageTaken));

            if (currentHealth <= 0)
            {
                achievments.AddToValue("Achievement: Total Kills", 1);
                switch (clients[foundClient].GetCurrentClass())
                {
                    case ClassList.labourer:
                        achievments.AddToValue("Achievement: Total Laborers Killed", 1);
                        break;
                    case ClassList.woodworker:
                        achievments.AddToValue("Achievement: Total Wood Workers Killed", 1);
                        break;
                    case ClassList.developer:
                        achievments.AddToValue("Achievement: Total Developers Killed", 1);
                        break;
                    case ClassList.programmer:
                        achievments.AddToValue("Achievement: Total Programmers Killed", 1);
                        break;
                    case ClassList.computer:
                        achievments.AddToValue("Achievement: Total Computers Killed", 1);
                        break;
                    case ClassList.fabricator:
                        achievments.AddToValue("Achievement: Total Fabricators Killed", 1);
                        break;
                    case ClassList.artist:
                        achievments.AddToValue("Achievement: Total Artists Killed", 1);
                        break;
                    case ClassList.castmember:
                        achievments.AddToValue("Achievement: Total Cast Members Killed", 1);
                        break;
                    case ClassList.craftsman:
                        achievments.AddToValue("Achievement: Total Craftsmen Killed", 1);
                        break;
                    case ClassList.manager:
                        achievments.AddToValue("Achievement: Total Managers Killed", 1);
                        break;
                    default:
                        break;
                }
            }
        }

        //Seperate from achievements
        if (killerIsCurrentPC && !damagedIsCurrentPC)
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

        clients[foundClient].SetHealth(currentHealth);
    }

    private void OnDrawGizmosSelected()
    {
        for (int i = 0; i < matchFocalPoint.Count; i++)
        {
            Gizmos.DrawCube(matchFocalPoint[i], Vector3.one);
        }
    }
}

[System.Serializable]
public struct PlayerDataSentToClient : INetworkSerializable
{
    public ulong id;
    public Vector3 headsetPos;
    public Quaternion headsetRot;
    public Vector3 rHandPos;
    public Quaternion rHandRot;
    public Vector3 lHandPos;
    public Quaternion lHandRot;
    public Vector3 pos;
    public Vector3 velocity;

    //Controls
    public Vector2 rightJoystick;
    public bool jump;
    public bool shoot;
    public bool crouch;
    public bool menu;

    //Movement
    public Vector3 _speed;
    public float _verticalVelocity;
    public float _fallTimeoutDelta;
    public float _hasBeenMovingDelta;

    //Midair Movement
    public Vector3 oldAxis;
    public Vector2 oldInput;
    public bool hasBeenGrounded;
    public bool hasBeenStopped;

    //Crouch
    public float currentCrouchLerp;
    public bool hasBeenCrouched;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref headsetPos);
        serializer.SerializeValue(ref headsetRot);
        serializer.SerializeValue(ref rHandPos);
        serializer.SerializeValue(ref rHandRot);
        serializer.SerializeValue(ref lHandPos);
        serializer.SerializeValue(ref lHandRot);
        serializer.SerializeValue(ref pos);
        serializer.SerializeValue(ref velocity);
        serializer.SerializeValue(ref rightJoystick);
        serializer.SerializeValue(ref jump);
        serializer.SerializeValue(ref shoot);
        serializer.SerializeValue(ref crouch);
        serializer.SerializeValue(ref menu);
        serializer.SerializeValue(ref _speed);
        serializer.SerializeValue(ref _verticalVelocity);
        serializer.SerializeValue(ref _fallTimeoutDelta);
        serializer.SerializeValue(ref _hasBeenMovingDelta);
        serializer.SerializeValue(ref oldAxis);
        serializer.SerializeValue(ref oldInput);
        serializer.SerializeValue(ref hasBeenGrounded);
        serializer.SerializeValue(ref hasBeenStopped);
        serializer.SerializeValue(ref currentCrouchLerp);
        serializer.SerializeValue(ref hasBeenCrouched);
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
    public string playerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref currentClass);
        serializer.SerializeValue(ref currentTeam);
        serializer.SerializeValue(ref cosmetics);
        serializer.SerializeValue(ref gunName);
        serializer.SerializeValue(ref playerName);
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

    //Prediction
    public float deltaTime; 

    //Controls
    public Vector2 rightJoystick;
    public bool jump;
    public bool shoot;
    public bool crouch;
    public bool menu;


    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref deltaTime);
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
        serializer.SerializeValue(ref menu);

    }
}