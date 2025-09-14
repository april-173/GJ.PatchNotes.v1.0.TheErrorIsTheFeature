using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// AnimalVisibility
/// - 用于根据 GridVision 的可见性结果来隐藏 / 显示动物
/// - 挂到每个动物 Prefab 上（兔子、蜘蛛等）
/// - 在 Player 每次移动并刷新视野后：调用 AnimalVisibility.RefreshAll() 来同步所有动物的可见性
/// </summary>
public class AnimalVisibility : MonoBehaviour
{
    public bool useAnimalVisibility = true;
    [SerializeField] private Tilemap overlayTilemap;
    [SerializeField] private SpriteRenderer spriteRenderer;

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

