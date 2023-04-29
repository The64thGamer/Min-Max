using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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


        //Functions
        rc_startVR.clicked += () => SwitchMainTab(0);
        rc_play.clicked += () => SwitchMainTab(1);
        rc_exit.clicked += () => Application.Quit();
        ps_startlocal.clicked += () => StartLocalOrHost(0);
        ps_startserver.clicked += () => StartLocalOrHost(1);
        ps_joinserver.clicked += () => StartLocalOrHost(2);
        ps_joinlocal.clicked += () => StartLocalOrHost(3);
        ms_startgame.clicked += () => StartCoroutine(LoadMap());
        selectMap.RegisterValueChangedCallback(evt => SwapMap(selectMap));
        maxPlayers.RegisterValueChangedCallback(evt => SetMaxPlayers(maxPlayers));

        //Ect
        selectMap.choices = mapNames;
        selectMap.index = 0;
        maxPlayers.value = Mathf.Max(PlayerPrefs.GetInt("ServerMaxPlayers"),1);
    }

    IEnumerator LoadMap()
    {
        if (currentSceneToLoad >= 0)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mapNames[currentSceneToLoad]);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }
    }

    void StartLocalOrHost(int loadmapmode)
    {
        PlayerPrefs.SetInt("LoadMapMode", loadmapmode);
        switch (loadmapmode)
        {
            case 0:
                root.Q<Label>("StartSetting").text = "Start Local Game";
                root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.None;
                root.Q<Button>("RequestKey").style.display = DisplayStyle.None;
                root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                break;
            case 1:
                root.Q<Label>("StartSetting").text = "Start Server";
                root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.Flex;
                root.Q<Button>("RequestKey").style.display = DisplayStyle.Flex;
                root.Q<Button>("StartGame").style.display = DisplayStyle.None;
                break;
            case 2:
                root.Q<Label>("StartSetting").text = "Join Server";
                root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.None;
                root.Q<Button>("RequestKey").style.display = DisplayStyle.None;
                root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                break;
            case 3:
                root.Q<Label>("StartSetting").text = "Join Local Game";
                root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.None;
                root.Q<Button>("RequestKey").style.display = DisplayStyle.None;
                root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
                break;
            default:
                break;
        }
    }

    void SwitchMainTab(int tab)
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
                break;
            case 1:
                root.Q<VisualElement>("PlayMenu").style.display = DisplayStyle.Flex;
                SetVEBorderColor(root.Q<VisualElement>("RCPlay"), borderButtonSelected);
                break;
            default:
                break;
        }
    }

    void SetVEBorderColor(VisualElement v, Color c)
    {
        v.style.borderTopColor = new StyleColor(c);
        v.style.borderRightColor = new StyleColor(c);
        v.style.borderLeftColor = new StyleColor(c);
        v.style.borderBottomColor = new StyleColor(c);
    }

    void SetMaxPlayers(SliderInt playerCount)
    {
        PlayerPrefs.SetInt("ServerMaxPlayers",(int)playerCount.value);
    }


    void SwapMap(DropdownField evt)
    {
        root.Q<VisualElement>("MapIcon").style.backgroundImage = new StyleBackground(mapIcons[evt.index]);
        currentSceneToLoad = evt.index;
    }
}
