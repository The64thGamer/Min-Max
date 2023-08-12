using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UIElements;
using UnityEngine.XR.Management;

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
    [SerializeField] string[] cosmeticTypes;

    [Header("Objects")]
    [SerializeField] GameObject customizeMenuCamera;
    [SerializeField] Cosmetics cosmetics;
    [SerializeField] VisualTreeAsset cosmeticIconVTA;
    [SerializeField] Texture2D noneTexture;
    List<GameObject> currentCharMeshes = new List<GameObject>();

    int[] cosmeticInts;
    NetworkManager m_NetworkManager;
    bool flippingPage;

    //Labels
    int currentCustClass;
    int currentCustTeam;
    int currentCustPage;
    int currentCustCosmType;
    int currentCustLoadout;

    private void OnEnable()
    {
        //Pinging
        m_NetworkManager = GameObject.Find("Transport").GetComponent<NetworkManager>();
        if (m_NetworkManager != null)
        {
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
            m_NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes("P");
        }

        SwitchPage(0);
    }

    private void Update()
    {
        if (flippingPage)
        {
            centerRing.eulerAngles = new Vector3(0, centerRing.eulerAngles.y + (Time.deltaTime * 750), 0);
            if (centerRing.eulerAngles.y > 359 || centerRing.eulerAngles.y < 180)
            {
                centerRing.eulerAngles = new Vector3(0, 0, 0);
                flippingPage = false;
            }
        }
    }

    void SwitchPage(int index)
    {
        if (!flippingPage)
        {
            leftMenu.visualTreeAsset = pages[index].leftAsset;
            VisualElement root = leftMenu.rootVisualElement;
            for (int i = 0; i < pages[index].leftButtons.Count; i++)
            {
                int indexCtx = index;
                int iCtx = i;
                root.Q<Button>(pages[indexCtx].leftButtons[iCtx]).clicked += () => ButtonPressed(pages[indexCtx].leftPageName, pages[indexCtx].leftButtons[iCtx]);
            }

            rightMenu.visualTreeAsset = pages[index].rightAsset;
            root = rightMenu.rootVisualElement;
            for (int i = 0; i < pages[index].rightButtons.Count; i++)
            {
                int indexCtx = index;
                int iCtx = i;
                root.Q<Button>(pages[indexCtx].rightButtons[iCtx]).clicked += () => ButtonPressed(pages[indexCtx].rightPageName, pages[indexCtx].rightButtons[iCtx]);
            }

            flippingPage = true;
            centerRing.eulerAngles = new Vector3(0, 180, 0);
            Graphics.CopyTexture(menuLeftRT, fakeMenuLeftRT);
            Graphics.CopyTexture(menuRightRT, fakeMenuRightRT);
        }
    }

    void ButtonPressed(string page, string button)
    {
        if (!flippingPage)
        {
            Debug.Log("Button " + button + " of " + page + " pressed");
            switch (page)
            {
                case "Title Left":
                    switch (button)
                    {
                        case "JoinGame":

                            break;
                        case "CreateGame":
                            SwitchPage(4);
                            break;
                        case "Customization":
                            SwitchPage(1);
                            currentCustClass = 3;
                            currentCustTeam = 0;
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
                            break;
                        case "Statistics":
                            SwitchPage(2);
                            SetLabel("Statistics",
                                "Total Damage: " + PlayerPrefs.GetFloat("Achievement: Total Damage") +
                                "\nTotal Kills: " + PlayerPrefs.GetFloat("Achievement: Total Kills") +
                                "\nTotal Distance Walked: " + (int)PlayerPrefs.GetFloat("Achievement: Total Walking Distance") + " m" +
                                "\nTotal Air Travel: " + (int)PlayerPrefs.GetFloat("Achievement: Total Air Travel") + " m" +
                                "\nTotal Air-Time: " + (int)PlayerPrefs.GetFloat("Achievement: Total Air-Time") + " sec" +
                                "\nTotal Laborers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Laborers Killed") +
                                "\nTotal Wood Workers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Wood Workers Killed") +
                                "\nTotal Developers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Developers Killed") +
                                "\nTotal Programmers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Programmers Killed") +
                                "\nTotal Computers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Computers Killed") +
                                "\nTotal Fabricators Killed: " + PlayerPrefs.GetFloat("Achievement: Total Fabricators Killed") +
                                "\nTotal Artists Killed: " + PlayerPrefs.GetFloat("Achievement: Total Artists Killed") +
                                "\nTotal Freelancers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Freelancers Killed") +
                                "\nTotal Craftsmen Killed: " + PlayerPrefs.GetFloat("Achievement: Total Craftsmen Killed") +
                                "\nTotal Managers Killed: " + PlayerPrefs.GetFloat("Achievement: Total Managers Killed")
                                ,false);
                            break;
                        case "Settings":
                            SwitchPage(3);
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
                        case "Back":
                            SwitchPage(0);
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

    void InsertCosmetic(int cosmeticValue)
    {
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
        if(noneIcon)
        {
            TemplateContainer myUI = cosmeticIconVTA.Instantiate();
            myUI.Q<VisualElement>("Icon").style.backgroundImage = new StyleBackground(noneTexture);

            myUI.Q<Button>("Button").clicked += () => InsertCosmetic(-1);
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
                myUI.Q<Button>("Button").clicked += () => InsertCosmetic(finalVal);
                visList.Add(myUI);
            }
        }
        SetLabel("CosmeticTypeLabel", cosmeticTypes[currentCustCosmType].PadRight(10), false);
    }

    void SetLabel(string element, string text, bool isRightPage)
    {
        if (isRightPage)
        {
            rightMenu.rootVisualElement.Q<Label>(element).text = text;
        }
        else
        {
            leftMenu.rootVisualElement.Q<Label>(element).text = text;
        }
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

    private void OnClientDisconnectCallback(ulong obj)
    {

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
            if(check >= 0)
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

    [System.Serializable]
    struct MenuPage
    {
        public string leftPageName;
        public string rightPageName;
        public VisualTreeAsset leftAsset;
        public VisualTreeAsset rightAsset;
        public List<string> leftButtons;
        public List<string> rightButtons;
    }
}
