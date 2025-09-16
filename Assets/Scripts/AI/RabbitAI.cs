using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RabbitAI : MonoBehaviour, ITurnActor
{
    #region < 字段 >
    [Header("权重")]
    [Tooltip("待机权重")]
    [SerializeField] private int idleWeight = 60;
    [Tooltip("1格移动（8向）权重")]
    [SerializeField] private int move1Weight = 25;
    [Tooltip("2格移动（4向）权重")]
    [SerializeField] private int move2Weight = 15;

    [Header("Tilemap 支持")]
    [Tooltip("是否使用 Tilemap 网格坐标")]
    [SerializeField] private bool useTilemapCoords = true;
    [Tooltip("地面 Tilemap")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("障碍 Tilemap")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("检测 / 层设置")]
    [Tooltip("障碍 LayerMask（场景中障碍物所属 layer）")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("可摧毁物体 LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("动物 LayerMask（包含其它动物）")]
    [SerializeField] private LayerMask animalLayer;
    [Tooltip("玩家 LayerMask")]
    [SerializeField] private LayerMask playerLayer;

    [Header("行为参数")]
    [Tooltip("两格移动时第一格与第二格之间的停顿时间（秒）")]
    [SerializeField] private float stepPause = 0.12f;
    [Tooltip("检测格子时的 Overlap 半径（world units）")]
    [SerializeField] private float collisionCheckRadius = 0.15f;

    [Header("尸体显示 & 渲染")]
    [Tooltip("正常状态的 Sprite")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("死亡后使用的尸体 Sprite")]
    [SerializeField] private Sprite corpseSprite;
    [Tooltip("自身 SpriteRenderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("自身 AnimalVisibility")]
    [SerializeField] private AnimalVisibility animalVisibility;

    [Header("引用")]
    [SerializeField] private Transform playerTransform;

    // 运行时状态
    private bool isDead = false;

    // 方向集（与 SpiderAI 一致）
    private static readonly Vector2Int[] DIR_8 = new Vector2Int[]
    {
        new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1),
        new Vector2Int(-1,0), new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1,-1)
    };
    private static readonly Vector2Int[] DIR_4 = new Vector2Int[]
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };
    #endregion

    private void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && normalSprite != null)
            spriteRenderer.sprite = normalSprite;

        if (animalVisibility == null)
            animalVisibility = GetComponent<AnimalVisibility>();

        if (TurnManager.Instance != null)
            TurnManager.Instance.Register(this);
        else
            Debug.LogWarning("[RabbitAI] 未找到 TurnManager，请确保场景中有 TurnManager。");
    }

    private void OnDestroy()
    {
        // 注销
        if (TurnManager.Instance != null)
            TurnManager.Instance.Unregister(this);
    }

    //private bool b = true;

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
    #region < 回合逻辑 >
    /// <summary>
    /// 被 TurnManager 调用：执行本回合逻辑
    /// 简化决策：根据权重随机选择 Idle / Move1 / Move2
    /// Move1（8向）为瞬移 1 格
    /// Move2（4向）为尝试沿某方向移动两格：先瞬移到第一格 -> 等待 stepPause -> 再尝试第二格
    /// </summary>
    public IEnumerator TakeTurn()
    {
        if (isDead) yield break;

        int total = Mathf.Max(1, idleWeight + move1Weight + move2Weight);
        int roll = Random.Range(0, total);

        if (roll < idleWeight)
        {
            // Idle（什么都不做）
            yield break;
        }
        else if (roll < idleWeight + move1Weight)
        {
            // Move1：随机 8 向移动 1 格
            TryRandomMove1();
            yield break;
        }
        else
        {
            // Move2：随机 4 向尝试移动 2 格（带停顿）
            yield return TryRandomMove2Coroutine();
            yield break;
        }
    }
    #endregion

    #region < 行为实现 >
    /// <summary>
    /// 随机 8 向选择一个可占据的格子并瞬移 1 格
    /// </summary>
    private void TryRandomMove1()
    {
        List<Vector2Int> dirs = new List<Vector2Int>(DIR_8);
        // Fisher-Yates shuffle
        for (int i = 0; i < dirs.Count; i++)
        {
            int j = Random.Range(i, dirs.Count);
            var tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp;
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
        // 若无可行方向则 Idle
    }

    /// <summary>
    /// 随机 4 向尝试移动两格（协程：第一格瞬移 + 等待 + 第二格尝试）
    /// </summary>
    private IEnumerator TryRandomMove2Coroutine()
    {
        List<Vector2Int> dirs = new List<Vector2Int>(DIR_4);
        for (int i = 0; i < dirs.Count; i++)
        {
            int j = Random.Range(i, dirs.Count);
            var tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp;
        }

        Vector3Int baseCell = GetBaseCell();

        foreach (var d in dirs)
        {
            Vector3Int firstCell = baseCell + new Vector3Int(d.x, d.y, 0);
            Vector3Int secondCell = baseCell + new Vector3Int(d.x * 2, d.y * 2, 0);

            // 先确保第一格可占（否则尝试其它方向）
            if (!CanOccupyCell(firstCell)) continue;

            // 移动到第一格
            transform.position = GetCellCenterWorld(firstCell);

            // 等待一个短暂停顿（给玩家视觉反馈）
            if (stepPause > 0f) yield return new WaitForSeconds(stepPause);

            // 尝试第二格（如果可占则移动过去）
            if (CanOccupyCell(secondCell))
            {
                transform.position = GetCellCenterWorld(secondCell);
            }

            // 无论第二格是否成功，完成本回合（不要继续尝试其它方向）
            yield break;
        }

        // 若所有方向都不可行，则 Idle
        yield break;
    }
    #endregion

    #region < 工具 / 检测 >
    /// <summary>
    /// 判断该 Cell 是否可被占据（不与障碍、可摧毁物、其它动物或玩家重叠）
    /// 更稳健地忽略自身（忽略同一 root）
    /// </summary>
    private bool CanOccupyCell(Vector3Int cell)
    {
        if (useTilemapCoords && groundTilemap != null)
        {
            if (!groundTilemap.HasTile(cell)) return false;
        }

        Vector3 world = GetCellCenterWorld(cell);

        int mask = obstacleLayer.value | destructibleLayer.value | animalLayer.value | playerLayer.value;

        var hits = Physics2D.OverlapCircleAll(world, collisionCheckRadius, mask);
        foreach (var h in hits)
        {
            if (h == null) continue;
            // 忽略自身以及自身子物体
            if (h.transform.root == transform.root) continue;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 获取当前基础格子（网格坐标）
    /// </summary>
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
    /// 根据 cell 返回 世界坐标中心
    /// </summary>
    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        if (useTilemapCoords && groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);
        else
            return new Vector3(cell.x, cell.y, transform.position.z);
    }
    #endregion

    #region < 死亡处理 >
    /// <summary>
    /// 被击杀：切换尸体 sprite、注销 TurnManager、禁用可见性、禁用碰撞并停用行为脚本（保持对象用于尸体展示）
    /// </summary>
    public void OnKilled()
    {
        if (isDead) return;
        isDead = true;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && corpseSprite != null)
            spriteRenderer.sprite = corpseSprite;

        // 注销 TurnManager
        if (TurnManager.Instance != null)
            TurnManager.Instance.Unregister(this);

        // 若有 AnimalVisibility，尝试禁用其显示控制
        if (animalVisibility != null)
        {
            try
            {
                var field = animalVisibility.GetType().GetField("useAnimalVisibility",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null) field.SetValue(animalVisibility, false);
            }
            catch { }
        }

        // 禁用碰撞：若有 BoxCollider2D 则把大小归零，否则直接禁用所有 Collider2D
        var box = GetComponent<BoxCollider2D>();
        if (box != null) box.size = Vector2.zero;
        else
        {
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols) if (c != null) c.enabled = false;
        }

        // 禁用脚本（停止进一步行动）
        this.enabled = false;
    }
    #endregion
}

