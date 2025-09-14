using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// AnimalVisibility
/// - ���ڸ��� GridVision �Ŀɼ��Խ�������� / ��ʾ����
/// - �ҵ�ÿ������ Prefab �ϣ����ӡ�֩��ȣ�
/// - �� Player ÿ���ƶ���ˢ����Ұ�󣺵��� AnimalVisibility.RefreshAll() ��ͬ�����ж���Ŀɼ���
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

