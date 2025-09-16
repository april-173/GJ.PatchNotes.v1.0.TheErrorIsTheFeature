using UnityEngine;
using UnityEngine.SceneManagement;

public class Die : MonoBehaviour
{
    public PlayerController playerController;
    public PlayerCombat playerCombat;

    public GameObject DiePanel;

    public void PlayerDie()
    {
        playerController.ClearKeyStack();

        playerController.enabled = false;
        playerCombat.enabled = false;
        DiePanel.SetActive(true);
    }

    public void DieButton()
    {

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
