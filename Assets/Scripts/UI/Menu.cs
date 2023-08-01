using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Management;

public class Menu : MonoBehaviour
{
    [SerializeField] MenuPage[] pages;
    [SerializeField] UIDocument leftMenu;
    [SerializeField] UIDocument rightMenu;
    NetworkManager m_NetworkManager;
    [SerializeField] Transform centerRing;
    [SerializeField] RenderTexture menuLeftRT;
    [SerializeField] RenderTexture menuRightRT;
    [SerializeField] RenderTexture fakeMenuLeftRT;
    [SerializeField] RenderTexture fakeMenuRightRT;
    bool flippingPage;

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
        if(flippingPage)
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
                            break;
                        case "Statistics":
                            SwitchPage(2);
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
                        case "Back":
                            SwitchPage(0);
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
