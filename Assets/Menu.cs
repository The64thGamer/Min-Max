using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class Menu : MonoBehaviour
{
    [SerializeField] MenuPage[] pages;
    [SerializeField] UIDocument leftMenu;
    [SerializeField] UIDocument rightMenu;
    NetworkManager m_NetworkManager;

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

    void SwitchPage(int index)
    {
        Debug.Log(index);
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
    }

    void ButtonPressed(string page, string button)
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

                        break;
                    case "Customization":
                        SwitchPage(1);
                        break;
                    case "Statistics":

                        break;
                    case "Settings":

                        break;
                    case "StartVR":

                        break;
                    case "Exit":

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
            default:
                break;
        }
    }

    private void OnClientDisconnectCallback(ulong obj)
    {

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
