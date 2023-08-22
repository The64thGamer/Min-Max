using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Xml.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEditor.ShaderGraph.Legacy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Management;
using Label = UnityEngine.UIElements.Label;

public class Menu : MonoBehaviour
{
    [Header("Physical Menu")]
    [SerializeField] MenuPage[] pages;
    [SerializeField] UIDocument leftMenu;
    [SerializeField] UIDocument rightMenu;
    [SerializeField] Transform centerRing;
    [SerializeField] RenderTexture menuLeftRT;
    [SerializeField] RenderTexture menuRightRT;
    [SerializeField] RenderTexture fakeMenuLeftRT;
    [SerializeField] RenderTexture fakeMenuRightRT;

    [Header("Lists")]
    [SerializeField] Texture2D[] teamIcons;
    [SerializeField] Color[] teamColors;
    [SerializeField] GameObject[] playerModels;
    [SerializeField] Transform playerModelHolder;
    [SerializeField] string[] cosmeticTypes;
    [SerializeField] MenuMaps[] maps;

    [Header("Objects")]
    [SerializeField] GameObject customizeMenuCamera;
    [SerializeField] Cosmetics cosmetics;
    [SerializeField] VisualTreeAsset cosmeticIconVTA;
    [SerializeField] VisualTreeAsset serverIconVTA;
    [SerializeField] Texture2D noneTexture;
    [SerializeField] Texture2D noMapTexture;
    [SerializeField] AudioSource aus;
    [SerializeField] Player optionalPlayer;
    List<GameObject> currentCharMeshes = new List<GameObject>();

    int[] cosmeticInts;
    NetworkManager m_NetworkManager;
    bool flippingPage;
    bool loadingMap;
    float soundTimer;

    //Customize Page
    int currentCustClass;
    int currentCustTeam;
    int currentCustPage;
    int currentCustCosmType;
    int currentCustLoadout;

    //Join Game Page
    bool onlineServerMenu;
    int currentServerSelected;
    int currentServerPage;
    ServerCheck serverCheck;
    string serverFailMessage;

    //Const
    const float pageFlipScaleReduction = 2.0f;

    private void OnEnable()
    {
        //Pinging
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        m_NetworkManager = GameObject.Find("Transport").GetComponent<NetworkManager>();
        if (m_NetworkManager != null)
        {
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
        }

        if (PlayerPrefs.GetInt("Settings: Vsync") == 1)
        {
            QualitySettings.vSyncCount = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
        }

        if (PlayerPrefs.GetInt("Settings: Windowed") == 1)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        float height = PlayerPrefs.GetFloat("Settings: PlayerHeight");
        if (height < 0.9144f || height > 2.1336) //Value is 3 feet, 7 Feet
        {
            PlayerPrefs.SetFloat("Settings: PlayerHeight", 1.524f); //Value is 5 feet
        }

        float fov = PlayerPrefs.GetFloat("Settings: FOV");
        if (fov < 70 || fov > 120)
        {
            PlayerPrefs.SetFloat("Settings: FOV", 90);
        }

        if (cosmetics == null)
        {
            cosmetics = GameObject.Find("Global Manager").transform.GetChild(0).GetComponent<Cosmetics>();
        }

        SwitchPage(0);
        if(optionalPlayer != null)
        {
            centerRing.localEulerAngles = new Vector3(0, 180, 0);
        }
    }

    private void Update()
    {
        soundTimer = Mathf.Max(0, soundTimer - Time.deltaTime);
        if (flippingPage)
        {
            centerRing.localEulerAngles = new Vector3(0, centerRing.localEulerAngles.y + (Time.deltaTime * 750), 0);
            centerRing.localScale = new Vector3(1 - (Mathf.Max(0, Vector3.Dot(transform.forward, -centerRing.right))/ pageFlipScaleReduction), 1, 1);
            if (centerRing.localEulerAngles.y >= 180)
            {
                centerRing.localEulerAngles = new Vector3(0, 180, 0);
                centerRing.localScale = Vector3.one;
                flippingPage = false;
            }
        }
    }

    void SwitchPage(int index)
    {
        if(optionalPlayer != null && index == 0)
        {
            //Pause menu must be the last index in pages
            index = pages.Length - 1;
        }
        if (!flippingPage)
        {
            leftMenu.visualTreeAsset = pages[index].leftAsset;
            rightMenu.visualTreeAsset = pages[index].rightAsset;

            SetButtons(leftMenu.rootVisualElement, pages[index].leftInteractables, pages[index].leftPageName);
            SetButtons(rightMenu.rootVisualElement, pages[index].rightInteractables, pages[index].rightPageName);

            flippingPage = true;
            centerRing.localEulerAngles = new Vector3(0, 0, 0);
            Graphics.CopyTexture(menuLeftRT, fakeMenuLeftRT);
            Graphics.CopyTexture(menuRightRT, fakeMenuRightRT);
        }
    }

