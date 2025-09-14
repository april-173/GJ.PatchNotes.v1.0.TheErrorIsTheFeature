using System.Collections;
using UnityEngine;

public class Plot : MonoBehaviour
{
    public PlayerController playerController;
    public PlayerCombat playerCombat;
    public GridVision gridVision;

    public GameObject[] spiders;

    private void Start()
    {
        foreach(var s in spiders)
        {
            s.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) 
        {
            StartCoroutine(PlayPlot());
        }
    }

    private IEnumerator PlayPlot()
    {
        playerController.ClearKeyStack();
        playerController.enabled = false;
        playerCombat.enabled = false;
        yield return null;

        AudioManager.Instance.PlayAnimalSFX(1, 1.5f, 1f);
        yield return null;

        spiders[0].SetActive(true);
        spiders[1].SetActive(true);
        yield return new WaitForSeconds(1f);

        AudioManager.Instance.PlayAnimalSFX(1, 1.5f, 1f);
        yield return new WaitForSeconds(0.2f);
        AudioManager.Instance.PlayAnimalSFX(1, 1.5f, 1f);
        yield return null;

        foreach (var s in spiders)
        {
            s.SetActive(true);
            AudioManager.Instance.PlaySFX(3, 0.5f, 18f);
            yield return null;
        }
        yield return new WaitForSeconds(1f);

        AudioManager.Instance.PlayAnimalSFX(0, 3f, 1f);
        yield return new WaitWhile(() => AudioManager.Instance.animalSFXAudioSource.isPlaying);
        AudioManager.Instance.PlayAnimalSFX(0, 3f, 1f);
        yield return new WaitForSeconds(1f);

        AudioManager.Instance.PlayAnimalSFX(1, 1.5f, 1f);
        gridVision.useInvertVision = true;
        gridVision.useRevealBlockingObstacles = true;
        yield return new WaitForSeconds(1f);

        AudioManager.Instance.PlayAnimalSFX(0, 3f, 1f);
        foreach (var s in spiders)
        {
            s.SetActive(false);
            AudioManager.Instance.PlaySFX(3, 0.5f, 18f);
            yield return null;
        }
        yield return new WaitForSeconds(1f);

        playerController.enabled = true;
        playerCombat.enabled = true;

        this.gameObject.SetActive(false);
    }
}
