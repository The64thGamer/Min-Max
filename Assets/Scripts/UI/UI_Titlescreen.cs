using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Management;

public class UI_Titlescreen : MonoBehaviour
{
    [SerializeField] UIDocument doc;
    [SerializeField] List<Texture2D> mapIcons;
    [SerializeField] List<string> mapNames;
    [SerializeField] NetworkManager m_NetworkManager;
    [SerializeField] VisualTreeAsset vta;
    [SerializeField] Texture2D unknownMap;

    //Colors
    [SerializeField] Color borderButtonSelected;

    //Ect
    VisualElement root;
    int currentSceneToLoad;

    bool alreadyLoading;
    bool foundLocalServer;
    int joinLocalIndex;
    bool serverCheckRunning;

    bool localOrServer;

    bool serverAttempt;
    bool serverFail;
    string serverFailMessage;

    private void OnEnable()
    {
        //Pinging
        m_NetworkManager = GameObject.Find("Transport").GetComponent<NetworkManager>();
        if (m_NetworkManager != null)
        {
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
            m_NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes("P");
        }

        //Root
        root = doc.rootVisualElement;

        //Bottom Bar
        Button rc_startVR = root.Q<Button>("RCStartVR");
        Button rc_play = root.Q<Button>("RCPlay");
        Button rc_exit = root.Q<Button>("RCExit");

        //Play Menu
        Button ps_joinserver = root.Q<Button>("PSJoinServer");
        Button ps_joinlocal = root.Q<Button>("PSJoinLocal");
        Button ps_startserver = root.Q<Button>("PSStartServer");
        Button ps_startlocal = root.Q<Button>("PSStartLocal");

        //Create Local
        Button ms_startgame = root.Q<Button>("StartGame");
        DropdownField selectMap = root.Q<DropdownField>("SelectMap");
        SliderInt maxPlayers = root.Q<SliderInt>("MaxPlayers");
        Toggle spectatorAsPlayer = root.Q<Toggle>("SpectatorAsPlayer");
        Toggle isHostSpectator = root.Q<Toggle>("HostisSpectator");
        TextField selectPort = root.Q<TextField>("SelectPort");
        DropdownField team1 = root.Q<DropdownField>("SelectTeam1");
        DropdownField team2 = root.Q<DropdownField>("SelectTeam2");
        Toggle spawnBots = root.Q<Toggle>("SpawnBots");
        TextField serverName = root.Q<TextField>("ServerName");


        //Join Local
        Button addnewLocalServer = root.Q<Button>("AddNewLocalServer");
        TextField newLocalPort = root.Q<TextField>("NewLocalServerPort");
        Button newLocalDelete = root.Q<Button>("NewLocalDeleteGame");
        Button newLocalStart = root.Q<Button>("NewLocalStartGame");

        //Functions When Button is Clicked
        rc_startVR.clicked += () => SwitchMainTab(0);
        rc_play.clicked += () => SwitchMainTab(1);
        rc_exit.clicked += () => Application.Quit();
        ps_startlocal.clicked += () => StartLocalOrHost(0);
        ps_startserver.clicked += () => StartLocalOrHost(1);
        ps_joinserver.clicked += () => StartLocalOrHost(2);
        ps_joinlocal.clicked += () => StartLocalOrHost(3);
        ms_startgame.clicked += () => StartCoroutine(LoadMap());
        newLocalStart.clicked += () => StartCoroutine(LoadMap());
        selectMap.RegisterValueChangedCallback(evt => SwapMap(selectMap));
        maxPlayers.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("ServerMaxPlayers", (int)maxPlayers.value));
        spectatorAsPlayer.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("ServerSpectatorAsPlayer", spectatorAsPlayer.value ? 1 : 0));
        selectPort.RegisterValueChangedCallback(evt => TryPortChange(selectPort));
        serverName.RegisterValueChangedCallback(evt => ServerRename(serverName));
        team1.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("Team1Setting", team1.index));
        team2.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("Team2Setting", team2.index));
        spawnBots.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("SpawnBotsInEmpty", spawnBots.value ? 1 : 0));
        addnewLocalServer.clicked += () => AddNewLocalServer(newLocalPort.text);
        newLocalDelete.clicked += () => RemoveLocalServer(joinLocalIndex);

        //Set Default Values when Out of Range (Or on First Boot)
        if (PlayerPrefs.GetInt("IsVREnabled") == 1)
        {
            rc_startVR.style.display = DisplayStyle.None;
        }
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
        if (PlayerPrefs.GetInt("ServerPort") <= 0)
        {
            PlayerPrefs.SetInt("ServerPort", 7777);
        }
        if (PlayerPrefs.GetFloat("PlayerHeight") < 0.9144f || PlayerPrefs.GetFloat("PlayerHeight") > 2.1336) //Value is 3 feet, 7 Feet
        {
            PlayerPrefs.SetFloat("PlayerHeight", 1.524f); //Value is 5 feet
        }

        //Set Values
        selectMap.choices = mapNames;
        selectMap.index = PlayerPrefs.GetInt("ServerMapName");
        maxPlayers.value = Mathf.Max(PlayerPrefs.GetInt("ServerMaxPlayers"), 1);
        spawnBots.value = Convert.ToBoolean(PlayerPrefs.GetInt("SpawnBotsInEmpty"));
        spectatorAsPlayer.value = Convert.ToBoolean(PlayerPrefs.GetInt("ServerSpectatorAsPlayer"));
        team1.index = PlayerPrefs.GetInt("Team1Setting");
        team2.index = PlayerPrefs.GetInt("Team2Setting");
        selectPort.value = PlayerPrefs.GetInt("ServerPort").ToString();
        serverName.value = PlayerPrefs.GetString("ServerName");

        SwitchMainTab(1);
    }

    IEnumerator LoadMap()
    {
        if (!alreadyLoading)
        {
            alreadyLoading = true;
            if (currentSceneToLoad >= 0)
            {
                PlayerPrefs.SetInt("ServerMapName", currentSceneToLoad);
                Application.backgroundLoadingPriority = ThreadPriority.Low;
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mapNames[currentSceneToLoad]);
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }
        }
    }

    void TryPortChange(TextField selectPort)
    {
        try
        {
            PlayerPrefs.SetInt("ServerPort", ushort.Parse(selectPort.text));
        }
        catch (Exception)
        {
            PlayerPrefs.SetInt("ServerPort", 7777);
            selectPort.value = PlayerPrefs.GetInt("ServerPort").ToString();
        }
    }

    void ServerRename(TextField name)
    {
        string rename = name.text;
        if(rename.Length > 20)
        {
            rename = rename.Substring(0, 20);
        }
        rename = string.Join("", rename.ToCharArray().Where(x => ((int)x) < 127));
        PlayerPrefs.SetString("ServerName", rename);
    }

    void StartLocalOrHost(int loadmapmode)
    {
        if (!alreadyLoading)
        {
            //Default
            root.Q<VisualElement>("LocalServers").style.display = DisplayStyle.None;
            root.Q<VisualElement>("ServerSettings").style.display = DisplayStyle.None;
            root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.None;
            root.Q<VisualElement>("JoinLocalCol").style.display = DisplayStyle.None;
            root.Q<VisualElement>("NewLocalDeleteGame").style.display = DisplayStyle.None;
            root.Q<VisualElement>("NewLocalStartGame").style.display = DisplayStyle.None;
            root.Q<TextField>("SelectPort").style.display = DisplayStyle.None;


            root.Q<Button>("StartGame").style.display = DisplayStyle.None;

            VisualElement col1 = root.Q<VisualElement>("PlayMenuCol1");

            Color noBorder = borderButtonSelected;
            noBorder.a = 0;

            for (int i = 0; i < col1.childCount; i++)
            {
                SetVEBorderColor(col1.ElementAt(i), noBorder);
            }
            //Clear Old List
            VisualElement visList = root.Q<VisualElement>("LocalServerList");
            List<VisualElement> children = new List<VisualElement>();
            foreach (var child in visList.Children())
            {
                //This stupid setup is because you can't delete children in foreach statement.
                children.Add(child);
            }
            for (int i = 0; i < children.Count; i++)
            {
                visList.Remove(children[i]);
            }

            PlayerPrefs.SetInt("LoadMapMode", loadmapmode);

            switch (loadmapmode)
            {
                case 0:
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("ServerSettings").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("PSStartLocal"), borderButtonSelected);
                    root.Q<TextField>("SelectPort").style.display = DisplayStyle.Flex;
                    currentSceneToLoad = root.Q<DropdownField>("SelectMap").index;
                    root.Q<Label>("StartSetting").text = "Start Local Game";
                    break;
                case 1:
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("ServerSettings").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("PSStartServer"), borderButtonSelected);
                    currentSceneToLoad = root.Q<DropdownField>("SelectMap").index;
                    root.Q<Label>("StartSetting").text = "Start Server (Join Code will copy to clipboard on start)";
                    break;
                case 2:
                    localOrServer = true;
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("JoinLocalCol").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("LocalServers").style.display = DisplayStyle.Flex;
                    root.Q<Label>("NewLocalServerName").text = "Select A Server";
                    root.Q<Label>("NewLocalMapName").text = "";
                    root.Q<Label>("NewLocalPlayerCount").text = "";
                    root.Q<VisualElement>("NewLocalMapIcon").style.backgroundImage = new StyleBackground(unknownMap);
                    root.Q<TextField>("NewLocalServerPort").label = "Join Key";

                    SetVEBorderColor(root.Q<VisualElement>("PSJoinServer"), borderButtonSelected);

                    //Recreate List
                    if (PlayerPrefs.GetInt("GlobalServersAdded") > 0)
                    {
                        for (int i = 0; i < PlayerPrefs.GetInt("GlobalServersAdded"); i++)
                        {
                            TemplateContainer myUI = vta.Instantiate();
                            myUI.Q<Label>("ServerName").text = "(" + PlayerPrefs.GetString("GlobalServer" + i) + ") " + PlayerPrefs.GetString("GlobalServerName" + i);
                            int e = i;
                            myUI.Q<Button>("Button").clicked += () => SetCurrentLocalServer(e);

                            visList.Add(myUI);
                        }
                    }
                    SetVEBorderColor(root.Q<VisualElement>("PSJoinServer"), borderButtonSelected);
                    break;
                case 3:
                    localOrServer = false;
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("JoinLocalCol").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("LocalServers").style.display = DisplayStyle.Flex; 
                    root.Q<Label>("NewLocalServerName").text = "Select A Server";
                    root.Q<Label>("NewLocalMapName").text = "";
                    root.Q<Label>("NewLocalPlayerCount").text = "";
                    root.Q<VisualElement>("NewLocalMapIcon").style.backgroundImage = new StyleBackground(unknownMap);
                    root.Q<TextField>("NewLocalServerPort").label = "Port";

                    SetVEBorderColor(root.Q<VisualElement>("PSJoinLocal"), borderButtonSelected);

                    //Recreate List
                    if (PlayerPrefs.GetInt("LocalServersAdded") > 0)
                    {
                        for (int i = 0; i < PlayerPrefs.GetInt("LocalServersAdded"); i++)
                        {
                            TemplateContainer myUI = vta.Instantiate();
                            myUI.Q<Label>("ServerName").text = "(" + PlayerPrefs.GetInt("LocalServer" + i).ToString("00000") + ") " + PlayerPrefs.GetString("LocalServerName" + i);
                            int e = i;
                            myUI.Q<Button>("Button").clicked += () => SetCurrentLocalServer(e);

                            visList.Add(myUI);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }

    void SetCurrentLocalServer(int index)
    {
        if (!serverCheckRunning)
        {
            serverCheckRunning = true;
            root.Q<VisualElement>("NewLocalDeleteGame").style.display = DisplayStyle.None;
            joinLocalIndex = index;
            foundLocalServer = false;
            if (!localOrServer)
            {
                m_NetworkManager.GetComponent<UnityTransport>().ConnectionData.Port = (ushort)PlayerPrefs.GetInt("LocalServer" + index);
                m_NetworkManager.StartClient();
                StartCoroutine(LocalServerCheck(index));
            }
            else
            {
                m_NetworkManager.StartClient();
                StartCoroutine(LocalServerCheck(index));
            }
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
            serverAttempt = true;
        }
        catch (RelayServiceException e)
        {
            serverFail = true;
            serverFailMessage = e.Message;
        }
    }

    //This entire loop sucks, but it gets the job done idgaf
    IEnumerator LocalServerCheck(int index)
    {
        Label name = root.Q<Label>("NewLocalServerName");

        if (localOrServer)
        {
            serverAttempt = false;
            serverFail = false;
            name.text = "Attempting Allocation";
            JoinServer(PlayerPrefs.GetString("GlobalServer" + index));
            while (!serverAttempt && !serverFail)
            {
                yield return null;
            }
            if (!serverFail)
            {
                PlayerPrefs.SetString("JoinCode", PlayerPrefs.GetString("GlobalServer" + index));
            }
        }

        if (!serverFail)
        {
            int secondCount = 0;
            while (true)
            {
                if (foundLocalServer)
                {
                    break;
                }
                name.text = "Connecting To Server";
                for (int i = 0; i < secondCount + 1; i++)
                {
                    name.text += ". ";
                }
                if (secondCount >= 10)
                {
                    if (!foundLocalServer)
                    {
                        serverFail = true;
                        serverFailMessage = "Could Not Connect To Server.";
                    }
                    break;
                }
                secondCount++;
                yield return new WaitForSeconds(1.0f);
            }
        }
        if(serverFail)
        {
            if (m_NetworkManager != null && m_NetworkManager.isActiveAndEnabled)
            {
                m_NetworkManager.Shutdown();
            }
            name.text = serverFailMessage;
            root.Q<VisualElement>("NewLocalDeleteGame").style.display = DisplayStyle.Flex;
        }
        serverCheckRunning = false;
        serverFail = false;
    }

    void AddNewLocalServer(string port)
    {
        if (localOrServer)
        {
            PlayerPrefs.SetInt("GlobalServersAdded", PlayerPrefs.GetInt("GlobalServersAdded") + 1);
            try
            {
                PlayerPrefs.SetString("GlobalServer" + (PlayerPrefs.GetInt("GlobalServersAdded") - 1), port);
            }
            catch (Exception)
            {
                PlayerPrefs.SetInt("GlobalServersAdded", PlayerPrefs.GetInt("GlobalServersAdded") - 1);
            }
            StartLocalOrHost(2);
        }
        else
        {
            PlayerPrefs.SetInt("LocalServersAdded", PlayerPrefs.GetInt("LocalServersAdded") + 1);
            try
            {
                PlayerPrefs.SetInt("LocalServer" + (PlayerPrefs.GetInt("LocalServersAdded") - 1), (int)ushort.Parse(port));
            }
            catch (Exception)
            {
                PlayerPrefs.SetInt("LocalServersAdded", PlayerPrefs.GetInt("LocalServersAdded") - 1);
            }
            StartLocalOrHost(3);

        }

    }

    void RemoveLocalServer(int index)
    {
        if (!localOrServer)
        {
            PlayerPrefs.SetInt("LocalServersAdded", PlayerPrefs.GetInt("LocalServersAdded") - 1);
            for (int i = index; i < PlayerPrefs.GetInt("LocalServersAdded"); i++)
            {
                PlayerPrefs.SetInt("LocalServer" + i, PlayerPrefs.GetInt("LocalServer" + (i + 1)));
            }
            StartLocalOrHost(3);

        }
        else
        {
            PlayerPrefs.SetInt("GlobalServersAdded", PlayerPrefs.GetInt("GlobalServersAdded") - 1);
            for (int i = index; i < PlayerPrefs.GetInt("LocalServersAdded"); i++)
            {
                PlayerPrefs.SetString("GlobalServer" + i, PlayerPrefs.GetString("GlobalServer" + (i + 1)));
            }
            StartLocalOrHost(2);
        }
    }

    void SwitchMainTab(int tab)
    {
        if (!alreadyLoading)
        {
            VisualElement tabroot = root.Q<VisualElement>("MiddleBar");
            VisualElement buttonRoot = root.Q<VisualElement>("BottomBar");

            Color noBorder = borderButtonSelected;
            noBorder.a = 0;

            for (int i = 0; i < tabroot.childCount; i++)
            {
                tabroot.ElementAt(i).style.display = DisplayStyle.None;
            }
            for (int i = 0; i < buttonRoot.childCount; i++)
            {
                SetVEBorderColor(buttonRoot.ElementAt(i), noBorder);
            }

            switch (tab)
            {
                case 0:
                    root.Q<VisualElement>("TitleMenu").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("RCStartVR"), borderButtonSelected);
                    StartCoroutine(StartXR());
                    break;
                case 1:
                    root.Q<VisualElement>("PlayMenu").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("RCPlay"), borderButtonSelected);
                    StartLocalOrHost(0);
                    break;
                default:
                    break;
            }
        }
    }

    void SetVEBorderColor(VisualElement v, Color c)
    {
        v.style.borderTopColor = new StyleColor(c);
        v.style.borderRightColor = new StyleColor(c);
        v.style.borderLeftColor = new StyleColor(c);
        v.style.borderBottomColor = new StyleColor(c);
    }


    void SwapMap(DropdownField evt)
    {
        root.Q<VisualElement>("MapIcon").style.backgroundImage = new StyleBackground(mapIcons[evt.index]);
        currentSceneToLoad = evt.index;
    }

    IEnumerator StartXR()
    {
        IReadOnlyList<XRLoader> loaders = XRGeneralSettings.Instance.Manager.activeLoaders;
        Debug.Log(loaders);
        for (int i = 0; i < loaders.Count; i++)
        {
            Debug.Log(loaders[i]);
        }
        yield return XRGeneralSettings.Instance.Manager.InitializeLoader();
        if (XRGeneralSettings.Instance.Manager.activeLoader == null)
        {
            Debug.LogError("Initializing XR Failed. Check Editor or Player log for details.");
        }
        else
        {
            Debug.Log("Starting XR...");
            XRGeneralSettings.Instance.Manager.StartSubsystems();
            PlayerPrefs.SetInt("IsVREnabled", 1);
            yield return null;
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("VR Title Screen");
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }
    }

    private void OnClientDisconnectCallback(ulong obj)
    {
        if (!m_NetworkManager.IsServer && m_NetworkManager.DisconnectReason != string.Empty)
        {
            foundLocalServer = true;
            string payload = m_NetworkManager.DisconnectReason;

            if (payload[0] == 'P')
            {
                string[] limiters = payload.Substring(1, payload.Length - 1).Split("😂", StringSplitOptions.None);
                if (limiters.Length == 5)
                {
                    if (Convert.ToUInt16(limiters[4]) == m_NetworkManager.NetworkConfig.ProtocolVersion)
                    {
                        root.Q<Label>("NewLocalPlayerCount").text = limiters[0] + "/" + limiters[1];
                        if (limiters[2] == "")
                        {
                            root.Q<Label>("NewLocalServerName").text = "'Unnamed Server'";
                        }
                        else
                        {
                            string rename = limiters[2];
                            if (rename.Length > 20)
                            {
                                rename = rename.Substring(0, 20);
                            }
                            rename = string.Join("", rename.ToCharArray().Where(x => ((int)x) < 127));
                            root.Q<Label>("NewLocalServerName").text = "'" + rename + "'";
                            PlayerPrefs.SetString("LocalServerName" + joinLocalIndex, limiters[2]);
                        }
                        int currentMap = Convert.ToInt16(limiters[3]);
                        string mapName = "Unknown Map";
                        if(currentMap < mapNames.Count)
                        {
                            mapName = mapNames[currentMap];
                        }
                        root.Q<Label>("NewLocalMapName").text = mapName;
                        root.Q<VisualElement>("NewLocalMapIcon").style.backgroundImage = new StyleBackground(mapIcons[currentMap]);
                        currentSceneToLoad = currentMap;
                        root.Q<VisualElement>("NewLocalStartGame").style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        if(Convert.ToUInt16(limiters[4]) > m_NetworkManager.NetworkConfig.ProtocolVersion)
                        {
                            root.Q<Label>("NewLocalServerName").text = "Err. Server is running newer version.";
                        }
                        else
                        {
                            root.Q<Label>("NewLocalServerName").text = "Err. Server is running older version.";
                        }
                    }
                }
                else
                {
                    root.Q<Label>("NewLocalServerName").text = "Server Payload Corrupted";
                }
            }
            else if (payload[0] == 'E')
            {
                root.Q<Label>("NewLocalServerName").text = payload.Substring(1, payload.Length - 1);
            }
            else
            {
                root.Q<Label>("NewLocalServerName").text = "Unknown Error: " + payload;
            }
            root.Q<VisualElement>("NewLocalDeleteGame").style.display = DisplayStyle.Flex;
        }
    }
}
