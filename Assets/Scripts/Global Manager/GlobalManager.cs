using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.Rendering.DebugUI;
using static UnityEngine.Rendering.VolumeComponent;

public class GlobalManager : NetworkBehaviour
{
    [Header("Server Settings")]
    List<TeamInfo> teams = new List<TeamInfo>();
    [SerializeField] NetworkVariable<int> ServerTickRate = new NetworkVariable<int>(10);

    [Header("Lists")]
    List<Player> clients = new List<Player>();
    [SerializeField] List<PlayerData> clientData;
    [SerializeField] Cosmetics co;
    [SerializeField] Achievements achievments;
    [SerializeField] GameObject clientPrefab;
    [SerializeField] List<Transform> teamSpawns;
    [SerializeField] List<Transform> teamGeometry;
    [SerializeField] List<Wire> teamWires;
    [SerializeField] List<Transform> teamBlocks;
    List<int> damageHashes = new List<int>();

    //Ect
    AllStats al;
    AudioSource au;
    NetworkManager m_NetworkManager;
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
                        SpawnPlayer(botID + (uint)i, "Bot # " + i, UnityEngine.Random.Range(0, 9), new int[0]);
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

    void CheckHostValidity()
    {
        if (!IsHost)
        {
            Debug.LogError("Global Manager Access Violation");
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
        CheckHostValidity();
        if (!serverStarted)
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

    public int GetCurrentRoundTime()
    {
        return currentGamemode.RequestRoundTime();
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
                    SpawnPlayer(botID + (uint)i, "Bot # " + i, UnityEngine.Random.Range(0, 9), new int[0]);
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
                SendInputDataServerRpc(clients[i].GetTracker().GetPlayerInputData());
            }
        }
    }

