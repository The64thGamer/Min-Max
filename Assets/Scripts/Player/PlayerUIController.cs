using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerUIController : MonoBehaviour
{
    [SerializeField] Texture2D palette;
    [SerializeField] Texture2D[] playerIcons;
    [SerializeField] VisualTreeAsset teammateVTA;
    [SerializeField] VisualTreeAsset enemyVTA;

    UIDocument playerUIVTA;
    Player player;
    GlobalManager gm;


    [Range(-0.9f, 0.9f)]
    float healthDrop;
    List<TeamList> teams;
    Label healthText;
    Label ammoText;
    Label clipText;
    Label timerText;
    Label hpLabelText;
    VisualElement boxHealth;
    VisualElement boxAmmo;
    VisualElement boxTeam;
    VisualElement holderEnemies;

    const float dropFontSizeCompensation = 4;

    private void Awake()
    {
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        playerUIVTA = transform.Find("PlayerUIDoc").GetComponent<UIDocument>();
        player = this.GetComponent<Player>();
        healthText = playerUIVTA.rootVisualElement.Q<Label>("Health");
        ammoText = playerUIVTA.rootVisualElement.Q<Label>("Ammo");
        clipText = playerUIVTA.rootVisualElement.Q<Label>("Clip");
        boxHealth = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxHealth");
        boxAmmo = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxAmmo");
        boxTeam = playerUIVTA.rootVisualElement.Q<VisualElement>("BoxTeam");
        holderEnemies = playerUIVTA.rootVisualElement.Q<VisualElement>("HolderEnemies");
        timerText = playerUIVTA.rootVisualElement.Q<Label>("Timer");
        hpLabelText = playerUIVTA.rootVisualElement.Q<Label>("HPLabel");
    }

    private void Update()
    {
        healthDrop = Mathf.Lerp(healthDrop, healthDrop * Mathf.Abs(healthDrop), Time.deltaTime * 10);
        healthText.style.translate = new Translate(0, healthDrop * -40, 0);
        int seconds = gm.GetCurrentRoundTime();
        if(seconds < 0)
        {
            timerText.text = "∞";
        }
        else
        {
            timerText.text = $"{seconds / 60}:{seconds % 60:D2}";
        }
        if (player.GetHealth() <= 0)
        {
            healthText.text = ((int)player.GetTimeTillRespawn()).ToString();
        }
    }

    public void UpdateGunUI()
    {
        Gun currentGun = player.GetCurrentGun();
        clipText.text = currentGun.FindStat(ChangableWeaponStats.currentClip).ToString();
        ammoText.text = "/ " + currentGun.FindStat(ChangableWeaponStats.currentAmmo).ToString();
    }

    public void UpdateHealthUI(float oldHealth)
    {
        float baseHealth = (float)player.GetClassStats().baseHealth;
        float healthLerp = Mathf.Max(0,player.GetHealth()) / baseHealth;
        healthLerp *= healthLerp;

        healthText.text = Mathf.Max(0, player.GetHealth()).ToString();
        healthText.style.fontSize = Mathf.Lerp(80, 120, healthLerp);

        Color[] colors = new Color[] { Color.red, Color.gray, palette.GetPixel((int)player.GetTeam(), 5) };
        float scaledTime = healthLerp * (colors.Length - 1);
        Color oldColor = colors[(int)scaledTime];
        Color newColor = colors[Mathf.Min((int)scaledTime + 1,colors.Length-1)];
        float newT = scaledTime - Mathf.Floor(scaledTime);
        Color boxColor = Color.Lerp(oldColor, newColor, newT);

        if (player.GetHealth() <= 0)
        {
            boxColor = new Color(0.3f, 0.3f, 0.35f, 1);
            hpLabelText.text = "Sec";
        }
        else
        {
            hpLabelText.text = "HP";
        }

        boxHealth.style.borderBottomColor = boxColor;
        boxHealth.style.borderTopColor = boxColor;
        boxHealth.style.borderLeftColor = boxColor;
        boxHealth.style.borderRightColor = boxColor;

        boxColor.a = 0.5f;
        boxHealth.style.backgroundColor = boxColor;

        //Health is dropped based on how much health is lost, and compensation
        //is given due to the font shrinking with health loss.
        healthDrop = Mathf.Clamp(((player.GetHealth() - oldHealth) / baseHealth) * (dropFontSizeCompensation - ((dropFontSizeCompensation - 1) * (player.GetHealth() / baseHealth))) * 0.9f, -0.9f, 0.9f);
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

        UpdateClientsConnected();
    }

    public void UpdateClientsConnected()
    {

        //Enemy Holder
        List<VisualElement> children = new List<VisualElement>();
        foreach (var child in holderEnemies.Children())
        {
            children.Add(child);
        }
        for (int i = 0; i < children.Count; i++)
        {
            holderEnemies.Remove(children[i]);
        }

        teams = gm.GetTeamColors(true);
        for (int i = 0; i < teams.Count; i++)
        {
            if (teams[i] != player.GetTeam())
            {
                TemplateContainer myUI = enemyVTA.Instantiate();
                holderEnemies.Add(myUI);
                Color boxColor = palette.GetPixel((int)teams[i], 5);
                VisualElement box = myUI.Q<VisualElement>("BoxEnemyTeam");
                box.style.borderBottomColor = boxColor;
                box.style.borderTopColor = boxColor;
                box.style.borderLeftColor = boxColor;
                box.style.borderRightColor = boxColor;
                box.style.backgroundColor = boxColor;
            }
        }

        //Teammate Holder
        children = new List<VisualElement>();
        foreach (var child in boxTeam.Children())
        {
            children.Add(child);
        }
        for (int i = 0; i < children.Count; i++)
        {
            boxTeam.Remove(children[i]);
        }

        List<Player> players = gm.GetClients();
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].GetTeam() == player.GetTeam())
            {
                TemplateContainer myUI = teammateVTA.Instantiate();
                myUI.Q<VisualElement>("Icon").style.backgroundImage = playerIcons[(int)players[i].GetCurrentClass()];
                boxTeam.Add(myUI);
            }
        }
    }

    public void SetVisibility(bool visible)
    {
        if(visible)
        {
            playerUIVTA.rootVisualElement.style.display = DisplayStyle.Flex;
        }
        else
        {
            playerUIVTA.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

}
