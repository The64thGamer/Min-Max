using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerUIController : MonoBehaviour
{
    [SerializeField] Texture2D palette;
    UIDocument playerUIVTA;
    Player player;

    [Range(-0.9f, 0.9f)]
    float healthDrop;

    Label healthText;
    VisualElement boxHealth;

    const float dropFontSizeCompensation = 4;

    private void Awake()
    {
        playerUIVTA = transform.Find("PlayerUIDoc").GetComponent<UIDocument>();
        player = this.GetComponent<Player>();
        healthText = playerUIVTA.rootVisualElement.Q<Label>("Health");
        boxHealth = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxHealth");
    }

    private void Update()
    {
        healthDrop = Mathf.Lerp(healthDrop,healthDrop * Mathf.Abs(healthDrop),Time.deltaTime * 10);
        healthText.style.translate = new Translate(0,healthDrop * -40, 0);
    }

    public void UpdateHealthUI(float oldHealth)
    {
        float baseHealth = (float)player.GetClassStats().baseHealth;
        float healthLerp = player.GetHealth() / baseHealth;
        healthLerp *= healthLerp;

        healthText.text = Mathf.Max(0,player.GetHealth()).ToString();
        healthText.style.fontSize = Mathf.Lerp(80, 120, healthLerp);

        Color boxColor = Color.Lerp(Color.red, palette.GetPixel((int)player.GetTeam(), 5), healthLerp);
        boxHealth.style.borderBottomColor = boxColor;
        boxHealth.style.borderTopColor = boxColor;
        boxHealth.style.borderLeftColor = boxColor;
        boxHealth.style.borderRightColor = boxColor;

        boxColor.a = 0.5f;
        boxHealth.style.backgroundColor = boxColor;

        //Health is dropped based on how much health is lost, and compensation
        //is given due to the font shrinking with health loss.
        healthDrop = Mathf.Clamp(((player.GetHealth() - oldHealth) / baseHealth) * (dropFontSizeCompensation - ((dropFontSizeCompensation - 1) * (player.GetHealth() / baseHealth))) * 0.9f,-0.9f,0.9f);
    }

    public void UpdateTeamColorUI()
    {

    }
}
