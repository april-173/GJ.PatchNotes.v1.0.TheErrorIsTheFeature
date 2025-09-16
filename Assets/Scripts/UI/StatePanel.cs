using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatePanel : MonoBehaviour
{
    public PlayerCombat playerCombat;
    public PlayerHealth playerHealth;
    public PlayerRabbitPickup playerRabbitPickup;

    [Header("UIœ‘ æ")]
    public GameObject bulletPanel;
    public GameObject[] bullerPanelChildren;
    public Sprite bulletSprite;
    public Sprite noBulletSprite;
    [Space]
    public GameObject KnifePanel;
    public Sprite[] KnifeSprite;
    [Space]
    public GameObject healthPanel;
    public GameObject[] healthPanelChildren;
    public Sprite healthSprite;
    public Sprite noHealthSprite;
    [Space]
    public TextMeshProUGUI rabbitCount;

    private void Update()
    {
        UpdateBullet();
        UpdateKnife();
        UpdateHealth();
        UpdateRabbitCount();
    }

    private void UpdateBullet()
    {
        for (int i = 0; i < bullerPanelChildren.Length; i++)
        {
            if (i < playerCombat.CurrentShotgunBulletsCount)
            {
                bullerPanelChildren[i].GetComponent<Image>().sprite = bulletSprite;
            }
            else
            {
                bullerPanelChildren[i].GetComponent<Image>().sprite = noBulletSprite;
            }
        }
    }

    private void UpdateKnife()
    {
        int index = playerCombat.CurrentKnifeAttackNumber;

        KnifePanel.GetComponent<Image>().sprite = KnifeSprite[index];
    }

    private void UpdateHealth()
    {
        for (int i = 0; i < healthPanelChildren.Length; i++)
        {
            if (i < playerHealth.CurrentHealth)
            {
                healthPanelChildren[i].GetComponent<Image>().sprite = healthSprite;
            }
            else
            {
                healthPanelChildren[i].GetComponent<Image>().sprite = noHealthSprite;
            }    
        }
    }

    private void UpdateRabbitCount()
    {
        rabbitCount.text = "X " + string.Format("{0:D2}", playerRabbitPickup.RabbitCount);
    }
}
