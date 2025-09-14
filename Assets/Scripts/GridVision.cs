using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GridVision : MonoBehaviour
{
    #region < �ֶ� >
    [Header("��������")]
    [Tooltip("������Ƭ��ͼ")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("�ϰ���Ƭ��ͼ")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("��Ӱͼ��")]
    [Tooltip("���ڻ�����Ӱ�� Tilemap")]
    [SerializeField] private Tilemap shadowTilemap;
    [Tooltip("������ shadowTilemap �� Tile")]
    [SerializeField] private TileBase shadowTile;
    [Tooltip("��Ӱ��ɫ")]
    [SerializeField] private Color shadowColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("��Ұ����")]
    [Tooltip("��Ұ�뾶")]
    [SerializeField][Min(0)] private int viewRadius = 12;
    [Tooltip("�Ƿ�������Ұ��ת����")]
    public bool useInvertVision = false;
    [Tooltip("�Ƿ������ڵ��ϰ������ʾ����")]
    public bool useRevealBlockingObstacles = true;

    [Header("�ű���Ϊ")]
    [Tooltip("�Ƿ�ÿ֡ǿ�Ƹ�����Ұ")]
    [SerializeField] private bool useForceUpdateEveryFrame = false;

    [Header("��Ұ���")]
    [Tooltip("�ɴݻ����� LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("������ʱ�� Overlap �뾶")]
    [SerializeField] private float collisionCheckRadius = 0.15f;

    // ����ʱ����
    private Vector3Int playerCell;  // ��ǰ��Ҹ���
    private Vector3Int lastPlayerCell = new Vector3Int(int.MinValue, int.MinValue,int.MinValue);
    private HashSet<Vector3Int> visibleCells = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> prevShadowCells = new HashSet<Vector3Int>();
    private List<Vector3Int> circleOffsets = new List<Vector3Int>();

    private float tileWorldW = 1f;
    private float tileWorldH = 1f;

    // ��һ֡ inspector/����ʱ����ֵ�����ڼ������仯������ˢ�£�
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
        // �ڱ༭����������ʱҲ�ܿ���Ч��
        if (viewRadius < 0) viewRadius = 0;
        PrecomputeCircleOffsets();

        EditorApplication.delayCall += () =>
        {
            // this != null ��֤���󻹴���
            // Application.isPlaying == false ��ֻ֤�ڱ༭��ģʽ��Ч
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

        // ��������ؼ�����������ʱ�Ƿ������ű��޸�
        if (useInvertVision != prevUseInvertVision || useRevealBlockingObstacles != prevUseRevealBlockingObstacles)
        {
            // ����ǰֵ��ǿ��ˢ��
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

    #region < Ԥ���� >
    /// <summary>
    /// ��֤����ʼ��
    /// </summary>
    private void ValidateAndInit()
    {
        if (groundTilemap == null || obstacleTilemap == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[GridVision] groundTilemap �� obstacleTilemap δָ����");
                enabled = false;
            }
            else
            {
                Debug.LogWarning("[GridVision] ���� Inspector ָ�� groundTilemap �� obstacleTilemap��");
            }
        }
    }

    /// <summary>
    /// ������ҰԲ��ƫ����
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
    /// ������Ƭ������ߴ�
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

    #region < ������Ұ >

    /// <summary>
    /// ǿ��ˢ�£����� Inspector �Ҽ�������е��ã�
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
    /// ���㵱ǰ�ɼ����Ӽ���
    /// </summary>
    private void UpdateVision()
    {
        visibleCells.Clear();
        foreach(var offset in circleOffsets)
        {
            var cell = playerCell + offset;
            // �����ǵ��浥Ԫ��
            if(!groundTilemap.HasTile(cell)) continue;
            if (HasLineOfSight(playerCell, cell)) 
                visibleCells.Add(cell);
        }
    }

    /// <summary>
    /// �ж� from->to �Ƿ�������ߣ��м���������ϰ����赲����Ŀ����ӱ����Կɼ���
    /// </summary>
    /// <param name="from">�������</param>
    /// <param name="to">�����յ�</param>
    /// <returns></returns>
    private bool HasLineOfSight(Vector3Int from, Vector3Int to)
    {
        foreach (var c in BresenhamLine(from, to))
        {
            // ������ʼ��Ԫ��
            if (c == from) continue;
            // ����Ѿ��ﵽĿ�꣬��ֹͣ��飨��������ϰ��Ŀ�걾���Կ��ܿɼ���
            if(useRevealBlockingObstacles)
                if (c == to) break;

            if (obstacleTilemap.HasTile(c)) return false;

            if (!CanOccupyCell(c)) return false;
        }
        return true;
    }

    /// <summary>
    /// ����ɭ��ֱ��
    /// </summary>
    /// <param name="start">ֱ�����</param>
    /// <param name="end">ֱ���յ�</param>
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

        // ������Ӱ���ϣ����ڰ뾶��Χ�ڣ�
        var shadowSet = new HashSet<Vector3Int>();
        foreach (var offset in circleOffsets) 
        {
            var cell = playerCell + offset;
            // �����ǵ��浥Ԫ��
            if (!groundTilemap.HasTile(cell)) continue;

            bool isVisible = visibleCells.Contains(cell);
            if (useInvertVision) isVisible = !isVisible;

            bool isShadowed = !isVisible;
            if (isShadowed) shadowSet.Add(cell);
        }

        // ��ǿ�ƽ���ȫ����д�������������ǰ���ݣ�Ȼ��ȫ���������롣
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

        // �Ƴ�������Ӱ�ڵ������Ѳ�������ӰӰ�����Ƭ
        foreach (var prev in new List<Vector3Int>(prevShadowCells))
        {
            if (!shadowSet.Contains(prev))
            {
                shadowTilemap.SetTile(prev, null);
                prevShadowCells.Remove(prev);
            }
        }

        // ����±���Ӱ���ǵ���Ƭ
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
    /// �жϸ� Cell �Ƿ�ռ��
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
    /// �ⲿ��ѯ��ĳ�����Ƿ�ɼ�
    /// </summary>
    public bool IsCellVisible(Vector3Int cell)
    {
        bool v = visibleCells.Contains(cell);
        return useInvertVision ? !v : v;
    }

    #endregion

#if UNITY_EDITOR
    #region < ���ӻ� >
    private void OnDrawGizmos()
    {
        if (groundTilemap == null) return;

        // ����༭ģʽ�µı�����ҵ�Ԫ��
        Vector3Int drawBaseCell;
        if (Application.isPlaying) drawBaseCell = playerCell;
        else drawBaseCell = groundTilemap.WorldToCell(transform.position);

        // �����δ�����ͼ���С������м���
        if (tileWorldW <= 0 || tileWorldH <= 0) ComputeTileWorldSize();

        float boxSize = 0.9f;
        float boxW = tileWorldW * boxSize;
        float boxH = tileWorldH * boxSize;
        float alpha = 0.2f;
        Color visCol = new Color(0f, 1f, 0f, alpha);
        Color invCol = new Color(1f, 0f, 0f, alpha);

        // ���δ���в��Ų�����������δ������ɼ���Ԫ����Ϣ,��ʱֻ����ư뾶����
        bool haveVisible = Application.isPlaying && visibleCells.Count > 0;

        // ����ƫ����
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

