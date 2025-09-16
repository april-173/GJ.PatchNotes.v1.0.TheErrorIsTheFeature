using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SpiderAI : MonoBehaviour, ITurnActor
{
    #region < �ֶ� >
    [Header("Ȩ��")]
    [Tooltip("�ƶ�Ȩ��")]
    [SerializeField] private int moveWeight = 60;
    [Tooltip("����Ȩ��")]
    [SerializeField] private int idleWeight = 30;
    [Tooltip("����Ȩ��")]
    [SerializeField] private int webWeight = 10;

    [Header("Tilemap ֧��")]
    [Tooltip("�Ƿ�ʹ�� Tilemap ��������")]
    [SerializeField] private bool useTilemapCoords = true;
    [Tooltip("���� Tilemap")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("�ϰ� Tilemap")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("���/������")]
    [Tooltip("�ϰ� LayerMask")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("�ɴݻ����� LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("���� LayerMask")]
    [SerializeField] private LayerMask animalLayer;
    [Tooltip("��� LayerMask")]
    [SerializeField] private LayerMask playerLayer;

    [Header("���ɶ���")]
    [Tooltip("֩������Ԥ����")]
    [SerializeField] private GameObject webPrefab;
    [Tooltip("֩�����ĸ�����")]
    [SerializeField] private Transform webParent;

    [Header("ʬ����ʾ")]
    [Tooltip("����״̬�� Sprite")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("������ʹ�õ�ʬ�� Sprite")]
    [SerializeField] private Sprite corpseSprite;

    [Header("����")]
    [Tooltip("������ʱ�� Overlap �뾶")]
    [SerializeField] private float collisionCheckRadius = 0.15f;

    [Header("�������")]
    [Tooltip("��� Transform")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("���� SpriteRenderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("���� AnimalVisibility")]
    [SerializeField] private AnimalVisibility animalVisibility;

    // �ڲ�״̬
    private bool isDead = false;

    private static readonly Vector2Int[] DIR_8 = new Vector2Int[]
{
        new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1),
        new Vector2Int(-1,0), new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1,-1)
};

    private static readonly Vector2Int[] DIR_4 = new Vector2Int[] {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };
    #endregion

    private void Start()
    {
        if(spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        animalVisibility = GetComponent<AnimalVisibility>();

        if (playerTransform == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null) playerTransform = pgo.transform;
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.Register(this);
        else
            Debug.LogWarning("[SpiderAI] δ�ҵ� TurnManager����ȷ���������� TurnManager��");
    }

    private void OnDestroy()
    {
        // ע����ֹ����
        if (TurnManager.Instance != null)
            TurnManager.Instance.Unregister(this);
    }

    //private bool b = false;

    //private void Update()
    //{
    //    if (Vector2.Distance(transform.position, playerTransform.position) > 30 && b)
    //    {
    //        TurnManager.Instance.Unregister(this);
    //    }
    //    else if (Vector2.Distance(transform.position, playerTransform.position) <= 30 && !b) 
    //    {
    //        TurnManager.Instance.Register(this);
    //    }
    //}

    #region < �߼����� >
    /// <summary>
    /// TurnManager ���ã�ִ�б��غ��߼�
    /// </summary>
    public IEnumerator TakeTurn()
    {
        if (isDead) yield break;

        // ����������ڰ˸� -> ����
        Collider2D playerCollider;
        if(IsPlayerAdjacent(out playerCollider))
        {
            AttackPlayer(playerCollider);

            AudioManager.Instance.PlayAnimalSFX(0, 0.5f, 3f);
            yield break;
        }

        // ������Ұ�����ϰ�ֱ����ң������Գ���ҷ����ƶ�
        if (playerTransform != null && HasLineOfSightToPlayer())
        {
            bool moveToward = TryMoveTowardsPlayer();
            if (moveToward)
                yield break;
        }

        // ������ߣ�����Ȩ�أ�
        int total = Mathf.Max(1, moveWeight + idleWeight + webWeight);
        int roll = Random.Range(0, total);

        if (roll < moveWeight)
        {
            TryRandomMove();
            yield break;
        }
        else if(roll < moveWeight + idleWeight)
        {
            yield break;
        }
        else
        {
            CreateWebAtCurrentCell();
            yield break;
        }
    }
    #endregion

    #region < AI�߼� >
    /// <summary>
    /// �������Ƿ��� 8 �����ڣ�����Ƿ��ظ���ҵ� collider
    /// </summary>
    /// <param name="playerCollider"></param>
    /// <returns></returns>
    private bool IsPlayerAdjacent(out Collider2D playerCollider)
    {
        playerCollider = null;
        Vector3Int baseCell = GetBaseCell();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector3Int c = baseCell + new Vector3Int(dx, dy, 0);
                Vector3 center = GetCellCenterWorld(c);
                var hits = Physics2D.OverlapCircleAll(center, collisionCheckRadius, playerLayer);
                if(hits !=null && hits.Length >0)
                {
                    playerCollider = hits[0];
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// �������
    /// </summary>
    /// <param name="playerCollider"></param>
    private void AttackPlayer(Collider2D playerCollider)
    {
        if(playerCollider == null) return;
        playerCollider.gameObject.GetComponent<PlayerHealth>().TakeDamage(1);
    }

    private bool HasLineOfSightToPlayer()
    {
        if (playerTransform == null) return false;

        if(useTilemapCoords && groundTilemap != null && obstacleTilemap != null)
        {
            Vector3Int from = GetBaseCell();
            Vector3Int to = groundTilemap.WorldToCell(playerTransform.position);

            foreach(var c in BresenhamLine(from,to))
            {
                if (c == from) continue;
                if (c == to) break;
                if (obstacleTilemap.HasTile(c)) return false;
            }
            return true;
        }
        else
        {
            // Raycast ��� obstacleLayer �Ƿ������߼��赲
            Vector3 origin = transform.position;
            Vector3 dir = (playerTransform.position - origin);
            float dist = dir.magnitude;
            if (dist <= 0.001f) return true;
            dir.Normalize();
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, obstacleLayer);
            return hit.collider == null;
        }
    }

    /// <summary>
    /// ���Գ���ҷ����ƶ�
    /// </summary>
    /// <returns></returns>
    private bool TryMoveTowardsPlayer()
    {
        if(playerTransform == null) return false;

        Vector3 origin = transform.position;

        Vector3Int baseCell = GetBaseCell();
        Vector3Int playerCell = useTilemapCoords && groundTilemap != null?
            groundTilemap.WorldToCell(playerTransform.position):
            new Vector3Int(Mathf.RoundToInt(playerTransform.position.x), Mathf.RoundToInt(playerTransform.position.y), 0);
        int dx = Mathf.Clamp(playerCell.x - baseCell.x, -1, 1);
        int dy = Mathf.Clamp(playerCell.y - baseCell.y, -1, 1);

        if (dx == 0 && dy == 0) return false;

        List<Vector2Int> candidates = new List<Vector2Int>();
        if (dx != 0 && dy != 0) 
        {
            // ������ x ���� y ƫ��ʱ�����ȰѶԽ�/ֱ��/���򶼷����ѡ
            candidates.Add(new Vector2Int(dx, dy));     // �Խ�
            candidates.Add(new Vector2Int(dx, 0));      // ��
            candidates.Add(new Vector2Int(0, dy));      // ��
        }
        else
        {
            // �����
            if (dx != 0) candidates.Add(new Vector2Int(dx, 0));
            if (dy != 0) candidates.Add(new Vector2Int(0, dy));
        }

        // ���Һ�ѡ˳���ڶԽ�/ֱ��֮�������
        for (int i =0;i<candidates.Count;i++)
        {
            int j = Random.Range(i, candidates.Count);
            var tmp = candidates[i]; candidates[i] = candidates[j]; ;candidates[j] = tmp ;
        }

        foreach (var d in candidates) 
        {
            Vector3Int targetCell = baseCell + new Vector3Int(d.x, d.y, 0);
            if (CanOccupyCell(targetCell)) 
            {
                transform.position = GetCellCenterWorld(targetCell);
                if (transform.position == playerTransform.position)
                    transform.position = origin;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ��� 8 �����ƶ� 1 ��
    /// </summary>
    private void TryRandomMove()
    {
        List<Vector2Int> dirs = new List<Vector2Int>(DIR_8);

        for (int i = 0; i < dirs.Count; i++) 
        {
            int j = Random.Range(i, dirs.Count);
            var tmp = dirs[i]; dirs[i] = dirs[j];dirs[j] = tmp;
        }

        Vector3Int baseCell = GetBaseCell();

        foreach (var d in dirs)
        {
            Vector3Int target = baseCell + new Vector3Int(d.x, d.y, 0);
            if (CanOccupyCell(target))
            {
                transform.position = GetCellCenterWorld(target);
                return;
            }
        }
    }

    /// <summary>
    /// �ڵ�ǰ������������webPrefab��
    /// </summary>
    private void CreateWebAtCurrentCell()
    {
        if (webPrefab == null) return;

        Vector3Int baseCell = GetBaseCell();
        Vector3 spawnPos = GetCellCenterWorld(baseCell);
        var go = Instantiate(webPrefab, spawnPos, Quaternion.identity, webParent != null ? webParent : null);
        go.name = "SpiderWeb_" + baseCell.x + "_" + baseCell.y;
    }

    /// <summary>
    /// �жϸ� Cell �Ƿ�ռ��
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    private bool CanOccupyCell(Vector3Int cell)
    {
        if (useTilemapCoords && groundTilemap != null)
            if (!groundTilemap.HasTile(cell)) return false;

        Vector3 world = GetCellCenterWorld(cell);

        int mask = obstacleLayer.value | destructibleLayer.value | animalLayer.value | playerLayer.value;

        var hits = Physics2D.OverlapCircleAll(world, collisionCheckRadius, mask);
        foreach(var h in hits)
        {
            if (h == null) continue;
            if (h.gameObject == this.gameObject) continue;
            return false;
        }
        return true;
    }

    /// <summary>
    /// ��ȡ��ǰ base cell
    /// </summary>
    /// <returns></returns>
    private Vector3Int GetBaseCell()
    {
        if (useTilemapCoords && groundTilemap != null)
            return groundTilemap.WorldToCell(transform.position);
        else
        {
            Vector3 p = transform.position;
            return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 0);
        }
    }

    /// <summary>
    /// ���� cell ����������������
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        if (useTilemapCoords && groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);
        else
            return new Vector3(cell.x, cell.y, transform.position.z);
    }

    /// <summary>
    /// ����ɭ��ֱ��
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    private IEnumerable<Vector3Int> BresenhamLine(Vector3Int start, Vector3Int end)
    {
        int x0 = start.x, y0 = start.y;
        int x1 = end.x, y1 = end.y;
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while(true)
        {
            yield return new Vector3Int(x0, y0, 0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 > -dy) { err -= dy;x0 += sx; }
            if (e2 < dx) { err += dx;y0 += sy; }
        }
    }

    /// <summary>
    /// ����ɱ
    /// </summary>
    public void OnKilled()
    {
        if (isDead) return;
        isDead = true;

        if (spriteRenderer != null && corpseSprite != null)
            spriteRenderer.sprite = corpseSprite;

        if (TurnManager.Instance != null)
            TurnManager.Instance.Unregister(this);

        animalVisibility.useAnimalVisibility = false;

        GetComponent<BoxCollider2D>().size = Vector2.zero;

        this.enabled = false;
    }
    #endregion

}
