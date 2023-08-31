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
    Label ammoText;
    Label clipText;
    VisualElement boxHealth;
    VisualElement boxAmmo;
    VisualElement boxTeam;


    const float dropFontSizeCompensation = 4;

    private void Awake()
    {
        playerUIVTA = transform.Find("PlayerUIDoc").GetComponent<UIDocument>();
        player = this.GetComponent<Player>();
        healthText = playerUIVTA.rootVisualElement.Q<Label>("Health");
        ammoText = playerUIVTA.rootVisualElement.Q<Label>("Ammo");
        clipText = playerUIVTA.rootVisualElement.Q<Label>("Clip");
        boxHealth = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxHealth");
        boxAmmo = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxAmmo");
        boxTeam = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxTeam");

        //Temp
        clipText.text = "∞";
        ammoText.text = "/ ∞";
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
        if(healthLerp <= 0)
        {
            boxColor = new Color(0.3f, 0.3f, 0.35f, 1);
        }
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
        Color boxColor = palette.GetPixel((int)player.GetTeam(), 5);
        Color innerColor = boxColor;
        innerColor.a = 0.5f;

        boxHealth.style.borderBottomColor = boxColor;
        boxHealth.style.borderTopColor = boxColor;
        boxHealth.style.borderLeftColor = boxColor;
        boxHealth.style.borderRightColor = boxColor;
        boxHealth.style.backgroundColor = innerColor;

        boxAmmo.style.borderBottomColor = boxColor;
        boxAmmo.style.borderTopColor = boxColor;
        boxAmmo.style.borderLeftColor = boxColor;
        boxAmmo.style.borderRightColor = boxColor;
        boxAmmo.style.backgroundColor = innerColor;

        boxTeam.style.borderBottomColor = boxColor;
        boxTeam.style.borderTopColor = boxColor;
        boxTeam.style.borderLeftColor = boxColor;
        boxTeam.style.borderRightColor = boxColor;
        boxTeam.style.backgroundColor = boxColor;
    }

}
