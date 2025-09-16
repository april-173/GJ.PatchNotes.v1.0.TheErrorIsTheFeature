using System.Collections;
using UnityEngine;

public class End : MonoBehaviour
{
    public PlayerController playerController;
    public PlayerCombat playerCombat;
    public GridVision gridVision;

    public GameObject EndPanel;

    public GameObject R;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            AudioManager.Instance.PlayAnimalSFX(2, 1f, 1f);

            gridVision.useInvertVision = false;
            gridVision.useRevealBlockingObstacles = true;

            playerController.ClearKeyStack();

            playerController.enabled = false;
            playerCombat.enabled = false;
            EndPanel.SetActive(true);
        }
    }

    public void EndButton()
    {
        playerController.enabled = true;
        playerCombat.enabled = true;

        R.SetActive(true);
        EndPanel.SetActive(false);
        this.gameObject.SetActive(false);
    }
}