    void SetButtons(VisualElement root, List<MenuButtons> interactables, string pagename)
    {
        if (interactables == null || root == null)
        {
            return;
        }
        for (int i = 0; i < interactables.Count; i++)
        {
            int iCtx = i;

            switch (interactables[iCtx].type)
            {
                case MenuButtonType.button:
                    Button button = root.Q<Button>(interactables[iCtx].name);
                    button.clicked += () => ButtonPressed(pagename, interactables[iCtx].name, "", false, 0, interactables[iCtx].sound);
                    button.RegisterCallback<MouseOverEvent>((type) =>
                    {
                        SetBorders(button, 8, 16);
                        PencilStroke();
                    });
                    button.RegisterCallback<MouseOutEvent>((type) =>
                    {
                        SetBorders(button, 4, 20);
                    });
                    break;
                case MenuButtonType.toggle:
                    Toggle toggle = root.Q<Toggle>(interactables[iCtx].name);
                    toggle.RegisterValueChangedCallback(evt => ButtonPressed(pagename, interactables[iCtx].name, "", evt.newValue, 0, interactables[iCtx].sound));
                    toggle.RegisterCallback<MouseOverEvent>((type) =>
                    {
                        SetBorders(toggle, 8, 16);
                        PencilStroke();

                    });
                    toggle.RegisterCallback<MouseOutEvent>((type) =>
                    {
                        SetBorders(toggle, 4, 20);
                    });
                    break;
                case MenuButtonType.textField:
                    TextField field = root.Q<TextField>(interactables[iCtx].name);
                    field.RegisterValueChangedCallback(evt => ButtonPressed(pagename, interactables[iCtx].name, evt.newValue, false, 0, interactables[iCtx].sound));
                    field.RegisterCallback<MouseOverEvent>((type) =>
                    {
                        SetBorders(field, 8, 16);
                        PencilStroke();
                    });
                    field.RegisterCallback<MouseOutEvent>((type) =>
                    {
                        SetBorders(field, 4, 20);
                    });
                    break;
                case MenuButtonType.slider:
                    SliderInt sliderInt = root.Q<SliderInt>(interactables[iCtx].name);
                    if (sliderInt == null)
                    {
                        Slider slider = root.Q<Slider>(interactables[iCtx].name);
                        slider.RegisterValueChangedCallback(evt => ButtonPressed(pagename, interactables[iCtx].name, "", false, evt.newValue, interactables[iCtx].sound));
                        slider.RegisterCallback<MouseOverEvent>((type) =>
                        {
                            SetBorders(slider, 8, 16);
                            PencilStroke();
                        });
                        slider.RegisterCallback<MouseOutEvent>((type) =>
                        {
                            SetBorders(slider, 4, 20);
                        });
                    }
                    else
                    {
                        sliderInt.RegisterValueChangedCallback(evt => ButtonPressed(pagename, interactables[iCtx].name, "", false, evt.newValue, interactables[iCtx].sound));
                        sliderInt.RegisterCallback<MouseOverEvent>((type) =>
                        {
                            SetBorders(sliderInt, 8, 16);
                            PencilStroke();
                        });
                        sliderInt.RegisterCallback<MouseOutEvent>((type) =>
                        {
                            SetBorders(sliderInt, 4, 20);
                        });
                    }
                    break;
                case MenuButtonType.dropDown:
                    DropdownField ddf = root.Q<DropdownField>(interactables[iCtx].name);
                    ddf.RegisterValueChangedCallback(evt => ButtonPressed(pagename, interactables[iCtx].name, "", false, ddf.index, interactables[iCtx].sound));
                    ddf.RegisterCallback<MouseOverEvent>((type) =>
                    {
                        SetBorders(ddf, 8, 16);
                        PencilStroke();
                    });
                    ddf.RegisterCallback<MouseOutEvent>((type) =>
                    {
                        SetBorders(ddf, 4, 20);
                    });
                    break;
                default:
                    break;
            }
        }
    }

    void SetBorders(VisualElement e, int border, int margin)
    {
        e.style.borderTopWidth = border;
        e.style.borderLeftWidth = border;
        e.style.borderRightWidth = border;
        e.style.borderBottomWidth = border;
        e.style.paddingTop = margin;
        e.style.paddingLeft = margin;
        e.style.paddingRight = margin;
        e.style.paddingBottom = margin;
    }

    void RefreshServerList(bool clearRightPage)
    {
        VisualElement visList = leftMenu.rootVisualElement.Q<VisualElement>("IconHolder");
        if (clearRightPage)
        {
            rightMenu.rootVisualElement.Q<VisualElement>("Margins").style.display = DisplayStyle.None;
        }
        //Clear old children
        List<VisualElement> children = new List<VisualElement>();
        foreach (var child in visList.Children())
        {
            children.Add(child);
        }
        for (int i = 0; i < children.Count; i++)
        {
            visList.Remove(children[i]);
        }

        string added = "LocalServersAdded";
        string codeOrPort = "LocalServer";
        string serverName = "LocalServerName";
        if (onlineServerMenu)
        {
            added = "GlobalServersAdded";
            codeOrPort = "GlobalServer";
            serverName = "GlobalServerName";
            SetLabel("ServerTabLabel", "Online", false);
            SetLabel("ServerAddCode", "Join Code", false);
        }
        else
        {
            SetLabel("ServerTabLabel", "LAN", false);
            SetLabel("ServerAddCode", "Port #", false);
        }
        SetLabel("PageLabel", "Page " + currentServerPage + "/" + Mathf.FloorToInt((float)PlayerPrefs.GetInt(added) / 4.0f), false);

        if (PlayerPrefs.GetInt(added) > 0)
        {
            for (int i = currentServerPage * 4; i < Mathf.Min(PlayerPrefs.GetInt(added), (currentServerPage * 4) + 4); i++)
            {
                TemplateContainer myUI = serverIconVTA.Instantiate();
                string serverFinalName = PlayerPrefs.GetString(serverName + i);
                serverFinalName = serverFinalName.Substring(0, Math.Min(30, serverFinalName.Length));
                if (serverFinalName == "")
                {
                    serverFinalName = "Unloaded Server";
                }
                myUI.Q<Label>("ServerName").text = serverFinalName;
                if (onlineServerMenu)
                {
                    if (Convert.ToBoolean(PlayerPrefs.GetInt("Settings: ServerCode")))
                    {
                        myUI.Q<Label>("ServerCode").text = "";
                    }
                    else
                    {
                        myUI.Q<Label>("ServerCode").text = "(Code: " + PlayerPrefs.GetString(codeOrPort + i) + ") ";

                    }
                }
                else
                {
                    myUI.Q<Label>("ServerCode").text = "(Port: " + PlayerPrefs.GetInt(codeOrPort + i).ToString("00000") + ") ";
                }
                int e = i;

                Button button = myUI.Q<Button>("Button");
                button.clicked += () => SelectAndPingServer(e);
                button.RegisterCallback<MouseOverEvent>((type) =>
                {
                    PencilStroke();
                    SetBorders(button, 8, 16);
                });
                button.RegisterCallback<MouseOutEvent>((type) =>
                {
                    SetBorders(button, 4, 20);
                });
                visList.Add(myUI);
            }
        }
    }

    enum ServerCheck
    {
        none,
        running,
        fail,
        pass,
    }

    void SelectAndPingServer(int index)
    {
        if (serverCheck == ServerCheck.none)
        {
            currentServerSelected = index;
            rightMenu.rootVisualElement.Q<VisualElement>("Margins").style.display = DisplayStyle.Flex;
            serverCheck = ServerCheck.running;
            StartCoroutine(BeginServerPing(index));
        }
    }