    void LateUpdate()
    {
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

    public Player GetClient(ulong id)
    {
        return clients[SearchClients(id)];
    }
    int SearchClients(ulong id)
    {
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].GetPlayerID() == id)
            {
                return i;
            }
        }
        Debug.LogError("Could Not Find Client: " + id);
        return -1;
    }
    int SearchGuns(int clientIndex, string gunNameKey)
    {
        for (int i = 0; i < clientData[clientIndex].playerGuns.Count; i++)
        {
            if (clientData[clientIndex].playerGuns[i].gunNameKey == gunNameKey)
            {
                return i;
            }
        }
        Debug.LogError("Could Not Find Client");
        return -1;
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
                    Transform t = null;
                    while (true)
                    {
                        Transform s = teamSpawns[teams[e].spawns].GetChild(UnityEngine.Random.Range(0, teamSpawns[teams[e].spawns].childCount));
                        if (s.gameObject.activeSelf)
                        {
                            t = s;
                            break;
                        }
                    }
                    spawnPos = t.position;
                    break;
                }
            }

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
            case ClassList.labourer:
                autoGun = "Flashpoint";
                break;
            case ClassList.woodworker:
                autoGun = "Flashpoint";
                break;
            case ClassList.developer:
                autoGun = "O-S F-O";
                break;
            case ClassList.programmer:
                autoGun = "W.I.P.";
                break;
            case ClassList.computer:
                autoGun = "Flashpoint";
                break;
            case ClassList.fabricator:
                autoGun = "O-S F-O";
                break;
            case ClassList.artist:
                autoGun = "W.I.P.";
                break;
            case ClassList.castmember:
                autoGun = "W.I.P.";
                break;
            case ClassList.craftsman:
                autoGun = "O-S F-O";
                break;
            case ClassList.manager:
                autoGun = "W.I.P.";
                break;
            default:
                break;
        }
        return autoGun;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnNewPlayerHostServerRpc(string playerName, int initialClass, int[] cosmetics, ServerRpcParams serverRpcParams = default)
    {
        SpawnPlayer(serverRpcParams.Receive.SenderClientId, playerName, initialClass, cosmetics);
    }

    void SpawnPlayer(ulong id, string playerName, int initialClass, int[] cosmetics)
    {
        CheckHostValidity();

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

        //New Player
        SetPlayerCosmeticsClientRpc(false, 0, id, cosmetics);
        SetPlayerClassClientRpc(false, 0, id, (ClassList)initialClass);
        SetPlayerTeamClientRpc(false, 0, id, decidedTeam);
        SetPlayerNameClientRpc(false, 0, id, playerName);
        SetPlayerGunsClientRpc(false, 0, id, autoGun, al.SearchGuns(autoGun).gunPrefab.GetComponent<Gun>().GetWeaponStats(), "", new WeaponStats[0], "", new WeaponStats[0]);
        RespawnPlayer(id, decidedTeam, true);

        //Send Other Player Data to New Connection
        SendClientOtherPlayerData(id);

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
                    if (clients[i].SendPlayerSwitchClassStatus())
                    {
                        initialClass = Mathf.Clamp(initialClass, 0, 9);
                        string autoGun = AutoGun((ClassList)initialClass);
                        SetPlayerClassClientRpc(false, 0, serverRpcParams.Receive.SenderClientId, (ClassList)initialClass);
                        SetPlayerCosmeticsClientRpc(false, 0, serverRpcParams.Receive.SenderClientId, cosmetics);
                        SetPlayerGunsClientRpc(false, 0, serverRpcParams.Receive.SenderClientId, autoGun, al.SearchGuns(autoGun).gunPrefab.GetComponent<Gun>().GetWeaponStats(), "", new WeaponStats[0], "", new WeaponStats[0]);
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
                    if (Convert.ToBoolean(PlayerPrefs.GetInt("SendToServerSwitchClass")))
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
                        SendPlayerStatusOnSwitchedClassesServerRpc(false, 0, new int[0]);
                    }
                }
                return;
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
        int i = SearchClients(id);

        if (clients[i].IsOwner && clients[i].GetPlayerID() < botID)
        {
            achievments.SaveAchievements();
        }
        //Refresh Stats
        clients[i].RespawnPlayer(spawnPos, respawnTimer);
        UpdateMatchFocalPoint(FindPlayerTeam(id));
        return;
    }

    [ClientRpc]
    void KickPlayerClientRpc(ulong id, string reason)
    {
        if (reason == null)
        {
            return;
        }
        Debug.Log("Disconnect: " + reason);
        int index = SearchClients(id);

        if (clients[index].IsOwner && clients[index].GetPlayerID() < botID)
        {
            PlayerPrefs.SetString("Disconnect Reason", reason);
        }

        Wire.WirePoint wireHeld = clients[index].GetWirePoint();
        if (wireHeld != null && IsHost)
        {
            RemoveClientWireClientRpc(id, wireHeld.point, false);
        }

        bool isowner = clients[index].IsOwner;
        Destroy(clients[index].gameObject);
        clients.RemoveAt(index);

        if (isowner && !IsHost)
        {
            DisconnectToTitleScreen(false);
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
        int oldHealth = (int)FindPlayerStat(id, ChangablePlayerStats.currentHealth);

        int damageTaken = oldHealth - Mathf.Max(currentHealth, 0);
        if ((int)FindPlayerStat(id, ChangablePlayerStats.currentHealth) <= 0 || damageTaken == 0)
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
                    Debug.Log("Player " + id + " was killed (" + oldHealth + " -> " + currentHealth + " HP) by Player" + idOfKiller);
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
            Debug.Log("Player " + id + " took " + damageTaken + " damage (" + oldHealth + " -> " + currentHealth + " HP) by Player" + idOfKiller);
            achievments.AddToValue("Achievement: Total Damage", Mathf.Max(0, damageTaken));
            achievments.AddToValue("Achievement: Total Healing", Mathf.Max(0, -damageTaken));

            if (currentHealth <= 0)
            {
                achievments.AddToValue("Achievement: Total Kills", 1);
                switch (FindPlayerClass(clients[foundClient].GetPlayerID()))
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
        if (IsHost)
        {
            SetPlayerValueClientRpc(false, 0, id, ChangablePlayerStats.currentHealth, currentHealth);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendInputDataServerRpc(PlayerInputData inputData, ServerRpcParams serverRpcParams = default)
    {
        for (int i = 0; i < clientData.Count; i++)
        {
            int index = SearchClients(serverRpcParams.Receive.SenderClientId);
            bool goodToGo = false;
            if (clientData[index].playerInputs.Count == 0)
            {
                goodToGo = true;
            }
            else
            {
                if (inputData.lastTimeSynced > clientData[index].playerInputs[clientData[index].playerInputs.Count - 1].lastTimeSynced)
                {
                    goodToGo = true;
                }
            }
            if (goodToGo)
            {
                clientData[index].playerInputs.Add(inputData);
                GetClient(serverRpcParams.Receive.SenderClientId).GetController().RecalculateServerPosition();
            }
        }
    }

    public void SendBotInputData(PlayerInputData inputData, ulong id)
    {
        CheckHostValidity();
        for (int i = 0; i < clientData.Count; i++)
        {
            int index = SearchClients(id);
            bool goodToGo = false;
            if (clientData[index].playerInputs.Count == 0)
            {
                goodToGo = true;
            }
            else
            {
                if (inputData.lastTimeSynced > clientData[index].playerInputs[clientData[index].playerInputs.Count - 1].lastTimeSynced)
                {
                    goodToGo = true;
                }
            }
            if (goodToGo)
            {
                clientData[index].playerInputs.Add(inputData);
                GetClient(id).GetController().RecalculateServerPosition();
            }
        }
    }

    [ClientRpc]
    public void SetPlayerPositionDataClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, PlayerPositionData data)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);
        clientData[index].playerPositionData.Add(data);
        if (!IsHost)
        {
            clients[index].GetController().RecalculateClientPosition();
        }
    }

    public PlayerPositionData GetCurrentPlayerPositonData(ulong id)
    {
        int index = SearchClients(id);
        if (clientData[index].playerPositionData.Count == 0)
        {
            //Debug.LogError("No Player Position Data Found");
            return new PlayerPositionData();
        }
        return clientData[index].playerPositionData[clientData[index].playerPositionData.Count - 1];
    }

    public PlayerInputData GetCurrentPlayerInputData(ulong id)
    {
        int index = SearchClients(id);
        if (clientData[index].playerPositionData.Count == 0)
        {
            //Debug.LogError("No Player Position Data Found");
            return new PlayerInputData();
        }
        return clientData[index].playerInputs[clientData[index].playerInputs.Count - 1];
    }

    [ClientRpc]
    public void SetPlayerValueClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, ChangablePlayerStats statName, float value)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);

        for (int i = 0; i < clientData[index].playerStats.Count; i++)
        {
            if (clientData[index].playerStats[i].statName == statName)
            {
                float oldHealth = FindPlayerStat(id, ChangablePlayerStats.currentHealth);
                clientData[index].playerStats[i].stat = value;
                clients[index].GetUIController().UpdateHealthUI(oldHealth);
                return;
            }
        }
    }

    public float FindPlayerStat(ulong id, ChangablePlayerStats statName)
    {
        int index = SearchClients(id);

        for (int i = 0; i < clientData[index].playerStats.Count; i++)
        {
            if (clientData[index].playerStats[i].statName == statName)
            {
                return clientData[index].playerStats[i].stat;
            }
        }
        return 0;
    }

    [ClientRpc]
    public void SetPlayerGunValueClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, string gunNameKey, ChangableWeaponStats statName, float value)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);
        int gunIndex = SearchGuns(index, gunNameKey);
        for (int i = 0; i < clientData[index].playerGuns[gunIndex].weaponStats.Count; i++)
        {
            if (clientData[index].playerGuns[gunIndex].weaponStats[i].statName == statName)
            {
                clientData[index].playerGuns[gunIndex].weaponStats[i].stat = value;
                clients[index].GetUIController().UpdateGunUI();
                return;
            }
        }
    }

    public float FindPlayerGunValue(ulong id, string gunNameKey, ChangableWeaponStats statName)
    {
        int index = SearchClients(id);
        int gunIndex = SearchGuns(index, gunNameKey);
        for (int i = 0; i < clientData[index].playerGuns[gunIndex].weaponStats.Count; i++)
        {
            if (clientData[index].playerGuns[gunIndex].weaponStats[i].statName == statName)
            {
                return clientData[index].playerGuns[gunIndex].weaponStats[i].stat;
            }
        }
        return 0;
    }

    [ClientRpc]
    public void SetPlayerNameClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, string value)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);
        clientData[index].playerName.value = value;
        clients[index].UpdateName();
    }

    public string FindPlayerName(ulong id)
    {
        return clientData[SearchClients(id)].playerName.value;
    }

    [ClientRpc]
    public void SetPlayerTeamClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, TeamList value)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);
        clientData[index].playerTeam.value = value;
        clients[index].UpdateTeamColor();
    }

    public void AddPlayerToClientList(Player player)
    {
        clients.Add(player);
        PlayerData data = new PlayerData();
        data.playerId.value = player.GetPlayerID();
        data.playerCosmetics.value = new int[0];
        data.playerStats = new List<PlayerStats>();
        data.playerGuns = new List<PlayerGunData>();
        data.playerInputs = new List<PlayerInputData>();
        data.playerPositionData = new List<PlayerPositionData>();
        clientData.Add(data);
    }

    public TeamList FindPlayerTeam(ulong id)
    {
        return clientData[SearchClients(id)].playerTeam.value;
    }

    [ClientRpc]
    public void SetPlayerClassClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, ClassList value)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);
        clientData[index].playerClass.value = value;
        SetPlayerValueClientRpc(sendToOneUser, oneUserId, id, ChangablePlayerStats.currentHealth, FindPlayerStat(id, ChangablePlayerStats.maxHealth));
        clients[index].UpdateClass();
        if (IsHost)
        {
            ResetClassStats(sendToOneUser, oneUserId, id, al.GetClassStats(FindPlayerClass(id)));
        }
    }


    public ClassList FindPlayerClass(ulong id)
    {
        return clientData[SearchClients(id)].playerClass.value;
    }

    public void ResetClassStats(bool sendToOneUser, ulong oneUserId, ulong id, PlayerStats[] data)
    {
        CheckHostValidity();
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            SetPlayerValueClientRpc(false,0,id, data[i].statName, data[i].stat);
        }
    }

    [ClientRpc]
    public void SetPlayerCosmeticsClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, int[] value)
    {
        if (sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);

        List<Cosmetic> stockCosmetics = GetCosmetics().GetClassCosmetics(FindPlayerClass(id));
        List<int> cosmeticIntList = new List<int>();
        for (int i = 0; i < value.Length; i++)
        {
            //This check is here to make sure you don't use unused cosmetic IDs
            if (value[i] < stockCosmetics.Count)
            {
                bool isDupeEquipRegion = false;
                for (int e = 0; e < cosmeticIntList.Count; e++)
                {
                    if (stockCosmetics[cosmeticIntList[e]].region == stockCosmetics[value[i]].region)
                    {
                        isDupeEquipRegion = true;
                    }
                }
                if (!isDupeEquipRegion)
                {
                    cosmeticIntList.Add(value[i]);
                }
            }
        }
        //Stock
        for (int i = 0; i < stockCosmetics.Count; i++)
        {
            if (stockCosmetics[i].stock == StockCosmetic.stock)
            {
                bool isStockDupeEquipRegion = false;
                for (int e = 0; e < cosmeticIntList.Count; e++)
                {
                    if (stockCosmetics[cosmeticIntList[e]].region == stockCosmetics[i].region)
                    {
                        isStockDupeEquipRegion = true;
                    }
                }
                if (!isStockDupeEquipRegion)
                {
                    cosmeticIntList.Add(i);
                }
            }
        }
        clientData[index].playerCosmetics.value = cosmeticIntList.ToArray();

        clients[index].UpdateClass();
    }

    public int[] FindPlayerCosmetics(ulong id)
    {
        int[] data = clientData[SearchClients(id)].playerCosmetics.value;
        if(data == null)
        {
            return new int[0];
        }
        return data;
    }

    [ClientRpc]
    public void SetPlayerGunsClientRpc(bool sendToOneUser, ulong oneUserId, ulong id, string primaryKey, WeaponStats[] primaryStats, string secondaryKey, WeaponStats[] secondaryStats, string meleeKey, WeaponStats[] meleeStats)
    {
        if(sendToOneUser && !CheckIfIDIsOwner(oneUserId))
        {
            return;
        }

        int index = SearchClients(id);
        clientData[index].playerGuns = new List<PlayerGunData>();

        PlayerGunData primary = new PlayerGunData();
        primary.gunNameKey = primaryKey;
        primary.weaponStats = primaryStats.ToList();
        clientData[index].playerGuns.Add(primary);
        PlayerGunData secondary = new PlayerGunData();
        secondary.gunNameKey = secondaryKey;
        secondary.weaponStats = secondaryStats.ToList();
        clientData[index].playerGuns.Add(secondary);
        PlayerGunData melee = new PlayerGunData();
        melee.gunNameKey = meleeKey;
        melee.weaponStats = meleeStats.ToList();
        clientData[index].playerGuns.Add(melee);

        clients[index].UpdateGuns();
    }


    public string FindPlayerGun(ulong id, int slot)
    {
        return clientData[SearchClients(id)].playerGuns[slot].gunNameKey;
    }

    public void SendClientOtherPlayerData(ulong id)
    {
        for (int i = 0; i < clientData.Count; i++)
        {
            SetPlayerCosmeticsClientRpc(true, id, clients[i].GetPlayerID(), clientData[i].playerCosmetics.value);
            SetPlayerClassClientRpc(true, id, clients[i].GetPlayerID(), clientData[i].playerClass.value);
            SetPlayerTeamClientRpc(true, id, clients[i].GetPlayerID(), clientData[i].playerTeam.value);
            SetPlayerNameClientRpc(true, id, clients[i].GetPlayerID(), clientData[i].playerName.value);
            SetPlayerGunsClientRpc(true, id, clients[i].GetPlayerID(), clientData[i].playerGuns[0].gunNameKey, clientData[i].playerGuns[0].weaponStats.ToArray(), clientData[i].playerGuns[1].gunNameKey, clientData[i].playerGuns[1].weaponStats.ToArray(), clientData[i].playerGuns[2].gunNameKey, clientData[i].playerGuns[2].weaponStats.ToArray());
        }
    }

    bool CheckIfIDIsOwner(ulong id)
    {
        int index = SearchClients(id);
        if (clients[index].IsOwner && clients[index].GetPlayerID() < botID)
        {
            return true;
        }
        return false;
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
public class TeamInfo : INetworkSerializable
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
public class PlayerData
{
    public SyncString playerName;
    public SyncUlong playerId;
    public SyncClass playerClass;
    public SyncTeam playerTeam;
    public SyncCosmetics playerCosmetics;
    public List<PlayerStats> playerStats;
    public List<PlayerGunData> playerGuns;
    public List<PlayerInputData> playerInputs;
    public List<PlayerPositionData> playerPositionData;
}

[System.Serializable]
public struct SyncString : INetworkSerializable
{
    public string value;
    public float lastTimeSynced;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref value);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}

