using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SpiderAI : MonoBehaviour, ITurnActor
{
    #region < 字段 >
    [Header("权重")]
    [Tooltip("移动权重")]
    [SerializeField] private int moveWeight = 60;
    [Tooltip("待机权重")]
    [SerializeField] private int idleWeight = 30;
    [Tooltip("结网权重")]
    [SerializeField] private int webWeight = 10;

    [Header("Tilemap 支持")]
    [Tooltip("是否使用 Tilemap 网格坐标")]
    [SerializeField] private bool useTilemapCoords = true;
    [Tooltip("地面 Tilemap")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("障碍 Tilemap")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("检测/层设置")]
    [Tooltip("障碍 LayerMask")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("可摧毁物体 LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("动物 LayerMask")]
    [SerializeField] private LayerMask animalLayer;
    [Tooltip("玩家 LayerMask")]
    [SerializeField] private LayerMask playerLayer;

    [Header("生成对象")]
    [Tooltip("蜘蛛网的预制体")]
    [SerializeField] private GameObject webPrefab;
    [Tooltip("蜘蛛网的父物体")]
    [SerializeField] private Transform webParent;

    [Header("尸体显示")]
    [Tooltip("正常状态的 Sprite")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("死亡后使用的尸体 Sprite")]
    [SerializeField] private Sprite corpseSprite;

    [Header("参数")]
    [Tooltip("检测格子时的 Overlap 半径")]
    [SerializeField] private float collisionCheckRadius = 0.15f;

    [Header("引用组件")]
    [Tooltip("玩家 Transform")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("自身 SpriteRenderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("自身 AnimalVisibility")]
    [SerializeField] private AnimalVisibility animalVisibility;

    // 内部状态
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
            Debug.LogWarning("[SpiderAI] 未找到 TurnManager，请确保场景中有 TurnManager。");
    }

    private void OnDestroy()
    {
        // 注销防止残留
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

    #region < 逻辑管理 >
    /// <summary>
    /// TurnManager 调用：执行本回合逻辑
    /// </summary>
    public IEnumerator TakeTurn()
    {
        if (isDead) yield break;

        // 若玩家在相邻八格 -> 攻击
        Collider2D playerCollider;
        if(IsPlayerAdjacent(out playerCollider))
        {
            AttackPlayer(playerCollider);

            AudioManager.Instance.PlayAnimalSFX(0, 0.5f, 3f);
            yield break;
        }

        // 若有视野（无障碍直视玩家），尝试朝玩家方向移动
        if (playerTransform != null && HasLineOfSightToPlayer())
        {
            bool moveToward = TryMoveTowardsPlayer();
            if (moveToward)
                yield break;
        }

        // 随机决策（基于权重）
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

    #region < AI逻辑 >
    /// <summary>
    /// 检测玩家是否在 8 邻域内，如果是返回该玩家的 collider
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
    /// 攻击玩家
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
            // Raycast 检查 obstacleLayer 是否在两者间阻挡
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
    /// 尝试朝玩家方向移动
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
            // 当既有 x 又有 y 偏移时，优先把对角/直向/竖向都放入候选
            candidates.Add(new Vector2Int(dx, dy));     // 对角
            candidates.Add(new Vector2Int(dx, 0));      // 横
            candidates.Add(new Vector2Int(0, dy));      // 竖
        }
        else
        {
            // 横或竖
            if (dx != 0) candidates.Add(new Vector2Int(dx, 0));
            if (dy != 0) candidates.Add(new Vector2Int(0, dy));
        }

        // 打乱候选顺序（在对角/直向之间随机）
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
    /// 随机 8 向尝试移动 1 格
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
    /// 在当前格子生成网（webPrefab）
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
    /// 判断该 Cell 是否被占据
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
    /// 获取当前 base cell
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
    /// 根据 cell 返回世界坐标中心
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
    /// 布雷森汉直线
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
    /// 被击杀
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
