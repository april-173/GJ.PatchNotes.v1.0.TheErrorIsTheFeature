using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    #region < 字段 >
    [Header("基础引用")]
    [Tooltip("地面瓦片地图")]
    [SerializeField]private Tilemap groundTilemap;
    [Tooltip("障碍瓦片地图")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("移动属性")]
    [Tooltip("允许移动")]
    [SerializeField] private bool canMove;
    [Tooltip("移动间隔时间")]
    [SerializeField, Range(0.01f, 1f)] private float moveIntervalTime;
    [Tooltip("是否启用平滑移动")]
    [SerializeField] private bool useSmoothMove;
    [Tooltip("平滑移动时间")]
    [SerializeField]private float smoothMoveSpeed;

    [Header("障碍检测")]
    [Tooltip("检测格子时的 Overlap 半径")]
    [SerializeField] private float collisionCheckRadius = 0.15f;
    [Tooltip("可摧毁物体 LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("动物 LayerMask")]
    [SerializeField] private LayerMask animalLayer;


    // 内部状态
    private bool isMoving = false;

    private bool canUpMove;
    private bool canDownMove;
    private bool canLeftMove;
    private bool canRightMove;

    private float moveIntervalTimer;        // 移动间隔计时器

    private Vector3Int currentCell;         // 玩家当前所在的格子
    private Vector3Int targetCell;          // 玩家目标格子
    private Vector3 targetWorldPosition;    // 玩家目标的世界坐标

    private List<KeyCode> keyStack = new List<KeyCode>();   // 优先级栈
    private Vector3Int inputDir = Vector3Int.zero;          // 输入方向
    private KeyCode[] trackedKeys = new KeyCode[]
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow
    };
    #endregion
    private void Start()
    {
        if (groundTilemap == null || obstacleTilemap == null)
        {
            Debug.LogError("[PlayerController] groundTilemap 或 obstacleTilemap 未指定。");
            gameObject.SetActive(false);
            return;
        }

        if(groundTilemap == obstacleTilemap)
        {
            Debug.LogError("[PlayerController] groundTilemap 与 obstacleTilemap 引用异常。");
            gameObject.SetActive(false);
            return;
        }

        // 初始化玩家所在的格子
        currentCell = groundTilemap.WorldToCell(transform.position);
        targetCell = currentCell;
        targetWorldPosition = groundTilemap.GetCellCenterWorld(currentCell);
        transform.position = targetWorldPosition;

        // 初始检测方向可行性
        MoveCheck();
    }

    private void Update()
    {
        // 处理按键按下/抬起事件，维护 keyStack（优先级：栈尾优先）
        HandleKeyStackEvents();
        // 计算当前优先方向
        inputDir = GetDirFromKeyStack();
        // 更新计时器
        Timer();
        // 玩家移动检测
        MoveCheck();
        // 玩家移动总控
        Move();
    }

    #region < 计时器 >
    /// <summary>
    /// 计时器：实时更新计时器
    /// </summary>
    private void Timer()
    {
        if (moveIntervalTimer > 0) moveIntervalTimer -= Time.deltaTime;
    }
    #endregion

    #region < 玩家输入 >
    public void ClearKeyStack()
    {
        keyStack.Clear();
    }

    /// <summary>
    /// 处理按键栈事件：处理 KeyDown / KeyUp 以维护后按优先栈
    /// </summary>
    private void HandleKeyStackEvents()
    {
        foreach (var key in trackedKeys)
        {
            // KeyDown：把 key 放到栈尾（先移除同 key 的旧项）
            if (Input.GetKeyDown(key)) 
            {
                for(int i = keyStack.Count - 1; i >= 0; i--)
                {
                    if (keyStack[i] == key) keyStack.RemoveAt(i);
                }
                keyStack.Add(key);
            }
            // KeyUp：移除该 key（如果抬起，但有同方向的其它按键仍在按住，会保持其他按键在栈中）
            if (Input.GetKeyUp(key))
            {
                for (int i = keyStack.Count - 1; i >= 0; i--)
                {
                    if (keyStack[i] == key) keyStack.RemoveAt(i);
                }
            }
        }
        // 当栈为空但有键被按住（例如在脚本启动前就按着），把第一个检测到的按住键加入栈（保证连续移动能工作）
        if (keyStack.Count == 0)
        {
            foreach (var key in trackedKeys)
            {
                if (Input.GetKey(key))
                {
                    keyStack.Add(key);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 从 keyStack（从尾到头）取第一个有效方向
    /// </summary>
    /// <returns>有效方向</returns>
    private Vector3Int GetDirFromKeyStack()
    {
        for(int i = keyStack.Count -1; i >= 0; i--)
        {
            if (TryKeyToDir(keyStack[i], out Vector3Int dir)) return dir;
        }
        return Vector3Int.zero;
    }

    /// <summary>
    /// 将按键映射为方向
    /// </summary>
    /// <param name="key"></param>
    /// <param name="dir"></param>
    /// <returns></returns>
    private bool TryKeyToDir(KeyCode key,out Vector3Int dir)
    {
        dir = Vector3Int.zero;
        switch(key)
        {
            case KeyCode.W:
            case KeyCode.UpArrow:
                dir = Vector3Int.up;return true;
            case KeyCode.S:
            case KeyCode.DownArrow:
                dir = Vector3Int.down;return true;
            case KeyCode.A:
            case KeyCode.LeftArrow:
                dir = Vector3Int.left;return true;
            case KeyCode.D:
            case KeyCode.RightArrow:
                dir = Vector3Int.right;return true;
            default:
                return false;
        }

    }

    #endregion

    #region < 玩家移动 >
    /// <summary>
    /// 玩家移动总控：集中管理所有有关移动的方法
    /// </summary>
    private void Move()
    {
        Vector3Int dir = inputDir;

        if (canMove && moveIntervalTimer <= 0 && dir != Vector3Int.zero)
        {
            if (dir == Vector3Int.up && canUpMove) Moving();
            if (dir == Vector3Int.down && canDownMove) Moving();
            if (dir == Vector3Int.left && canLeftMove) Moving();
            if (dir == Vector3Int.right && canRightMove) Moving();
        }

        void Moving()
        {
            isMoving = true;

            targetCell = currentCell + dir;
            targetWorldPosition = groundTilemap.GetCellCenterWorld(targetCell);

            if(isMoving && useSmoothMove)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, smoothMoveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, targetWorldPosition) < 0.01f)
                {
                    transform.position = targetWorldPosition;
                    currentCell = targetCell;
                    isMoving = false;
                }
            }
            else
            {
                transform.position = targetWorldPosition;
                currentCell = targetCell;
                isMoving = false;
            }

            moveIntervalTimer = moveIntervalTime;
            TurnManager.Instance.PlayerMoved();

            GetComponent<PlayerCombat>().increaseKnifeAttackNumber(100);

            AudioManager.Instance.PlaySFX(3, 0.08f, 1.8f, false);
        }
    }

    /// <summary>
    /// 移动检测：判断玩家上下左右四个方向能否进行移动
    /// </summary>
    private void MoveCheck()
    {
        Vector3Int up = currentCell + Vector3Int.up;
        Vector3Int down = currentCell + Vector3Int.down;
        Vector3Int left = currentCell + Vector3Int.left;
        Vector3Int right = currentCell + Vector3Int.right;

        canUpMove = groundTilemap.HasTile(up) && !obstacleTilemap.HasTile(up) && CanOccupyCell(up);
        canDownMove = groundTilemap.HasTile(down) && !obstacleTilemap.HasTile(down) && CanOccupyCell(down);
        canLeftMove = groundTilemap.HasTile(left) && !obstacleTilemap.HasTile(left) && CanOccupyCell(left);
        canRightMove = groundTilemap.HasTile(right) && !obstacleTilemap.HasTile(right) && CanOccupyCell(right);
    }

    /// <summary>
    /// 判断该 Cell 是否被占据
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    private bool CanOccupyCell(Vector3Int cell)
    {
        int mask =  destructibleLayer.value | animalLayer.value;
        Vector3 world = groundTilemap.GetCellCenterWorld(cell);

        var hits = Physics2D.OverlapCircleAll(world, collisionCheckRadius, mask);
        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.gameObject == this.gameObject) continue;
            return false;
        }
        return true;
    }
    #endregion