[System.Serializable]
public struct SyncCosmetics : INetworkSerializable
{
    public int[] value;
    public float lastTimeSynced;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref value);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}



[System.Serializable]
public struct SyncUlong : INetworkSerializable
{
    public ulong value;
    public float lastTimeSynced;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref value);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}

[System.Serializable]
public struct SyncClass : INetworkSerializable
{
    public ClassList value;
    public float lastTimeSynced;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref value);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}

[System.Serializable]
public struct SyncTeam : INetworkSerializable
{
    public TeamList value;
    public float lastTimeSynced;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref value);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}



[System.Serializable]
public class PlayerInputData : INetworkSerializable
{
    public Vector3 headsetPos;
    public Quaternion headsetRot;
    public Vector3 rHandPos;
    public Quaternion rHandRot;
    public Vector3 lHandPos;
    public Quaternion lHandRot;
    public Vector2 rightJoystick;
    public bool jump;
    public bool shoot;
    public bool crouch;
    public bool menu;
    public float lastTimeSynced;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
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
        serializer.SerializeValue(ref lastTimeSynced);
    }
}


[System.Serializable]
public class PlayerGunData
{
    public string gunNameKey;
    public List<WeaponStats> weaponStats;
    public float lastTimeSynced;

}

[System.Serializable]
public class WeaponStats : INetworkSerializable
{
    public ChangableWeaponStats statName;
    public float stat;
    public float lastTimeSynced;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref statName);
        serializer.SerializeValue(ref stat);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}

