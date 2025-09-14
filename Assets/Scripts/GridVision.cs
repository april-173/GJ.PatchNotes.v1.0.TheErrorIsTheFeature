using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GridVision : MonoBehaviour
{
    #region < 字段 >
    [Header("基础引用")]
    [Tooltip("地面瓦片地图")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("障碍瓦片地图")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("阴影图层")]
    [Tooltip("用于绘制阴影的 Tilemap")]
    [SerializeField] private Tilemap shadowTilemap;
    [Tooltip("放置于 shadowTilemap 的 Tile")]
    [SerializeField] private TileBase shadowTile;
    [Tooltip("阴影颜色")]
    [SerializeField] private Color shadowColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("视野设置")]
    [Tooltip("视野半径")]
    [SerializeField][Min(0)] private int viewRadius = 12;
    [Tooltip("是否启用视野反转功能")]
    public bool useInvertVision = false;
    [Tooltip("是否启用遮挡障碍物的显示功能")]
    public bool useRevealBlockingObstacles = true;

    [Header("脚本行为")]
    [Tooltip("是否每帧强制更新视野")]
    [SerializeField] private bool useForceUpdateEveryFrame = false;

    [Header("视野检测")]
    [Tooltip("可摧毁物体 LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("检测格子时的 Overlap 半径")]
    [SerializeField] private float collisionCheckRadius = 0.15f;

    // 运行时数据
    private Vector3Int playerCell;  // 当前玩家格子
    private Vector3Int lastPlayerCell = new Vector3Int(int.MinValue, int.MinValue,int.MinValue);
    private HashSet<Vector3Int> visibleCells = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> prevShadowCells = new HashSet<Vector3Int>();
    private List<Vector3Int> circleOffsets = new List<Vector3Int>();

    private float tileWorldW = 1f;
    private float tileWorldH = 1f;

    // 上一帧 inspector/运行时布尔值（用于检测变量变化并触发刷新）
    private bool prevUseInvertVision;
    private bool prevUseRevealBlockingObstacles;

    #endregion

    private void Start()
    {
        ValidateAndInit();
        PrecomputeCircleOffsets();
        ComputeTileWorldSize();
        ForceRefresh();

        prevUseInvertVision = useInvertVision;
        prevUseRevealBlockingObstacles = useRevealBlockingObstacles;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 在编辑器调整参数时也能看到效果
        if (viewRadius < 0) viewRadius = 0;
        PrecomputeCircleOffsets();

        EditorApplication.delayCall += () =>
        {
            // this != null 保证对象还存在
            // Application.isPlaying == false 保证只在编辑器模式生效
            if (this != null && !Application.isPlaying)
            {
                ForceRefresh();
            }
        };
    }
#endif

    private void Update()
    {
        if (groundTilemap == null || obstacleTilemap == null) return;

        // 检测两个关键布尔在运行时是否被其他脚本修改
        if (useInvertVision != prevUseInvertVision || useRevealBlockingObstacles != prevUseRevealBlockingObstacles)
        {
            // 更新前值并强制刷新
            prevUseInvertVision = useInvertVision;
            prevUseRevealBlockingObstacles = useRevealBlockingObstacles;
            ForceRefresh();
        }

        playerCell = groundTilemap.WorldToCell(transform.position);
        if(useForceUpdateEveryFrame || playerCell != lastPlayerCell)
        {
            UpdateVision();
            ApplyShadowToTilemap();
            lastPlayerCell = playerCell;
        }
    }

    #region < 预计算 >
    /// <summary>
    /// 验证并初始化
    /// </summary>
    private void ValidateAndInit()
    {
        if (groundTilemap == null || obstacleTilemap == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[GridVision] groundTilemap 或 obstacleTilemap 未指定。");
                enabled = false;
            }
            else
            {
                Debug.LogWarning("[GridVision] 请在 Inspector 指定 groundTilemap 与 obstacleTilemap。");
            }
        }
    }

    /// <summary>
    /// 计算视野圆形偏移量
    /// </summary>
    private void PrecomputeCircleOffsets()
    {
        circleOffsets.Clear();
        int r = Mathf.Max(0, viewRadius);
        int rr = r * r;
        for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
                if (x * x + y * y <= rr)
                    circleOffsets.Add(new Vector3Int(x, y, 0));
    }
    /// <summary>
    /// 计算瓦片的世界尺寸
    /// </summary>
    private void ComputeTileWorldSize()
    {
        if (groundTilemap == null) return;

        Vector3Int refCell = Vector3Int.zero;
        if (Application.isPlaying)
            refCell = groundTilemap.WorldToCell(transform.position);
        else
            refCell = groundTilemap.WorldToCell(Vector3.zero);

        Vector3 c = groundTilemap.GetCellCenterWorld(refCell);
        Vector3 cr = groundTilemap.GetCellCenterWorld(refCell + Vector3Int.right);
        Vector3 cu = groundTilemap.GetCellCenterWorld(refCell + Vector3Int.up);
        tileWorldW = Mathf.Abs(cr.x - c.x);
        tileWorldH = Mathf.Abs(cu.y - c.y);
        if (tileWorldW <= 0) tileWorldW = 1f;
        if (tileWorldH <= 0) tileWorldH = 1f;
    }
    #endregion

    #region < 网格视野 >

    /// <summary>
    /// 强制刷新（可在 Inspector 右键或代码中调用）
    /// </summary>
    [ContextMenu("Force Refresh Vision")]
    public void ForceRefresh()
    {
        if (groundTilemap == null || obstacleTilemap == null) return;
        playerCell = groundTilemap.WorldToCell(transform.position);
        PrecomputeCircleOffsets();
        UpdateVision();
        ApplyShadowToTilemap(true);
        lastPlayerCell = playerCell;
    }

    /// <summary>
    /// 计算当前可见格子集合
    /// </summary>
    private void UpdateVision()
    {
        visibleCells.Clear();
        foreach(var offset in circleOffsets)
        {
            var cell = playerCell + offset;
            // 跳过非地面单元格
            if(!groundTilemap.HasTile(cell)) continue;
            if (HasLineOfSight(playerCell, cell)) 
                visibleCells.Add(cell);
        }
    }

    /// <summary>
    /// 判断 from->to 是否存在视线：中间格子若有障碍则阻挡，但目标格子本身仍可见。
    /// </summary>
    /// <param name="from">视线起点</param>
    /// <param name="to">视线终点</param>
    /// <returns></returns>
    private bool HasLineOfSight(Vector3Int from, Vector3Int to)
    {
        foreach (var c in BresenhamLine(from, to))
        {
            // 忽略起始单元格
            if (c == from) continue;
            // 如果已经达到目标，就停止检查（即便存在障碍物，目标本身仍可能可见）
            if(useRevealBlockingObstacles)
                if (c == to) break;

            if (obstacleTilemap.HasTile(c)) return false;

            if (!CanOccupyCell(c)) return false;
        }
        return true;
    }

    /// <summary>
    /// 布雷森汉直线
    /// </summary>
    /// <param name="start">直线起点</param>
    /// <param name="end">直线终点</param>
    /// <returns></returns>
    private IEnumerable<Vector3Int> BresenhamLine(Vector3Int start,Vector3Int end)
    {
        int x0 = start.x, y0 = start.y;
        int x1 = end.x, y1 = end.y;
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            yield return new Vector3Int(x0, y0, 0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private void ApplyShadowToTilemap(bool forceFullRewrite = false)
    {
        if (shadowTilemap == null)
        { 
            prevShadowCells.Clear(); 
            return; 
        }

        // 计算阴影集合（仅在半径范围内）
        var shadowSet = new HashSet<Vector3Int>();
        foreach (var offset in circleOffsets) 
        {
            var cell = playerCell + offset;
            // 跳过非地面单元格
            if (!groundTilemap.HasTile(cell)) continue;

            bool isVisible = visibleCells.Contains(cell);
            if (useInvertVision) isVisible = !isVisible;

            bool isShadowed = !isVisible;
            if (isShadowed) shadowSet.Add(cell);
        }

        // 若强制进行全部重写：先清除所有先前内容，然后全部重新输入。
        if (forceFullRewrite) 
        {
            foreach (var prev in prevShadowCells)
                shadowTilemap.SetTile(prev, null);
            prevShadowCells.Clear();
            foreach(var s in shadowSet)
            {
                shadowTilemap.SetTile(s, shadowTile);
                shadowTilemap.SetTileFlags(s, TileFlags.None);
                shadowTilemap.SetColor(s, shadowColor);
            }
            prevShadowCells = shadowSet;
            return;
        }

        // 移除曾被阴影遮挡但现已不再受阴影影响的瓦片
        foreach (var prev in new List<Vector3Int>(prevShadowCells))
        {
            if (!shadowSet.Contains(prev))
            {
                shadowTilemap.SetTile(prev, null);
                prevShadowCells.Remove(prev);
            }
        }

        // 添加新被阴影覆盖的瓦片
        foreach(var s in shadowSet)
        {
            if(!prevShadowCells.Contains(s))
            {
                shadowTilemap.SetTile(s, shadowTile);
                shadowTilemap.SetTileFlags(s, TileFlags.None);
                shadowTilemap.SetColor(s, shadowColor);
                prevShadowCells.Add(s);
            }
        }
    }

    /// <summary>
    /// 判断该 Cell 是否被占据
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    private bool CanOccupyCell(Vector3Int cell)
    {
        Vector3 world = groundTilemap.GetCellCenterWorld(cell);

        int mask = destructibleLayer.value;

        var hits = Physics2D.OverlapCircleAll(world, collisionCheckRadius, mask);
        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.gameObject == this.gameObject) continue;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 外部查询：某格子是否可见
    /// </summary>
    public bool IsCellVisible(Vector3Int cell)
    {
        bool v = visibleCells.Contains(cell);
        return useInvertVision ? !v : v;
    }

    #endregion

#if UNITY_EDITOR
    #region < 可视化 >
    private void OnDrawGizmos()
    {
        if (groundTilemap == null) return;

        // 计算编辑模式下的本地玩家单元格
        Vector3Int drawBaseCell;
        if (Application.isPlaying) drawBaseCell = playerCell;
        else drawBaseCell = groundTilemap.WorldToCell(transform.position);

        // 如果尚未计算出图块大小，则进行计算
        if (tileWorldW <= 0 || tileWorldH <= 0) ComputeTileWorldSize();

        float boxSize = 0.9f;
        float boxW = tileWorldW * boxSize;
        float boxH = tileWorldH * boxSize;
        float alpha = 0.2f;
        Color visCol = new Color(0f, 1f, 0f, alpha);
        Color invCol = new Color(1f, 0f, 0f, alpha);

        // 如果未进行播放操作，可能尚未计算出可见单元格信息,此时只需绘制半径即可
        bool haveVisible = Application.isPlaying && visibleCells.Count > 0;

        // 迭代偏移量
        foreach(var offset in circleOffsets)
        {
            var cell = drawBaseCell + offset;
            if(!groundTilemap.HasTile(cell)) continue;
            Vector3 center = groundTilemap.GetCellCenterWorld(cell);

            bool isVisible = haveVisible && visibleCells.Contains(cell);
            if (useInvertVision && haveVisible) isVisible = !isVisible;

            Color c = (haveVisible ? (isVisible ? visCol : invCol) : new Color(0.5f, 0.5f, 0.5f, alpha / 2));
            Gizmos.color = c;
            Gizmos.DrawCube(center, new Vector3(boxW, boxH, 0.01f));
            Gizmos.color = Color.white * 0.6f;
            Gizmos.DrawWireCube(center, new Vector3(boxW, boxH, 0.01f));
        }
    }


    #endregion
#endif
}

