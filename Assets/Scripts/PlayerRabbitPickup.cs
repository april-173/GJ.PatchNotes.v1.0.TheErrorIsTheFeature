using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// PlayerRabbitPickup (修正版)
/// - 只拾取真正的 Rabbit（通过 RabbitAI 组件识别）
/// - 不再使用 transform.root，避免禁用父对象或其它动物
/// - 支持 pickAllInRange / 只拾最近一个
/// - 可选择是对兔子 SetActive(false)（默认）还是仅禁用该 RabbitAI 与碰撞器/渲染器
/// </summary>
public class PlayerRabbitPickup : MonoBehaviour
{
    [Header("拾取按键")]
    [SerializeField] private KeyCode pickKey = KeyCode.E;

    [Header("网格 / 检测设置")]
    [SerializeField] private bool useTilemapCoords = true;
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("检测单元格时在格子中心检测的半径（World 单位）")]
    [SerializeField] private float cellCheckRadius = 0.18f;
    [Tooltip("动物所在的 LayerMask")]
    [SerializeField] private LayerMask animalLayer;

    [Header("拾取选项")]
    [Tooltip("是否拾取该范围内所有兔子（false = 只拾取最近的一个）")]
    [SerializeField] private bool pickAllInRange = false;
    [Tooltip("拾取后是否将兔子 GameObject SetActive(false)")]
    [SerializeField] private bool deactivateOnPickup = true;

    [Header("统计（只读）")]
    [SerializeField] private int rabbitCount = 0;

    [Header("调试")]
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool debugDrawGizmos = true;

    public int RabbitCount => rabbitCount;

    private void Update()
    {
        if (Input.GetKeyDown(pickKey))
        {
            TryPickupAround();
        }
    }

    private void TryPickupAround()
    {
        Vector3Int baseCell = GetBaseCell();
        HashSet<RabbitAI> foundSet = new HashSet<RabbitAI>();

        // 遍历 8 邻域格子
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector3Int cell = baseCell + new Vector3Int(dx, dy, 0);
                Vector3 center = GetCellCenterWorld(cell);

                Collider2D[] hits = Physics2D.OverlapCircleAll(center, cellCheckRadius, animalLayer);
                if (hits == null || hits.Length == 0) continue;

                // 只把拥有 RabbitAI 的对象加入集合
                for (int i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (hit == null) continue;

                    // 优先在父链中查找 RabbitAI（这样若 collider 在子对象也可以识别）
                    var rabbit = hit.GetComponentInParent<RabbitAI>();
                    if (rabbit != null)
                    {
                        // 将具体的 RabbitAI 实例加入集合（HashSet 自动去重）
                        foundSet.Add(rabbit);
                    }
                }
            }
        }

        if (foundSet.Count == 0)
        {
            if (debugLog) Debug.Log("[PlayerRabbitPickup] 周围没有可拾取的兔子。");
            return;
        }

        if (pickAllInRange)
        {
            // 逐个拾取
            foreach (var rabbit in foundSet)
            {
                DoPickup(rabbit);
            }
        }
        else
        {
            // 只拾取最近的一个
            RabbitAI nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 myPos = transform.position;

            foreach (var r in foundSet)
            {
                if (r == null) continue;
                float sq = (r.transform.position - myPos).sqrMagnitude;
                if (sq < bestSqr)
                {
                    bestSqr = sq;
                    nearest = r;
                }
            }

            if (nearest != null) DoPickup(nearest);
        }
    }

    /// <summary>
    /// 对单个 RabbitAI 实例执行拾取：从 TurnManager 注销，然后隐藏或禁用该兔子（仅影响该实例）
    /// </summary>
    private void DoPickup(RabbitAI rabbit)
    {
        if (rabbit == null) return;

        AudioManager.Instance.PlayAnimalSFX(2, 1f, 1f);

        GameObject rabbitGO = rabbit.gameObject;

        // 注销 TurnManager（若实现了 ITurnActor）
        if (TurnManager.Instance != null)
        {
            if (rabbit is ITurnActor actor)
            {
                TurnManager.Instance.Unregister(actor);
            }
        }
        else
        {
            if (debugLog) Debug.LogWarning("[PlayerRabbitPickup] TurnManager.Instance 为 null，无法注销该兔子的回合管理。");
        }

        // 隐藏/禁用该兔子
        if (deactivateOnPickup)
        {
            rabbitGO.SetActive(false);
        }
        else
        {
            // 禁用 RabbitAI、禁用 AnimalVisibility（如有）、禁用 collider 与 renderer
            // 禁用 AI 脚本
            rabbit.enabled = false;

            // AnimalVisibility 若存在则禁用
            var av = rabbit.GetComponent<AnimalVisibility>();
            if (av != null) av.enabled = false;

            // 禁用所有子 Collider2D
            var cols = rabbitGO.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in cols) if (c != null) c.enabled = false;

            // 禁用渲染
            var srs = rabbitGO.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs) if (sr != null) sr.enabled = false;
        }

        // 更新计数与日志
        rabbitCount++;
        if (debugLog)
            Debug.Log($"[PlayerRabbitPickup] 已拾取兔子：{rabbitGO.name}。当前计数 = {rabbitCount}");
    }

    #region 格子坐标助手
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

    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        if (useTilemapCoords && groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);
        else
            return new Vector3(cell.x, cell.y, transform.position.z);
    }
    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmos) return;

        Vector3Int baseCell = useTilemapCoords && groundTilemap != null ?
            groundTilemap.WorldToCell(transform.position) :
            new Vector3Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y), 0);

        Gizmos.color = Color.yellow;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector3Int c = baseCell + new Vector3Int(dx, dy, 0);
                Vector3 center = useTilemapCoords && groundTilemap != null ? groundTilemap.GetCellCenterWorld(c) : new Vector3(c.x, c.y, transform.position.z);
                Gizmos.DrawWireSphere(center, cellCheckRadius);
            }
        }
    }
#endif
}