[System.Serializable]
public class PlayerStats : INetworkSerializable
{
    public ChangablePlayerStats statName;
    public float stat;
    public float lastTimeSynced;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref statName);
        serializer.SerializeValue(ref stat);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}

[System.Serializable]
public class PlayerPositionData : INetworkSerializable
{
    //Position
    public Vector3 position;
    public Vector3 velocity;

    //Physics Calculations
    public Vector3 _speed;
    public float _verticalVelocity;
    public float _fallTimeoutDelta;
    public float _hasBeenMovingDelta;
    public float baseSpeed;
    public Vector3 oldAxis;
    public Vector2 oldInput;
    public bool hasBeenGrounded;
    public bool hasBeenStopped;
    public float currentCrouchLerp;
    public bool hasBeenCrouched;
    public Vector3 mainCamforward;
    public Vector3 mainCamRight;

    //Sync
    public float lastTimeSynced;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref velocity);
        serializer.SerializeValue(ref _speed);
        serializer.SerializeValue(ref _verticalVelocity);
        serializer.SerializeValue(ref _fallTimeoutDelta);
        serializer.SerializeValue(ref _hasBeenMovingDelta);
        serializer.SerializeValue(ref baseSpeed);
        serializer.SerializeValue(ref oldAxis);
        serializer.SerializeValue(ref oldInput);
        serializer.SerializeValue(ref hasBeenGrounded);
        serializer.SerializeValue(ref hasBeenStopped);
        serializer.SerializeValue(ref currentCrouchLerp);
        serializer.SerializeValue(ref hasBeenCrouched);
        serializer.SerializeValue(ref mainCamforward);
        serializer.SerializeValue(ref mainCamRight);
        serializer.SerializeValue(ref lastTimeSynced);
    }
}

[SerializeField]
public enum ChangableWeaponStats
{
    shotsPerSecond,
    bulletsPerShot,
    maxAmmo,
    maxClip,
    bulletSpeed,
    damage,
    bulletSpreadAngle,
    radiationAfterburn,
    maxBulletRange,
    reloadSpeed,
    currentClip,
    currentAmmo,
}

[SerializeField]
public enum ChangablePlayerStats
{
    currentHealth,
    maxHealth,
    eyeHeight,
    groundSpeed,
    airSpeed,
    currentlyHeldWeapon,
}
