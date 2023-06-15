using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public class UI_Titlescreen : MonoBehaviour
{
    [SerializeField] UIDocument doc;
    [SerializeField] List<Texture2D> mapIcons;
    [SerializeField] List<string> mapNames;


    //Colors
    [SerializeField] Color borderButtonSelected;

    //Ect
    VisualElement root;
    int currentSceneToLoad;

    bool alreadyLoading;

    private void OnEnable()
    {
        root = doc.rootVisualElement;

        //Bottom Bar
        Button rc_startVR = root.Q<Button>("RCStartVR");
        Button rc_play = root.Q<Button>("RCPlay");
        Button rc_exit = root.Q<Button>("RCExit");

        //Play Menu
        Button ps_joinserver = root.Q<Button>("PSJoinServer");
        Button ps_startserver = root.Q<Button>("PSStartServer");
        Button ps_startlocal = root.Q<Button>("PSStartLocal");
        Button ps_joinlocal = root.Q<Button>("PSJoinLocal");

        //Server Settings
        Button ms_startgame = root.Q<Button>("StartGame");
        DropdownField selectMap = root.Q<DropdownField>("SelectMap");
        SliderInt maxPlayers = root.Q<SliderInt>("MaxPlayers");
        Toggle spectatorAsPlayer = root.Q<Toggle>("SpectatorAsPlayer");
        Toggle isHostSpectator = root.Q<Toggle>("HostisSpectator");
        TextField selectPort = root.Q<TextField>("SelectPort");
        DropdownField team1 = root.Q<DropdownField>("SelectTeam1");
        DropdownField team2 = root.Q<DropdownField>("SelectTeam2");
        Toggle spawnBots = root.Q<Toggle>("SpawnBots");


        //Functions When Button is Clicked
        rc_startVR.clicked += () => SwitchMainTab(0);
        rc_play.clicked += () => SwitchMainTab(1);
        rc_exit.clicked += () => Application.Quit();
        ps_startlocal.clicked += () => StartLocalOrHost(0);
        ps_startserver.clicked += () => StartLocalOrHost(1);
        ps_joinserver.clicked += () => StartLocalOrHost(2);
        ps_joinlocal.clicked += () => StartLocalOrHost(3);
        ms_startgame.clicked += () => StartCoroutine(LoadMap());
        selectMap.RegisterValueChangedCallback(evt => SwapMap(selectMap));
        maxPlayers.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("ServerMaxPlayers", (int)maxPlayers.value));
        spectatorAsPlayer.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("ServerSpectatorAsPlayer", spectatorAsPlayer.value ? 1 : 0));
        selectPort.RegisterValueChangedCallback(evt => TryPortChange(selectPort));
        team1.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("Team1Setting", team1.index));
        team2.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("Team2Setting", team2.index));
        spawnBots.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt("SpawnBotsInEmpty", spawnBots.value ? 1 : 0));

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
        selectMap.index = 0;
        maxPlayers.value = Mathf.Max(PlayerPrefs.GetInt("ServerMaxPlayers"),1);
        spawnBots.value = Convert.ToBoolean(PlayerPrefs.GetInt("SpawnBotsInEmpty"));
        spectatorAsPlayer.value = Convert.ToBoolean(PlayerPrefs.GetInt("ServerSpectatorAsPlayer"));
        team1.index = PlayerPrefs.GetInt("Team1Setting");
        team2.index = PlayerPrefs.GetInt("Team2Setting");
        selectPort.value = PlayerPrefs.GetInt("ServerPort").ToString();



    }

    IEnumerator LoadMap()
    {
        if (!alreadyLoading)
        {
            alreadyLoading = true;
            if (currentSceneToLoad >= 0)
            {
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

    void StartLocalOrHost(int loadmapmode)
    {
        if (!alreadyLoading)
        {
            //Default
            root.Q<VisualElement>("ServerSettings").style.display = DisplayStyle.None;
            root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.None;
            root.Q<VisualElement>("JoinLocalCol").style.display = DisplayStyle.None;
            root.Q<VisualElement>("JoinServerCol").style.display = DisplayStyle.None;

            root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.None;
            root.Q<Button>("RequestKey").style.display = DisplayStyle.None;
            root.Q<Button>("StartGame").style.display = DisplayStyle.None;

            VisualElement col1 = root.Q<VisualElement>("PlayMenuCol1");

            Color noBorder = borderButtonSelected;
            noBorder.a = 0;

            for (int i = 0; i < col1.childCount; i++)
            {
                SetVEBorderColor(col1.ElementAt(i), noBorder);
            }

            PlayerPrefs.SetInt("LoadMapMode", loadmapmode);

            switch (loadmapmode)
            {
                case 0:
                    root.Q<Label>("StartSetting").text = "Start Local Game";
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("ServerSettings").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("PSStartLocal"), borderButtonSelected);
                    break;
                case 1:
                    root.Q<Label>("StartSetting").text = "Start Server";
                    root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.Flex;
                    root.Q<Button>("RequestKey").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("ServerSettings").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("PSStartServer"), borderButtonSelected);
                    break;
                case 2:
                    root.Q<Label>("StartSetting").text = "Join Server";
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("JoinServerCol").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("PSJoinServer"), borderButtonSelected);
                    break;
                case 3:
                    root.Q<Label>("StartSetting").text = "Join Local Game";
                    root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                    root.Q<VisualElement>("StartMapCol").style.display = DisplayStyle.Flex;
                    SetVEBorderColor(root.Q<VisualElement>("PSJoinLocal"), borderButtonSelected);
                    break;
                default:
                    break;
            }
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
}