#if UNITY_EDITOR
    #region < 可视化 >
    private void OnDrawGizmos()
    {
        if (groundTilemap == null) return;

        // 计算基准格子（若在编辑器未在运行，使用 transform.position）
        Vector3Int drawBaseCell;
        if (Application.isPlaying)
            drawBaseCell = currentCell;
        else
            drawBaseCell = groundTilemap.WorldToCell(transform.position);

        // 定义半透明颜色
        float alpha = 0.2f;
        Color okColor = new Color(0f, 1f, 0f, alpha);
        Color noColor = new Color(1f, 0f, 0f, alpha);
        float boxSize = 1f;
        Color lineColor = new Color(Color.white.r, Color.white.g, Color.white.b, alpha);

        // 绘制 Up
        Vector3 upCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.up);
        Gizmos.color = (Application.isPlaying ? (canUpMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(upCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(upCenter, Vector3.one * boxSize);

        // 绘制 Down
        Vector3 downCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.down);
        Gizmos.color = (Application.isPlaying ? (canDownMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(downCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(downCenter, Vector3.one * boxSize);

        // 绘制 Left
        Vector3 leftCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.left);
        Gizmos.color = (Application.isPlaying ? (canLeftMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(leftCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(leftCenter, Vector3.one * boxSize);

        // 绘制 Right
        Vector3 rightCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.right);
        Gizmos.color = (Application.isPlaying ? (canRightMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(rightCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(rightCenter, Vector3.one * boxSize);

        // 绘制玩家中心小标记
        Vector3 center = groundTilemap.GetCellCenterWorld(drawBaseCell);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, alpha);
        Gizmos.DrawSphere(center, 0.12f);
        Gizmos.color = lineColor;
        Gizmos.DrawWireSphere(center, 0.12f);
    }
    #endregion
#endif
}
