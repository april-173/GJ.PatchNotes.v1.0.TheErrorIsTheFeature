using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Health : MonoBehaviour
{
    public PlayerHealth playerHealth;

    public bool useAnimalVisibility = true;
    [SerializeField] private Tilemap overlayTilemap;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private void Start()
    {
        playerHealth = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerHealth.CurrentHealth != playerHealth.MaxHealth)
            {
                StartCoroutine(AddHealth());
            }
        }
    }

    private IEnumerator AddHealth()
    {
        spriteRenderer.enabled = false;
        playerHealth.Heal(1);

        yield return new WaitForSeconds(1f);

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (useAnimalVisibility)
        {
            Vector3Int c = overlayTilemap.WorldToCell(transform.position);
            if (overlayTilemap.HasTile(c))
                spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0);
            else
                spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1);
        }
        else
        {
            spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1);
        }
    }
}
