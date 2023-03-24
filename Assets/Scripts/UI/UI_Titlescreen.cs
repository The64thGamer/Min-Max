using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class UI_Titlescreen : MonoBehaviour
{
    VisualElement root;

    [SerializeField] List<Texture2D> mapIcons;
    [SerializeField] List<string> mapNames;
    int currentSceneToLoad;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        //Grab
        Button rc_play = root.Q<Button>("RCPlay");
        Button rc_exit = root.Q<Button>("RCExit");
        Button ps_startlocal = root.Q<Button>("PSStartLocal");
        Button ps_startserver = root.Q<Button>("PSStartServer");
        Button ps_joinserver = root.Q<Button>("PSJoinServer");
        Button ps_back = root.Q<Button>("PSBack");
        Button ms_startgame = root.Q<Button>("StartGame");
        DropdownField selectMap = root.Q<DropdownField>("SelectMap");
        SliderInt maxPlayers = root.Q<SliderInt>("MaxPlayers");


        //Functions
        rc_play.clicked += () => SwapColumn(RightColumnSwaps.none,LeftColumnSwaps.PlaySection);
        rc_exit.clicked += () => Application.Quit();
        ps_startlocal.clicked += () => StartLocalOrHost(2);
        ps_startserver.clicked += () => StartLocalOrHost(1);
        //ps_joinserver.clicked += () =>
        ps_back.clicked += () => SwapColumn(RightColumnSwaps.TBAPanel, LeftColumnSwaps.MainButtons);
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

    enum LeftColumnSwaps
    {
        none,
        MainButtons,
        PlaySection
    }
    enum RightColumnSwaps
    {
        none,
        TBAPanel,
        MapSelection
    }

    //"LoadMapMode" 0 == Local, 1 == Host, 2 == Client
    void StartLocalOrHost(int loadmapmode)
    {
        PlayerPrefs.SetInt("LoadMapMode", loadmapmode);
        SwapColumn(RightColumnSwaps.MapSelection, LeftColumnSwaps.none);
        if (loadmapmode == 1)
        {
            root.Q<Label>("StartLocalGame").style.display = DisplayStyle.None;
            root.Q<Label>("StartServerGame").style.display = DisplayStyle.Flex;
            root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.Flex;
            root.Q<Button>("RequestKey").style.display = DisplayStyle.Flex;
            root.Q<Button>("StartGame").style.display = DisplayStyle.None;
        }
        else
        {

            root.Q<Label>("StartLocalGame").style.display = DisplayStyle.Flex;
            root.Q<Label>("StartServerGame").style.display = DisplayStyle.None;
            root.Q<Label>("SendPlayerKey").style.display = DisplayStyle.None;
            root.Q<Button>("RequestKey").style.display = DisplayStyle.None;
            root.Q<Button>("StartGame").style.display = DisplayStyle.Flex;
        }
    }

    void SwapColumn(RightColumnSwaps r, LeftColumnSwaps l)
    {
        switch (l)
        {
            case LeftColumnSwaps.MainButtons:
                root.Q<VisualElement>("MainButtons").style.display = DisplayStyle.Flex;
                root.Q<VisualElement>("PlaySection").style.display = DisplayStyle.None;
                break;
            case LeftColumnSwaps.PlaySection:
                root.Q<VisualElement>("MainButtons").style.display = DisplayStyle.None;
                root.Q<VisualElement>("PlaySection").style.display = DisplayStyle.Flex;
                break;
            default:
                break;
        }
        switch (r)
        {
            case RightColumnSwaps.TBAPanel:
                root.Q<VisualElement>("MapSelection").style.display = DisplayStyle.None;
                root.Q<VisualElement>("TBAPanel").style.display = DisplayStyle.Flex;
                break;
            case RightColumnSwaps.MapSelection:
                root.Q<VisualElement>("MapSelection").style.display = DisplayStyle.Flex;
                root.Q<VisualElement>("TBAPanel").style.display = DisplayStyle.None;
                break;
            default:
                break;
        }
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
