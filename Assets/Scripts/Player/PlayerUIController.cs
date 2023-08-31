using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerUIController : MonoBehaviour
{
    UIDocument playerUIVTA;
    Player player;

    private void Awake()
    {
        playerUIVTA = transform.Find("PlayerUIDoc").GetComponent<UIDocument>();
        player = this.GetComponent<Player>();
    }

    public void UpdateHealthUI()
    {
        playerUIVTA.rootVisualElement.Q<Label>("Health").text = Mathf.Abs(player.GetHealth()).ToString();
    }

    public void UpdateTeamColorUI()
    {

    }
}
