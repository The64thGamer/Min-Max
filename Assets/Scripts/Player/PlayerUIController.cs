using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerUIController : MonoBehaviour
{
    [SerializeField] Texture2D palette;
    UIDocument playerUIVTA;
    Player player;

    private void Awake()
    {
        playerUIVTA = transform.Find("PlayerUIDoc").GetComponent<UIDocument>();
        player = this.GetComponent<Player>();
    }

    public void UpdateHealthUI()
    {
        float healthLerp = player.GetHealth() / (float)player.GetClassStats().baseHealth;

        Label healthText = playerUIVTA.rootVisualElement.Q<Label>("Health");
        healthText.text = Mathf.Max(0,player.GetHealth()).ToString();
        healthText.style.fontSize = Mathf.Lerp(60, 120, healthLerp);

        VisualElement boxHealth = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxHealth");
        Color boxColor = Color.Lerp(Color.red, palette.GetPixel((int)player.GetTeam(), 5), healthLerp);
        boxHealth.style.backgroundColor = boxColor;
        boxHealth.style.borderBottomColor = boxColor;
        boxHealth.style.borderTopColor = boxColor;
        boxHealth.style.borderLeftColor = boxColor;
        boxHealth.style.borderRightColor = boxColor;
    }

    public void UpdateTeamColorUI()
    {

    }
}
