using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public class CameraController:MonoBehaviour
{
    #region < 字段 >
    [Header("基础引用")]
    [Tooltip("玩家的 Transform 组件")]
    [SerializeField] private Transform follow;
    [Tooltip("用于单元格/世界转换的图层")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("自身的 Camera 组件")]
    [SerializeField] private Camera targetCamera;

    [Header("区块设置")]
    [Tooltip("区块宽度（以瓦片为单位）")]
    [SerializeField] private int chunkWidth = 33;
    [Tooltip("区块高度（以瓦片为单位）")]
    [SerializeField] private int chunkHeight = 16;

    [Header("摄像机行为")]
    [Tooltip("是否启用摄像机的平滑移动")]
    [SerializeField] private bool useSmoothTransition = false;
    [Tooltip("平滑移动速度")]
    [SerializeField] private float smoothTransitionSpeed = 8f;
    [Tooltip("是否启用启动时自动对齐至区块位置")]
    [SerializeField] private bool useSnapToChunkOnStart = true;

    // 内部状态
    private int currentChunkX, currentChunkY;       // 当前区块坐标
    private Vector3 targetPosition;                 // 目标位置
    private bool isMoving = false;                  // 是否正在移动

    private int lastScreenW = 0, lastScreenH = 0;   // 根据屏幕尺寸进行跟踪，以便在需要时对分辨率的变化做出响应
    #endregion
    private void Start()
    {
        if(follow == null)
        {
            Debug.LogError("[CameraController] follow 未指定。");
            gameObject.SetActive(false);
            return;
        }
        if (groundTilemap == null)
        {
            Debug.LogError("[CameraController] groundTilemap 未指定。");
            gameObject.SetActive(false);
            return;
        }
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>() ?? Camera.main;
            if(targetCamera == null )
            {
                Debug.LogError("[CameraController] 未找到 Camera 组件，请指定 targetCamera 或添加 Camera 组件。");
                gameObject.SetActive(false);
                return;
            }
        }

        // 计算当前摄像机“所属”的区块（依据摄像机中心单元格）
        Vector3 cameraWorld = transform.position;
        Vector3Int cameraCell = groundTilemap.WorldToCell(cameraWorld);
        currentChunkX = FloorDiv(cameraCell.x, Mathf.Max(1, chunkWidth));
        currentChunkY = FloorDiv(cameraCell.y, Mathf.Max(1, chunkHeight));

        // 确定初始摄像机位置：对准块的中心，或保持当前摄像机位置
        UpdateTargetPositionFromChunk(currentChunkX, currentChunkY);
        if (useSnapToChunkOnStart)
        {
            transform.position = targetPosition;
            isMoving = false;
        }

        // 初始化屏幕跟踪
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
    }
    private void LateUpdate()
    {
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
        }

        CameraFollow();
    }

    #region < 相机跟随 >
    /// <summary>
    /// 相机跟随总控
    /// </summary>
    private void CameraFollow()
    {
        if (isMoving)
        {
            if (useSmoothTransition)
            {
                // 利用指数插值实现平滑过渡
                transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-smoothTransitionSpeed * Time.deltaTime));
                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
                {
                    transform.position = targetPosition;
                    isMoving = false;
                }
            }
            else
            {
                transform.position = targetPosition;
                isMoving = false;
            }
            return;
        }

        // 计算摄像机在世界坐标系中的矩形位置（当前帧）
        Rect camRect = GetCameraWorldRect();

        // 如果玩家仍在摄像机视野范围内，则不采取任何行动
        Vector3 playerWorld = follow.position;
        if (camRect.Contains(new Vector2(playerWorld.x, playerWorld.y))) return;

        // 玩家离开摄像机矩形区域 -> 计算玩家所在的区域，并将摄像机移动至该区域的中心位置
        Vector3Int playerCell = groundTilemap.WorldToCell(playerWorld);
        int playerChunkX = FloorDiv(playerCell.x, Mathf.Max(1, chunkWidth));
        int playerChunkY = FloorDiv(playerCell.y, Mathf.Max(1, chunkHeight));

        // 设置新的目标区块
        currentChunkX = playerChunkX;
        currentChunkY = playerChunkY;
        UpdateTargetPositionFromChunk(currentChunkX, currentChunkY);

        // 开始移动
        isMoving = true;
    }

    /// <summary>
    /// 根据当前摄像机的位置、正交尺寸以及屏幕比例，以世界单位计算摄像机视图的矩形区域。
    /// </summary>
    /// <returns></returns>
    private Rect GetCameraWorldRect()
    {
        float halfHeight = targetCamera.orthographicSize;
        float aspect = (float)Screen.width / (float)Screen.height;
        float halfWidth = halfHeight * aspect;
        Vector3 cam = transform.position;
        return new Rect(cam.x - halfWidth, cam.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
    }

    /// <summary>
    /// 根据区块的索引（X 坐标、Y 坐标）计算区块中心的“世界坐标”位置。
    /// 通过使用两个角块的图元单元中心来实现，因此能够很好地适应网格偏移情况。
    /// </summary>
    /// <param name="chunkX">区块X轴坐标</param>
    /// <param name="chunkY">区块Y轴坐标</param>
    private void UpdateTargetPositionFromChunk(int chunkX,int chunkY)
    {
        Vector3Int chunkOriginCell = new Vector3Int(chunkX * chunkWidth, chunkY * chunkHeight, 0);
        Vector3Int chunkOppositeCell = chunkOriginCell + new Vector3Int(chunkWidth - 1, chunkHeight - 1, 0);

        Vector3 worldA = groundTilemap.GetCellCenterWorld(chunkOriginCell);
        Vector3 worldB = groundTilemap.GetCellCenterWorld(chunkOppositeCell);

        Vector3 chunkCenter = (worldA + worldB) * 0.5f;
        targetPosition = new Vector3(chunkCenter.x,chunkCenter.y,transform.position.z);
    }

    /// <summary>
    /// 使用基于浮点数的向下取整方式进行除法运算，以正确处理负数情况
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private int FloorDiv(int a,int b)
    {
        return Mathf.FloorToInt((float)a / (float)b);
    }

    #endregion

#if UNITY_EDITOR
    #region < 可视化 >
    private void OnDrawGizmosSelected()
    {
        if (groundTilemap == null || follow == null) return;
        if (targetCamera == null) targetCamera = Camera.main;

        Rect camRect = GetCameraWorldRect();
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.12f);
        Gizmos.DrawCube(new Vector3(camRect.center.x, camRect.center.y, 0f), new Vector3(camRect.width, camRect.height, 0.01f));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(camRect.center.x, camRect.center.y, 0f), new Vector3(camRect.width, camRect.height, 0.01f));

        Vector3Int camCell = groundTilemap.WorldToCell(transform.position);
        int cx = FloorDiv(camCell.x, Mathf.Max(1, chunkWidth));
        int cy = FloorDiv(camCell.y, Mathf.Max(1, chunkHeight));
        Vector3Int origin = new Vector3Int(cx * chunkWidth, cy * chunkHeight, 0);
        Vector3Int opp = origin + new Vector3Int(chunkWidth - 1, chunkHeight - 1, 0);
        Vector3 a = groundTilemap.GetCellCenterWorld(origin);
        Vector3 b = groundTilemap.GetCellCenterWorld(opp);

        float tileSpanX = Mathf.Abs(b.x - a.x) / (chunkWidth - 1 == 0 ? 1 : (chunkWidth - 1));
        float tileSpanY = Mathf.Abs(b.y - a.y) / (chunkHeight - 1 == 0 ? 1 : (chunkHeight - 1));
        Vector3 bottomLeft = a - new Vector3(tileSpanX * 0.5f, tileSpanY * 0.5f, 0f);
        Vector3 topRight = b + new Vector3(tileSpanX * 0.5f, tileSpanY * 0.5f, 0f);
        Vector3 center = (bottomLeft + topRight) * 0.5f;
        Vector3 size = new Vector3(Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y), 0.01f);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.12f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }
    #endregion
#endif
}