    IEnumerator BeginServerPing(int index)
    {
        SetLabel("ServerName", "Loading", true);
        SetLabel("Gamemode", "Attempting Allocation", true);
        SetPicture("MapBackground", noMapTexture,Color.white, true);
        HideElement("Delete", true, true);
        HideElement("Play", true, true);

        m_NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes("P");

        if (onlineServerMenu)
        {
            PingOnlineServer(PlayerPrefs.GetString("GlobalServer" + index));
        }
        else
        {
            m_NetworkManager.GetComponent<UnityTransport>().ConnectionData.Port = (ushort)PlayerPrefs.GetInt("LocalServer" + index);
            m_NetworkManager.StartClient();
        }

        int secondCount = 0;
        while (true)
        {
            if (serverCheck == ServerCheck.pass || serverCheck == ServerCheck.fail)
            {
                break;
            }
            SetLabel("Gamemode", "Connecting To Server", true);
            string label = "Loading ";
            for (int i = 0; i < (secondCount + 1) % 4; i++)
            {
                label += ". ";
            }
            SetLabel("ServerName", label, true);

            if (secondCount >= 30)
            {
                if (serverCheck == ServerCheck.running)
                {
                    serverCheck = ServerCheck.fail;
                    serverFailMessage = "Could Not Connect To Server.";
                }
                break;
            }
            secondCount++;
            yield return new WaitForSeconds(0.33333f);
        }

        if (serverCheck == ServerCheck.fail)
        {
            if (m_NetworkManager != null && m_NetworkManager.isActiveAndEnabled)
            {
                m_NetworkManager.Shutdown();
            }
            SetLabel("ServerName", "Error", true);
            SetLabel("Gamemode", serverFailMessage, true);
            HideElement("Delete",false,true);
        }
        if (serverCheck == ServerCheck.pass)
        {
            if (onlineServerMenu)
            {
                PlayerPrefs.SetString("JoinCode", PlayerPrefs.GetString("GlobalServer" + index));
            }
            HideElement("Delete", false, true);
            HideElement("Play", false, true);
        }
        serverCheck = ServerCheck.none;
    }

