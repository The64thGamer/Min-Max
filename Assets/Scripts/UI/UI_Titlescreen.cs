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

        //Functions
        rc_play.clicked += () => SwapColumn(RightColumnSwaps.none,LeftColumnSwaps.PlaySection);
        rc_exit.clicked += () => Application.Quit();
        ps_startlocal.clicked += () => StartLocalOrHost(false);
        ps_startserver.clicked += () => StartLocalOrHost(true);
        //ps_joinserver.clicked += () =>
        ps_back.clicked += () => SwapColumn(RightColumnSwaps.TBAPanel, LeftColumnSwaps.MainButtons);
        ms_startgame.clicked += () => StartCoroutine(LoadMap());
        selectMap.RegisterValueChangedCallback(evt => SwapMap(selectMap));

        //Ect
        selectMap.choices = mapNames;
    }

    IEnumerator LoadMap()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mapNames[currentSceneToLoad]);
        while (!asyncLoad.isDone)
        {
            yield return null;
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

    void StartLocalOrHost(bool host)
    {
        SwapColumn(RightColumnSwaps.MapSelection, LeftColumnSwaps.none);
        if (host)
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

    void SwapMap(DropdownField evt)
    {
        root.Q<VisualElement>("MapIcon").Q<Image>().image = mapIcons[evt.index];
        currentSceneToLoad = evt.index;
    }
}