    async void PingOnlineServer(string joinCode)
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
            serverCheck = ServerCheck.pass;
        }
        catch (RelayServiceException e)
        {
            serverCheck = ServerCheck.fail;
            serverFailMessage = e.Message;
        }
    }


    private void OnClientDisconnectCallback(ulong obj)
    {
        if (!m_NetworkManager.IsServer && m_NetworkManager.DisconnectReason != string.Empty)
        {
            serverCheck = ServerCheck.pass;
            string payload = m_NetworkManager.DisconnectReason;

            if (payload[0] == 'P')
            {
                string[] limiters = payload.Substring(1, payload.Length - 1).Split("😂", StringSplitOptions.None);
                if (limiters.Length == 5)
                {
                    if (Convert.ToUInt16(limiters[4]) == m_NetworkManager.NetworkConfig.ProtocolVersion)
                    {
                        string description = limiters[0] + "/" + limiters[1];
                        if (limiters[2] == "")
                        {
                            SetLabel("ServerName", "Unnamed Server", true);
                        }
                        else
                        {
                            string rename = limiters[2];
                            if (rename.Length > 20)
                            {
                                rename = rename.Substring(0, 20);
                            }
                            rename = string.Join("", rename.ToCharArray().Where(x => ((int)x) < 127));
                            SetLabel("ServerName", rename, true);
                            PlayerPrefs.SetString("LocalServerName" + currentServerSelected, limiters[2]);
                            RefreshServerList(false);
                        }
                        int currentMap = Convert.ToInt16(limiters[3]);
                        string mapName = "Unknown Map";
                        if (currentMap < maps.Count())
                        {
                            mapName = maps[currentMap].mapName;
                            SetPicture("MapBackground", maps[currentMap].image, Color.white, true);
                        }
                        description += '\n' + mapName;
                        PlayerPrefs.SetInt("ServerMapName", currentMap);
                    }
                    else
                    {
                        if (Convert.ToUInt16(limiters[4]) > m_NetworkManager.NetworkConfig.ProtocolVersion)
                        {
                            SetLabel("ServerName", "Error", true);
                            SetLabel("Gamemode", "Server is running a newer version.", true);
                        }
                        else
                        {
                            SetLabel("ServerName", "Error", true);
                            SetLabel("Gamemode", "Server is running an older version.", true);
                        }
                    }
                }
                else
                {
                    SetLabel("ServerName", "Error", true);
                    SetLabel("Gamemode", "Server information corrupted or not formatted correctly.", true);
                }
            }
            else if (payload[0] == 'E')
            {
                SetLabel("ServerName", "Error", true);
                SetLabel("Gamemode", payload.Substring(1, payload.Length - 1), true);
            }
            else
            {
                SetLabel("ServerName", "Error", true);
                SetLabel("Gamemode", "Unknown Error: " + payload, true);
            }
        }
    }


    void AddServer(string port)
    {
        if (onlineServerMenu)
        {
            PlayerPrefs.SetInt("GlobalServersAdded", PlayerPrefs.GetInt("GlobalServersAdded") + 1);
            try
            {
                PlayerPrefs.SetString("GlobalServer" + (PlayerPrefs.GetInt("GlobalServersAdded") - 1), port);
                PlayerPrefs.SetString("GlobalServerName" + (PlayerPrefs.GetInt("GlobalServersAdded") - 1), "");
            }
            catch (Exception)
            {
                PlayerPrefs.SetInt("GlobalServersAdded", PlayerPrefs.GetInt("GlobalServersAdded") - 1);
            }
        }
        else
        {
            PlayerPrefs.SetInt("LocalServersAdded", PlayerPrefs.GetInt("LocalServersAdded") + 1);
            try
            {
                PlayerPrefs.SetInt("LocalServer" + (PlayerPrefs.GetInt("LocalServersAdded") - 1), (int)ushort.Parse(port));
                PlayerPrefs.SetString("LocalServerName" + (PlayerPrefs.GetInt("LocalServersAdded") - 1), "");

            }
            catch (Exception)
            {
                PlayerPrefs.SetInt("LocalServersAdded", PlayerPrefs.GetInt("LocalServersAdded") - 1);
            }
        }
        RefreshServerList(true);
    }

    void RemoveServer(int index)
    {
        if (!onlineServerMenu)
        {
            PlayerPrefs.SetInt("LocalServersAdded", PlayerPrefs.GetInt("LocalServersAdded") - 1);
            for (int i = index; i < PlayerPrefs.GetInt("LocalServersAdded"); i++)
            {
                PlayerPrefs.SetInt("LocalServer" + i, PlayerPrefs.GetInt("LocalServer" + (i + 1)));
            }
        }
        else
        {
            PlayerPrefs.SetInt("GlobalServersAdded", PlayerPrefs.GetInt("GlobalServersAdded") - 1);
            for (int i = index; i < PlayerPrefs.GetInt("LocalServersAdded"); i++)
            {
                PlayerPrefs.SetString("GlobalServer" + i, PlayerPrefs.GetString("GlobalServer" + (i + 1)));
            }
        }
        RefreshServerList(true);
    }


    void ButtonPressed(string page, string button, string valueString, bool valueBool, float valueFloat, MenuButtonSound sound)
    {
        if (!flippingPage)
        {
            switch (sound)
            {
                case MenuButtonSound.penFlick:
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Pen Flick"), 0.6f);
                    break;
                case MenuButtonSound.pageTurn:
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Page Flip"), 0.5f);
                    break;
                case MenuButtonSound.typewriter:
                    int num = 0;
                    if (valueString != "")
                    {
                        num = valueString[valueString.Length - 1] % 13;
                    }
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Typewriter" + num), UnityEngine.Random.Range(1.0f, 1.2f));
                    break;
                default:
                    break;
            }
            Debug.Log("Button " + button + " of " + page + " pressed");
            switch (page)
            {
                case "Title Left":
                    switch (button)
                    {
                        case "JoinGame":
                            SwitchPage(6);
                            onlineServerMenu = true;
                            currentServerSelected = -1;
                            currentServerPage = 0;
                            RefreshServerList(true);
                            break;
                        case "CreateGame":
                            SwitchPage(4);
                            List<string> mapnames = new List<string>();
                            for (int i = 0; i < maps.Length; i++)
                            {
                                mapnames.Add(maps[i].mapName);
                            }
                            int mapIndex = PlayerPrefs.GetInt("ServerMapName");
                            SetDropdown("SelectMap", mapnames, mapIndex, false);
                            SetLabel("MapName", maps[mapIndex].mapName, true);
                            SetLabel("Gamemode", "Gamemode: " + maps[mapIndex].gameMode, true);
                            SetPicture("MapBackground", maps[mapIndex].image, Color.white, true);
                            SetTextField("ServerName", PlayerPrefs.GetString("ServerName"), false);
                            SetTextField("SelectPort", PlayerPrefs.GetInt("ServerPort").ToString(), false);
                            SetDropdown("SelectTeam1", null, PlayerPrefs.GetInt("Team1Setting"), false);
                            SetDropdown("SelectTeam2", null, PlayerPrefs.GetInt("Team2Setting"), false);
                            SetSlider("MaxPlayers", Mathf.Max(PlayerPrefs.GetInt("ServerMaxPlayers"), 1), false);
                            SetToggle("CreateOnline", Convert.ToBoolean(PlayerPrefs.GetInt("CreateOnline")), false);
                            SetToggle("SpawnBots", Convert.ToBoolean(PlayerPrefs.GetInt("SpawnBotsInEmpty")), false);

                            if (PlayerPrefs.GetInt("CreateOnline") == 0)
                            {
                                PlayerPrefs.SetString("Connection Setting", "Create LAN Server");
                                SetLabel("ButtonWarning", "LAN players can connect by server port", true);
                            }
                            else
                            {
                                PlayerPrefs.SetString("Connection Setting", "Create Online Server");
                                SetLabel("ButtonWarning", "An invite code will be copied to your clipboard upon start", true);
                            }
                            break;
                        case "Customization":
                            DisplayCustomization();
                            break;
                        case "Statistics":
                            DisplayStatistics();
                            break;
                        case "Settings":
                            DisplaySettings();
                            break;
                        case "StartVR":
                            StartCoroutine(StartXR());
                            break;
                        case "Exit":
                            Application.Quit();
                            break;
                        default:
                            break;
                    }
                    break;
                case "Pause Left":
                    switch (button)
                    {
                        case "ServerInfo":
                            
                            break;
                        case "Customization":
                            DisplayCustomization();
                            break;
                        case "Statistics":
                            DisplayStatistics();
                            break;
                        case "Settings":
                            DisplaySettings();
                            break;
                        case "StartVR":
                            StartCoroutine(StartXR());
                            break;
                        case "Exit":
                            cosmetics.transform.parent.GetComponent<GlobalManager>().DisconnectToTitleScreen(false);
                            break;
                        default:
                            break;
                    }
                    break;
                case "Cust Left":
                    switch (button)
                    {
                        case "ClassLeft":
                            currentCustClass = Mathf.Max(currentCustClass - 1, 0);
                            SetLabel("ClassLabel", "The " + System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Enum.GetName(typeof(ClassList), currentCustClass)), false);
                            SetCharacterVisibility();
                            currentCustCosmType--;
                            currentCustCosmType = CheckCosmeticType(1);
                            CreateCosmeticPage();
                            break;
                        case "ClassRight":
                            currentCustClass = Mathf.Min(currentCustClass + 1, 9);
                            SetLabel("ClassLabel", "The " + System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Enum.GetName(typeof(ClassList), currentCustClass)), false);
                            SetCharacterVisibility();
                            currentCustCosmType--;
                            currentCustCosmType = CheckCosmeticType(1);
                            CreateCosmeticPage();
                            break;
                        case "TeamLeft":
                            currentCustTeam = Mathf.Max(currentCustTeam - 1, 0);
                            SetPicture("TeamIcon", teamIcons[currentCustTeam], teamColors[currentCustTeam], false);
                            CreateCosmeticPage();
                            UpdateTeamColor();
                            break;
                        case "TeamRight":
                            currentCustTeam = Mathf.Min(currentCustTeam + 1, 8);
                            SetPicture("TeamIcon", teamIcons[currentCustTeam], teamColors[currentCustTeam], false);
                            CreateCosmeticPage();
                            UpdateTeamColor();
                            break;
                        case "LoadoutLeft":
                            currentCustLoadout = Mathf.Max(currentCustLoadout - 1, 0);
                            SetLabel("LoadoutLabel", "Var " + (char)('A' + currentCustLoadout % 26), false);
                            SetCharacterVisibility();
                            break;
                        case "LoadoutRight":
                            currentCustLoadout = Mathf.Min(currentCustLoadout + 1, 25);
                            SetLabel("LoadoutLabel", "Var " + (char)('A' + currentCustLoadout % 26), false);
                            SetCharacterVisibility();
                            break;
                        case "CosmeticLeft":
                            currentCustCosmType = CheckCosmeticType(-1);
                            CreateCosmeticPage();
                            break;
                        case "CosmeticRight":
                            currentCustCosmType = CheckCosmeticType(1);
                            CreateCosmeticPage();
                            break;
                        case "PageLeft":
                            currentCustPage = Mathf.Max(currentCustPage - 1, 0);
                            CreateCosmeticPage();
                            SetLabel("PageLabel", "Page " + currentCustPage.ToString(), false);
                            break;
                        case "PageRight":
                            currentCustPage = Mathf.Min(currentCustPage + 1, 10);
                            CreateCosmeticPage();
                            SetLabel("PageLabel", "Page " + currentCustPage.ToString(), false);
                            break;
                        case "Back":
                            SwitchPage(0);
                            customizeMenuCamera.SetActive(false);
                            break;
                        default:
                            break;
                    }
                    break;
                case "Cust Right":
                    switch (button)
                    {
                        case "CharacterRotate":
                            for (int i = 0; i < playerModels.Length; i++)
                            {
                                playerModels[i].transform.localEulerAngles = new Vector3(0, valueFloat, 0);
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case "Join Left":
                    switch (button)
                    {
                        case "ServerTabLeft":
                            onlineServerMenu = !onlineServerMenu;
                            currentServerPage = 0;
                            RefreshServerList(true);
                            break;
                        case "ServerTabRight":
                            onlineServerMenu = !onlineServerMenu;
                            currentServerPage = 0;
                            RefreshServerList(true);
                            break;
                        case "PageLeft":
                            currentServerPage = Mathf.Max(0, currentServerPage - 1);
                            string addedR = "LocalServersAdded";
                            if (onlineServerMenu)
                            {
                                addedR = "GlobalServersAdded";
                            }
                            RefreshServerList(true);
                            break;
                        case "PageRight":
                            string addedL = "LocalServersAdded";
                            if (onlineServerMenu)
                            {
                                addedL = "GlobalServersAdded";
                            }
                            currentServerPage = Mathf.Min(Mathf.FloorToInt((float)PlayerPrefs.GetInt(addedL) / 4.0f), currentServerPage + 1);
                            RefreshServerList(true);
                            break;
                        case "ServerAdd":
                            AddServer(leftMenu.rootVisualElement.Q<TextField>("ServerAddCode").value);
                            break;
                        case "Back":
                            SwitchPage(0);
                            break;
                        default:
                            break;
                    }
                    break;
                case "Join Right":
                    switch (button)
                    {
                        case "Delete":
                            RemoveServer(currentServerSelected);
                            RefreshServerList(true);
                            break;
                        case "Play":
                            SwitchPage(5);
                            int final = PlayerPrefs.GetInt("ServerMapName");
                            SetLabel("MapName", maps[final].mapName, true);
                            SetLabel("Gamemode", "Gamemode: " + maps[final].gameMode, true);
                            SetPicture("MapBackground", maps[final].image, Color.white, true);
                            StartCoroutine(LoadMap());
                            break;
                        default:
                            break;
                    }
                    break;
                case "Stats Left":
                    switch (button)
                    {
                        case "Back":
                            SwitchPage(0);
                            break;
                        default:
                            break;
                    }
                    break;
                case "Settings Left":
                    switch (button)
                    {
                        case "PlayerName":
                            PlayerPrefs.SetString("Settings: Player Name", valueString);
                            break;
                        case "Vsync":
                            PlayerPrefs.SetInt("Settings: Vsync", Convert.ToInt32(valueBool));
                            if (valueBool)
                            {
                                QualitySettings.vSyncCount = 1;
                            }
                            else
                            {
                                QualitySettings.vSyncCount = 0;
                            }
                            break;
                        case "Windowed":
                            PlayerPrefs.SetInt("Settings: Windowed", Convert.ToInt32(valueBool));
                            if (valueBool)
                            {
                                Screen.fullScreenMode = FullScreenMode.Windowed;
                            }
                            else
                            {
                                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                            }
                            break;
                        case "PlayerHeight":
                            if (valueFloat < 0.9144f || valueFloat > 2.1336) //Value is 3 feet, 7 Feet
                            {
                                valueFloat = 1.524f; //Value is 5 feet
                            }
                            PlayerPrefs.SetFloat("Settings: PlayerHeight", valueFloat);
                            break;
                        case "FOV":
                            PlayerPrefs.SetFloat("Settings: FOV", valueFloat);
                            if (optionalPlayer != null)
                            {
                                optionalPlayer.GetTracker().UpdateFOV();
                            }
                            break;
                        case "ServerCode":
                            PlayerPrefs.SetInt("Settings: ServerCode", Convert.ToInt32(valueBool));
                            break;
                        case "Back":
                            SwitchPage(0);
                            break;
                        default:
                            break;
                    }
                    break;
                case "SvrSett Left":
                    switch (button)
                    {
                        case "SelectName":
                            PlayerPrefs.SetString("ServerName", valueString);
                            break;
                        case "SelectMap":
                            PlayerPrefs.SetInt("ServerMapName", (int)valueFloat);
                            SetLabel("MapName", maps[(int)valueFloat].mapName, true);
                            SetLabel("Gamemode", "Gamemode: " + maps[(int)valueFloat].gameMode, true);
                            SetPicture("MapBackground", maps[(int)valueFloat].image, Color.white, true);
                            break;
                        case "SelectPort":
                            int final;
                            try
                            {
                                final = Convert.ToInt32(valueString);
                            }
                            catch (Exception)
                            {
                                final = 7777;
                            }
                            PlayerPrefs.SetInt("ServerPort", final);
                            SetTextField("SelectPort", final.ToString(), false);
                            break;
                        case "CreateOnline":
                            PlayerPrefs.SetInt("CreateOnline", Convert.ToInt32(valueBool));
                            if (!valueBool)
                            {
                                SetLabel("ButtonWarning", "LAN players can connect by server port", true);
                                PlayerPrefs.SetString("Connection Setting", "Create LAN Server");
                            }
                            else
                            {
                                SetLabel("ButtonWarning", "An invite code will be copied to your clipboard upon start", true);
                                PlayerPrefs.SetString("Connection Setting", "Create Online Server");
                            }
                            break;
                        case "MaxPlayers":
                            PlayerPrefs.SetInt("ServerMaxPlayers", (int)valueFloat);
                            break;
                        case "SelectTeam1":
                            PlayerPrefs.SetInt("Team1Setting", (int)valueFloat);
                            break;
                        case "SelectTeam2":
                            PlayerPrefs.SetInt("Team2Setting", (int)valueFloat);
                            break;
                        case "SpawnBots":
                            PlayerPrefs.SetInt("SpawnBotsInEmpty", Convert.ToInt32(valueBool));
                            break;
                        case "Back":
                            SwitchPage(0);
                            break;
                        default:
                            break;
                    }
                    break;
                case "SvrSett Right":
                    switch (button)
                    {
                        case "CreateServer":
                            SwitchPage(5);
                            int final = PlayerPrefs.GetInt("ServerMapName");
                            SetLabel("MapName", maps[final].mapName, true);
                            SetLabel("Gamemode", "Gamemode: " + maps[final].gameMode, true);
                            SetPicture("MapBackground", maps[final].image, Color.white, true);
                            StartCoroutine(LoadMap());
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
        }
    }

    int CheckCosmeticType(int adjust)
    {
        int neededValue = Mathf.Clamp(currentCustCosmType + adjust, 0, 10);
        List<Cosmetic> currentCosmetics = cosmetics.GetClassCosmetics((ClassList)currentCustClass);
        while (true)
        {
            if (neededValue < 0 || neededValue > 10)
            {
                return Mathf.Clamp(currentCustCosmType, 0, 10);
            }
            int cosmeticsInRegion = 0;
            for (int i = 0; i < currentCosmetics.Count; i++)
            {
                if (currentCosmetics[i].region == (EquipRegion)neededValue)
                {
                    cosmeticsInRegion++;
                }
            }
            if (cosmeticsInRegion > 0)
            {
                return Mathf.Clamp(neededValue, 0, 10);
            }
            neededValue += adjust;
        }

    }

    void DisplayCustomization()
    {
        SwitchPage(1);
        currentCustClass = 3;
        currentCustTeam = UnityEngine.Random.Range(0,9);
        currentCustPage = 0;
        currentCustLoadout = 0;
        currentCustCosmType = -1;
        currentCustCosmType = CheckCosmeticType(1);
        SetCharacterVisibility();
        SetPicture("TeamIcon", teamIcons[currentCustTeam], teamColors[currentCustTeam], false);
        SetLabel("CosmeticTypeLabel", cosmeticTypes[currentCustCosmType].PadRight(10), false);
        SetLabel("ClassLabel", "The " + System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Enum.GetName(typeof(ClassList), currentCustClass)), false);
        SetLabel("LoadoutLabel", "Var " + (char)('A' + currentCustLoadout % 26), false);
        SetLabel("PageLabel", "Page " + currentCustPage.ToString(), false);
        UpdateTeamColor();
        customizeMenuCamera.SetActive(true);
        CreateCosmeticPage();
    }

    void DisplaySettings()
    {
        SwitchPage(3);
        string finalName = PlayerPrefs.GetString("Settings: Player Name");
        if (finalName == "")
        {
            finalName = "Intern #" + UnityEngine.Random.Range(0, 1000000);
            PlayerPrefs.SetString("Settings: Player Name", finalName);
        }
        SetTextField("PlayerName", finalName, false);
        SetToggle("Vsync", Convert.ToBoolean(PlayerPrefs.GetInt("Settings: Vsync")), false);
        SetToggle("Windowed", Convert.ToBoolean(PlayerPrefs.GetInt("Settings: Windowed")), false);
        SetSlider("PlayerHeight", PlayerPrefs.GetFloat("Settings: PlayerHeight"), false);
        SetToggle("ServerCode", Convert.ToBoolean(PlayerPrefs.GetInt("Settings: ServerCode")), false);
        SetSlider("FOV", PlayerPrefs.GetFloat("Settings: FOV"), false);
    }

    void DisplayStatistics()
    {
        SwitchPage(2);
        SetLabel("Statistics",
                                "TOTAL STATS" +
                                "\nTime Playing: " + TimeSpan.FromSeconds(PlayerPrefs.GetFloat("Achievement: Total Match Runtime")).ToString("hh':'mm") +
                                "\nDamage: " + PlayerPrefs.GetFloat("Achievement: Total Damage") +
                                "\nKills: " + PlayerPrefs.GetFloat("Achievement: Total Kills") +
                                "\nDeaths: " + PlayerPrefs.GetFloat("Achievement: Total Deaths") +
                                "\nSuicides: " + PlayerPrefs.GetFloat("Achievement: Total Suicides") +
                                "\nHealing: " + PlayerPrefs.GetFloat("Achievement: Total Healing") +
                                "\nSelf-Healing: " + PlayerPrefs.GetFloat("Achievement: Total Self-Healing") +
                                "\nDistance Walked: " + (int)PlayerPrefs.GetFloat("Achievement: Total Walking Distance") + " m" +
                                "\nAir Travel: " + (int)PlayerPrefs.GetFloat("Achievement: Total Air Travel") + " m" +
                                "\nAir-Time: " + (int)PlayerPrefs.GetFloat("Achievement: Total Air-Time") + " sec" +
                                "\n\nTOTAL KILLED" +
                                "\nLaborers: " + PlayerPrefs.GetFloat("Achievement: Total Laborers Killed") +
                                "\nWood Workers: " + PlayerPrefs.GetFloat("Achievement: Total Wood Workers Killed") +
                                "\nDevelopers: " + PlayerPrefs.GetFloat("Achievement: Total Developers Killed") +
                                "\nProgrammers: " + PlayerPrefs.GetFloat("Achievement: Total Programmers Killed") +
                                "\nComputers: " + PlayerPrefs.GetFloat("Achievement: Total Computers Killed") +
                                "\nFabricators: " + PlayerPrefs.GetFloat("Achievement: Total Fabricators Killed") +
                                "\nArtists: " + PlayerPrefs.GetFloat("Achievement: Total Artists Killed") +
                                "\nFreelancers: " + PlayerPrefs.GetFloat("Achievement: Total Freelancers Killed") +
                                "\nCraftsmen: " + PlayerPrefs.GetFloat("Achievement: Total Craftsmen Killed") +
                                "\nManagers: " + PlayerPrefs.GetFloat("Achievement: Total Managers Killed")
                                , false);
    }

    void InsertCosmetic(int cosmeticValue)
    {
        if (PlayerPrefs.GetInt("Loadout " + currentCustClass + " Var: " + currentCustLoadout + " Type: " + currentCustCosmType) == cosmeticValue + 1)
        {
            return;
        }
        if (cosmeticValue != -1)
        {
            switch (cosmetics.FindCosmetic((ClassList)currentCustClass, cosmeticValue).weight)
            {
                case CosmeticWeight.small:
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Backpack_Small " + UnityEngine.Random.Range(0, 6)), 0.8f);
                    break;
                case CosmeticWeight.medium:
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Backpack_Medium " + UnityEngine.Random.Range(0, 4)), 0.8f);
                    break;
                case CosmeticWeight.large:
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Backpack_Large " + UnityEngine.Random.Range(0, 5)), 0.8f);
                    break;
                case CosmeticWeight.huge:
                    aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Backpack_Huge 0"), 0.8f);
                    break;
                default:
                    break;
            }
        }
        else
        {
            aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Pen Flick"), 0.8f);
        }
        PlayerPrefs.SetInt("Loadout " + currentCustClass + " Var: " + currentCustLoadout + " Type: " + currentCustCosmType, cosmeticValue + 1);
        SetCharacterVisibility();
    }

    void CreateCosmeticPage()
    {
        VisualElement visList = leftMenu.rootVisualElement.Q<VisualElement>("IconHolder");

        //Clear old children
        List<VisualElement> children = new List<VisualElement>();
        foreach (var child in visList.Children())
        {
            children.Add(child);
        }
        for (int i = 0; i < children.Count; i++)
        {
            visList.Remove(children[i]);
        }

        //Create "None" Icon
        List<Cosmetic> currentCosmetics = cosmetics.GetClassCosmetics((ClassList)currentCustClass);
        bool noneIcon = true;
        for (int i = 0; i < currentCosmetics.Count; i++)
        {
            if (currentCosmetics[i].region == (EquipRegion)currentCustCosmType && currentCosmetics[i].stock == StockCosmetic.stock)
            {
                noneIcon = false;
            }
        }
        if (noneIcon)
        {
            TemplateContainer myUI = cosmeticIconVTA.Instantiate();
            myUI.Q<VisualElement>("Icon").style.backgroundImage = new StyleBackground(noneTexture);

            Button button = myUI.Q<Button>("Button");
            button.clicked += () => InsertCosmetic(-1);
            button.RegisterCallback<MouseOverEvent>((type) =>
            {
                PencilStroke();
                SetBorders(button, 8, 16);


            });
            button.RegisterCallback<MouseOutEvent>((type) =>
            {
                SetBorders(button, 4, 20);

            });
            visList.Add(myUI);
        }

        //Create new icons
        for (int i = 0; i < currentCosmetics.Count; i++)
        {
            if (currentCosmetics[i].region == (EquipRegion)currentCustCosmType)
            {
                TemplateContainer myUI = cosmeticIconVTA.Instantiate();
                myUI.Q<VisualElement>("Icon").style.backgroundImage = new StyleBackground(currentCosmetics[i].icon);

                int finalVal = i;
                Button button = myUI.Q<Button>("Button");
                button.clicked += () => InsertCosmetic(finalVal);
                button.RegisterCallback<MouseOverEvent>((type) =>
                {
                    PencilStroke();
                    SetBorders(button, 8, 16);

                });
                button.RegisterCallback<MouseOutEvent>((type) =>
                {
                    SetBorders(button, 4, 20);
                });
                visList.Add(myUI);
            }
        }
        SetLabel("CosmeticTypeLabel", cosmeticTypes[currentCustCosmType].PadRight(10), false);
    }

    void PencilStroke()
    {
        if (!flippingPage && soundTimer <= 0)
        {
            soundTimer = 0.1f;
            aus.PlayOneShot(Resources.Load<AudioClip>("Sounds/Menu/Pencil Stroke " + UnityEngine.Random.Range(0, 21)), UnityEngine.Random.Range(0.2f, 0.3f));
        }
    }

    void HideElement(string element, bool hide, bool isRightPage)
    {
        VisualElement v = leftMenu.rootVisualElement;
        if (isRightPage)
        {
            v = rightMenu.rootVisualElement;
        }
        if(hide)
        {
            v.Q<VisualElement>(element).style.display = DisplayStyle.None;

        }
        else
        {
            v.Q<VisualElement>(element).style.display = DisplayStyle.Flex;
        }
    }

    void SetLabel(string element, string text, bool isRightPage)
    {
        VisualElement v = leftMenu.rootVisualElement;
        if (isRightPage)
        {
            v = rightMenu.rootVisualElement;
        }
        Label r = v.Q<Label>(element);
        if (r == null)
        {
            v.Q<TextField>(element).label = text;
        }
        else
        {
            r.text = text;
        }
    }

    void SetToggle(string element, bool value, bool isRightPage)
    {
        if (isRightPage)
        {
            rightMenu.rootVisualElement.Q<Toggle>(element).SetValueWithoutNotify(value);
        }
        else
        {
            leftMenu.rootVisualElement.Q<Toggle>(element).SetValueWithoutNotify(value);
        }
    }
    void SetTextField(string element, string value, bool isRightPage)
    {
        if (isRightPage)
        {
            rightMenu.rootVisualElement.Q<TextField>(element).SetValueWithoutNotify(value);
        }
        else
        {
            leftMenu.rootVisualElement.Q<TextField>(element).SetValueWithoutNotify(value);
        }
    }

    void SetDropdown(string element, List<string> value, int index, bool isRightPage)
    {
        DropdownField d;
        if (isRightPage)
        {
            d = rightMenu.rootVisualElement.Q<DropdownField>(element);

        }
        else
        {
            d = leftMenu.rootVisualElement.Q<DropdownField>(element);
        }
        if (value != null)
        {
            d.choices = value;
        }
        d.index = index;
    }

    void SetSlider(string element, float index, bool isRightPage)
    {
        Slider d;
        if (isRightPage)
        {
            d = rightMenu.rootVisualElement.Q<Slider>(element);
            if (d == null)
            {
                SliderInt di = rightMenu.rootVisualElement.Q<SliderInt>(element);
                di.value = (int)index;
                return;
            }
        }
        else
        {
            d = leftMenu.rootVisualElement.Q<Slider>(element);
            if (d == null)
            {
                SliderInt di = leftMenu.rootVisualElement.Q<SliderInt>(element);
                di.value = (int)index;
                return;
            }
        }
        d.value = index;
    }

    void SetPicture(string element, Texture2D bgImage, Color color, bool isRightPage)
    {
        if (isRightPage)
        {
            rightMenu.rootVisualElement.Q<VisualElement>(element).style.backgroundImage = new StyleBackground(bgImage);
            rightMenu.rootVisualElement.Q<VisualElement>(element).style.backgroundColor = color;
        }
        else
        {
            leftMenu.rootVisualElement.Q<VisualElement>(element).style.backgroundImage = new StyleBackground(bgImage);
            leftMenu.rootVisualElement.Q<VisualElement>(element).style.backgroundColor = color;
        }
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


    public void SetCharacterVisibility()
    {
        List<int> newCosInts = new List<int>();

        for (int i = 0; i < 11; i++)
        {
            int check = PlayerPrefs.GetInt("Loadout " + currentCustClass + " Var: " + currentCustLoadout + " Type: " + i) - 1;
            if (check >= 0)
            {
                newCosInts.Add(check);
            }
        }

        cosmeticInts = newCosInts.ToArray();
        SetupCosmetics(cosmeticInts);

        while (currentCharMeshes.Count > 0)
        {
            Destroy(currentCharMeshes[0]);
            currentCharMeshes.RemoveAt(0);
        }
        for (int i = 0; i < playerModels.Length; i++)
        {
            if (i == (int)currentCustClass)
            {
                Debug.Log(i + " a " + currentCustClass);

                List<Cosmetic> classCosmetics = cosmetics.GetClassCosmetics((ClassList)currentCustClass);

                //Reveal class
                playerModels[i].SetActive(true);

                //Apply Bodygroup Hiding
                Transform t = playerModels[i].transform;
                SetMeshVis(t, "Skin Arm L", true, false);
                SetMeshVis(t, "Skin Arm R", true, false);
                SetMeshVis(t, "Skin Body", true, false);
                SetMeshVis(t, "Skin Foot L", true, false);
                SetMeshVis(t, "Skin Foot R", true, false);
                SetMeshVis(t, "Skin Hand L", true, false);
                SetMeshVis(t, "Skin Hand R", true, false);
                SetMeshVis(t, "Skin Head", true, false);
                SetMeshVis(t, "Skin Leg L", true, false);
                SetMeshVis(t, "Skin Leg R", true, false);

                //Get Combination Hide Bodygroups Enum
                BodyGroups combined = new BodyGroups();
                for (int e = 0; e < cosmeticInts.Length; e++)
                {
                    combined = combined | classCosmetics[cosmeticInts[e]].hideBodyGroups;
                }

                if (combined.HasFlag(BodyGroups.armL))
                {
                    SetMeshVis(t, "Skin Arm L", false, false);
                }
                if (combined.HasFlag(BodyGroups.armR))
                {
                    SetMeshVis(t, "Skin Arm R", false, false);
                }
                if (combined.HasFlag(BodyGroups.body))
                {
                    SetMeshVis(t, "Skin Body", false, false);
                }
                if (combined.HasFlag(BodyGroups.footL))
                {
                    SetMeshVis(t, "Skin Foot L", false, false);
                }
                if (combined.HasFlag(BodyGroups.footR))
                {
                    SetMeshVis(t, "Skin Foot R", false, false);
                }
                if (combined.HasFlag(BodyGroups.handL))
                {
                    SetMeshVis(t, "Skin Hand L", false, false);
                }
                if (combined.HasFlag(BodyGroups.handR))
                {
                    SetMeshVis(t, "Skin Hand R", false, false);
                }
                if (combined.HasFlag(BodyGroups.head))
                {
                    SetMeshVis(t, "Skin Head", false, false);
                }
                if (combined.HasFlag(BodyGroups.legL))
                {
                    SetMeshVis(t, "Skin Leg L", false, false);
                }
                if (combined.HasFlag(BodyGroups.legR))
                {
                    SetMeshVis(t, "Skin Leg R", false, false);
                }

                //Apply Cosmetics
                for (int e = 0; e < cosmeticInts.Length; e++)
                {
                    ApplyCosmetics(classCosmetics[cosmeticInts[e]].prefab, t);
                }

            }
            else
            {
                //Hide other classes
                playerModels[i].SetActive(false);
            }
        }
        UpdateTeamColor();
    }

    void SetupCosmetics(int[] classCosmetics)
    {
        List<Cosmetic> stockCosmetics = cosmetics.GetClassCosmetics((ClassList)currentCustClass);
        List<int> cosmeticIntList = cosmeticInts.ToList<int>();
        for (int i = 0; i < classCosmetics.Length; i++)
        {
            if (classCosmetics[i] < stockCosmetics.Count)
            {
                Cosmetic cm = stockCosmetics[classCosmetics[i]];
                bool isDupeEquipRegion = false;
                for (int e = 0; e < cosmeticIntList.Count; e++)
                {
                    if (stockCosmetics[cosmeticIntList[e]].region == cm.region)
                    {
                        isDupeEquipRegion = true;
                    }
                }
                if (!isDupeEquipRegion)
                {
                    cosmeticIntList.Add(classCosmetics[i]);
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
        cosmeticInts = cosmeticIntList.ToArray();
    }

    void ApplyCosmetics(GameObject prefab, Transform t)
    {
        GameObject g = new GameObject(prefab.name);
        g.layer = 9;
        g.transform.parent = t;
        SkinnedMeshRenderer targetSkin = g.AddComponent<SkinnedMeshRenderer>();
        SkinnedMeshRenderer originalSkin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        targetSkin.SetSharedMaterials(originalSkin.sharedMaterials.ToList<Material>());
        targetSkin.sharedMesh = originalSkin.sharedMesh;
        targetSkin.rootBone = t.Find("Armature").GetChild(0);
        currentCharMeshes.Add(g);

        Transform[] newBones = new Transform[originalSkin.bones.Length];

        int a = 0;
        foreach (var originalBone in originalSkin.bones)
        {

            foreach (var newBone in targetSkin.rootBone.GetComponentsInChildren<Transform>())
            {
                if (newBone.name == originalBone.name)
                {
                    newBones[a] = newBone;
                    continue;
                }
            }
            a++;
        }
        targetSkin.bones = newBones;
    }

    void SetMeshVis(Transform trans, string meshName, bool set, bool alwaysUpdate)
    {
        GameObject g = trans.Find(meshName).gameObject;
        SkinnedMeshRenderer r = g.GetComponent<SkinnedMeshRenderer>();
        if (r != null)
        {
            r.updateWhenOffscreen = alwaysUpdate;
        }
        g.SetActive(set);
    }

    void UpdateTeamColor()
    {
        //Player
        float teamFinal = (float)currentCustTeam + 1;
        Renderer[] meshes = customizeMenuCamera.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < meshes.Length; i++)
        {
            Material[] mats = meshes[i].materials;
            for (int r = 0; r < mats.Length; r++)
            {
                mats[r].SetFloat("_Team_1", teamFinal);
            }
            meshes[i].materials = mats;
        }

    }

    IEnumerator LoadMap()
    {
        if (!loadingMap)
        {
            //Wait for page to turn
            yield return new WaitForSeconds(0.25f);
            loadingMap = true;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(maps[PlayerPrefs.GetInt("ServerMapName")].sceneName);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }
    }

    [System.Serializable]
    struct MenuPage
    {
        public string leftPageName;
        public string rightPageName;
        public VisualTreeAsset leftAsset;
        public VisualTreeAsset rightAsset;
        public List<MenuButtons> leftInteractables;
        public List<MenuButtons> rightInteractables;
    }

    [System.Serializable]
    struct MenuMaps
    {
        public string mapName;
        public string sceneName;
        public string gameMode;
        public Texture2D image;
    }

    public enum MenuButtonType
    {
        button,
        toggle,
        textField,
        slider,
        dropDown,
    }
    public enum MenuButtonSound
    {
        penFlick,
        pageTurn,
        none,
        typewriter,
    }

    [System.Serializable]
    struct MenuButtons
    {
        public string name;
        public MenuButtonType type;
        public MenuButtonSound sound;
    }
}
