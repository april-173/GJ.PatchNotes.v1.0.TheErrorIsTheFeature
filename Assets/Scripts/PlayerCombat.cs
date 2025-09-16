using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Transform))]
public class PlayerCombat : MonoBehaviour
{
    #region < 字段 >
    [Header("基础引用")]
    [Tooltip("玩家的 Transform 组件")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("摄像机的 CameraSheke 组件")]
    [SerializeField] private CameraShake cameraShake; 
    [Tooltip("地面瓦片地图")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("障碍瓦片地图")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("小刀（Knife）设置")]
    [Tooltip("小刀攻击指示器")]
    [SerializeField] private GameObject knifeIndicatorPrefab;
    [Tooltip("小刀攻击冷却（秒）")]
    [SerializeField] private float knifeCooldown= 0.25f;
    [Tooltip("小刀攻击的格子距离")]
    [SerializeField] private int knifeRangeTiles = 1;
    [Tooltip("小刀攻击指示器的本地缩放")]
    [SerializeField] private Vector2 knifeIndicatorScale = Vector2.one;
    [Tooltip("小刀攻击每回合次数")]
    public int knifeAttackNumber;

    [Header("猎枪（Shotgun）设置")]
    [Tooltip("猎枪攻击指示器")]
    [SerializeField] private GameObject shotgunIndicatorPrefab;
    [Tooltip("猎枪攻击冷却（秒）")]
    [SerializeField] private float shotgunCooldown = 0.25f;
    [Tooltip("猎枪攻击的格子距离")]
    [SerializeField] private int shotgunRangeTiles = 1;
    [Tooltip("猎枪攻击指示器的本地缩放")]
    [SerializeField] private Vector2 shotgunIndicatorScale = Vector2.one;
    [Tooltip("是否启用按住右键进入瞄准模式功能")]
    [SerializeField] private bool enableShotgun = true;
    [Tooltip("按住右键是否显示瞄准线")]
    [SerializeField] private bool useShotgunLine = true;
    [Tooltip("用来绘制瞄准线的 LineRenderer")]
    [SerializeField] private LineRenderer shotgunLineRenderer;
    [Tooltip("遇到障碍时是否停止绘制攻击指示器")]
    [SerializeField] private bool shotgunStopAtObstacle = true;
    [Tooltip("子弹数量")]
    public int shotgunBulletsCount;

    [Header("检测设置")]
    [Tooltip("是否使用 Tilemap 网格坐标")]
    [SerializeField] private bool useTilemapCoords = true;
    [Tooltip("不使用 Tilemap 网格坐标时的格子世界大小")]
    [SerializeField] private Vector2 worldTileSize = new Vector2(1f, 1f);
    [Tooltip("动物的 LayerMask")]
    [SerializeField] private LayerMask animalLayer;
    [Tooltip("可摧毁障碍的 layerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("检测半径")]
    [SerializeField] private float detectionRadius = 0.35f;

    [Header("可视化")]
    [Tooltip("是否在 Scene 中绘制调试线条")]
    [SerializeField] private bool debugDrawGizmos = true;

    // 运行时数据
    private GameObject knifeIndicatorInstance;
    private GameObject shotgunIndicatorParent;  // 父物体，用于承载一排 indicator
    private List<GameObject> shotgunIndicatorInstances = new List<GameObject>();
    private float knifeTimer = 0f;
    private float shotgunTimer = 0f;
    private bool isAimingShotgun = false;

    // 缓存 tile 世界大小（基于 groundTilemap）
    private float cachedTileW = 1f;
    private float cachedTileH = 1f;

    private int currentShotgunBulletsCount;
    private int currentKnifeAttackNumber;

    public int CurrentShotgunBulletsCount => currentShotgunBulletsCount;
    public int CurrentKnifeAttackNumber => currentKnifeAttackNumber;

    // 8 向枚举
    private static readonly Vector2Int[] EIGHT_DIRS = new Vector2Int[]
    {
        new Vector2Int(1,0),    // 0 = right
        new Vector2Int(1,1),    // 1 = up-right
        new Vector2Int(0,1),    // 2 = up
        new Vector2Int(-1,1),   // 3 = up-left
        new Vector2Int(-1,0),   // 4 = left
        new Vector2Int(-1,-1),  // 5 = down-left
        new Vector2Int(0,-1),   // 6 = down
        new Vector2Int(1,-1),   // 7 = down-right
    };
    #endregion
    private void Start()
    {
        if (playerTransform == null) playerTransform = transform;
        SetupIndicatorInstances();
        CacheTileSize();

        currentShotgunBulletsCount = shotgunBulletsCount;
        currentKnifeAttackNumber = knifeAttackNumber;
    }

    private void OnValidate()
    {
        if (knifeIndicatorScale.x <= 0) knifeIndicatorScale.x = 1f;
        if (knifeIndicatorScale.y <= 0) knifeIndicatorScale.y = 1f;
        if (shotgunIndicatorScale.x <= 0) shotgunIndicatorScale.x = 1f;
        if (shotgunIndicatorScale.y <= 0) shotgunIndicatorScale.y = 1f;
        CacheTileSize();
    }

    private void Update()
    {
        // 计时器（冷却）
        if (knifeTimer > 0f) knifeTimer -= Time.deltaTime;
        if (shotgunTimer > 0f) shotgunTimer -= Time.deltaTime;

        // 鼠标世界位置与玩家相对向量
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 playerPos = playerTransform.position;
        Vector2 delta = mouseWorld - playerPos;

        // 检测玩家最近的上下左右方向
        Vector2Int knifeDir = GetNearestCardinal(delta);

        // 处理瞄准状态
        isAimingShotgun = enableShotgun && (Input.GetMouseButton(1));

        if(isAimingShotgun)
        {
            // 瞄准：隐藏小刀视觉
            HideKnifeIndicator();

            int dirIndex = GetNearestOctantIndex(delta);
            Vector2Int shotgunDir = EIGHT_DIRS[dirIndex];

            // 更新并显示猎枪瞄准视觉（line + indicator）
            UpdateShotgunIndicators(shotgunDir);

            // 按住右键同时按左键触发猎枪攻击
            if (Input.GetMouseButtonDown(0) && haveShotgunBullets() && shotgunTimer <= 0 && knifeTimer <= 0)   
            {
                ShotgunAttack(shotgunDir);

                cameraShake.Shake(0.2f, 25, 0.15f);

                ReduceShotgunBullets(1);

                StartCoroutine(PlayShotgunSFX());
            }
        }
        else
        {
            // 非瞄准：隐藏猎枪视觉
            HideShotgunIndicators();

            // 左键触发小刀攻击
            if (Input.GetMouseButton(0))
            {
                // 显示近战攻击指示器（上下左右）
                UpdateKnifeIndicator(knifeDir);
            }
            else
            {
                HideKnifeIndicator();
            }

            if (Input.GetMouseButtonUp(0) && haveKnifeAttackNumber() && knifeTimer <= 0 && shotgunTimer <= 0)
            {
                KnifeAttack(knifeDir);
                cameraShake.Shake(0.1f, 10, 0.1f);

                ReduceKnifeAttackNumber(1);

                AudioManager.Instance.PlaySFX(0, 0.5f, 1.2f);
            }
        }
    }

    #region < 指示器 >
    /// <summary>
    /// 设置指示器实例
    /// </summary>
    private void SetupIndicatorInstances()
    {
        // 小刀指示器实例
        if(knifeIndicatorPrefab != null)
        {
            knifeIndicatorInstance = Instantiate(knifeIndicatorPrefab, transform);
            knifeIndicatorInstance.name = "KnifeIndicator";
            knifeIndicatorInstance.SetActive(false);
        }

        // 猎枪指示器父物体
        shotgunIndicatorParent = new GameObject("ShotgunIndicators");
        shotgunIndicatorParent.transform.SetParent(transform, false);
        shotgunIndicatorParent.SetActive(false);
    }

    /// <summary>
    /// 缓存指示器大小
    /// </summary>
    private void CacheTileSize()
    {
        if(useTilemapCoords && groundTilemap != null)
        {
            // 取参考 cell
            Vector3Int refCell = groundTilemap.WorldToCell(transform.position);
            Vector3 center = groundTilemap.GetCellCenterWorld(refCell);
            Vector3 right = groundTilemap.GetCellCenterWorld(refCell + Vector3Int.right);
            Vector3 up = groundTilemap.GetCellCenterWorld(refCell + Vector3Int.up);
            cachedTileW = Mathf.Abs(right.x - center.x);
            cachedTileH = Mathf.Abs(up.y - center.y);
            if (cachedTileW <= 0) cachedTileW = 1f;
            if (cachedTileH <= 0) cachedTileH = 1f;
        }
        else
        {
            cachedTileW = worldTileSize.x;
            cachedTileH = worldTileSize.y;
        }
    }

    /// <summary>
    /// 指示器方位
    /// </summary>
    /// <param name="go">指示器预制件</param>
    /// <param name="worldPos">世界坐标</param>
    private void PositionIndictor(GameObject go, Vector3 worldPos, Vector2 indicatorScale)
    {
        if (go == null) return;
        go.transform.position = worldPos;
        // 将比例设置为与图块大小成比例
        go.transform.localScale = new Vector3(indicatorScale.x * cachedTileW, indicatorScale.y * cachedTileH, 1f);
        if (!go.activeSelf) go.SetActive(true);
    }

    /// <summary>
    /// 隐藏小刀攻击指示器
    /// </summary>
    private void HideKnifeIndicator()
    {
        if (knifeIndicatorInstance != null) knifeIndicatorInstance.SetActive(false);
    }

    /// <summary>
    /// 隐藏猎枪攻击指示器
    /// </summary>
    private void HideShotgunIndicators()
    {
        shotgunIndicatorParent.SetActive(false);
        if (shotgunLineRenderer != null) shotgunLineRenderer.enabled = false;
        foreach (var ch in shotgunIndicatorInstances) if (ch != null) ch.SetActive(false);
    }
    #endregion

    #region < 小刀攻击 >
    /// <summary>
    /// 获取最近方位
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    private Vector2Int GetNearestCardinal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x >= 0 ? Vector2Int.right : Vector2Int.left;
        else
            return delta.y >= 0 ? Vector2Int.up : Vector2Int.down;
    }

    /// <summary>
    /// 获取目标单元格世界坐标中心
    /// </summary>
    /// <param name="dir">小刀攻击距离</param>
    /// <param name="rangeTile">瓦片范围</param>
    /// <returns></returns>
    private Vector3 GetTargetWorldPositionForCell(Vector2Int dir,int rangeTile)
    {
        if(useTilemapCoords && groundTilemap != null)
        {
            Vector3Int baseCell = groundTilemap.WorldToCell(playerTransform.position);
            Vector3Int targetCell = baseCell + new Vector3Int(dir.x * rangeTile, dir.y * rangeTile, 0);
            return groundTilemap.GetCellCenterWorld(targetCell);
        }
        else
        {
            Vector3 basePos = playerTransform.position;
            Vector3 offset = new Vector3(dir.x * rangeTile * cachedTileW, dir.y * rangeTile * cachedTileH, 0);
            return basePos + offset;
        }
    }

    /// <summary>
    /// 更新小刀攻击指示器
    /// </summary>
    /// <param name="dir">小刀攻击距离</param>
    private void UpdateKnifeIndicator(Vector2Int dir)
    {
        if (knifeIndicatorPrefab == null) return;

        Vector3 worldPos = GetTargetWorldPositionForCell(dir, knifeRangeTiles);
        PositionIndictor(knifeIndicatorInstance, worldPos, knifeIndicatorScale);
    }

    /// <summary>
    /// 小刀攻击
    /// </summary>
    /// <param name="dir">小刀攻击距离</param>
    private void KnifeAttack(Vector2Int dir)
    {
        if (knifeTimer > 0f) return;
        knifeTimer = knifeCooldown;

        Vector3 targetWorld = GetTargetWorldPositionForCell(dir, knifeRangeTiles);

        // 用 Physics2D 检测 animal Layer
        Collider2D[] hits;
        if(useTilemapCoords)
        {
            hits = Physics2D.OverlapCircleAll(targetWorld, detectionRadius, animalLayer);
        }
        else
        {
            hits = Physics2D.OverlapCircleAll(targetWorld, detectionRadius, animalLayer);
        }

        // 对命中动物逐一处理（发送死亡消息）
        foreach (var h in hits)
        {
            if (h == null) continue;
            h.SendMessage("OnKilled", SendMessageOptions.DontRequireReceiver);

        }

        // 对可摧毁障碍的伤害/摧毁
        if (destructibleLayer != (LayerMask)0)
        {
            // 检测障碍并发送 Destroy 请求
            Collider2D[] obstacles = Physics2D.OverlapCircleAll(targetWorld, detectionRadius, destructibleLayer);
            foreach (var o in obstacles)
            {
                o.SendMessage("OnDestroyed", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void ReduceKnifeAttackNumber(int count)
    {
        if (currentKnifeAttackNumber - count < 0 || count <= 0)
            return;

        currentKnifeAttackNumber -= count;
    }

    public void increaseKnifeAttackNumber(int count)
    {
        if (count <= 0)
            return;

        if (currentKnifeAttackNumber + count > knifeAttackNumber)
        {
            currentKnifeAttackNumber = knifeAttackNumber;
            return;
        }


        currentKnifeAttackNumber += count;
    }

    private bool haveKnifeAttackNumber()
    {
        return currentKnifeAttackNumber > 0;
    }
    #endregion

    #region < 猎枪攻击 >
    /// <summary>
    /// 获取最近方位（8向索引）
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    private int GetNearestOctantIndex(Vector2 delta)
    {
        float angle = Mathf.Atan2(delta.y, delta.x);
        float deg = angle * Mathf.Rad2Deg;
        if (deg < 0) deg += 360f;
        int idx = Mathf.RoundToInt(deg / 45f) % 8;
        return idx;
    }

    /// <summary>
    /// 更新猎枪指示器
    /// </summary>
    private void UpdateShotgunIndicators(Vector2Int dir)
    {
        if (!enableShotgun) return;
        CacheTileSize();

        shotgunIndicatorParent.SetActive(true);

        // 沿着指定方向计算细胞，直至到达边界或遇到障碍物
        var cells = new List<Vector3Int>();
        Vector3Int baseCell = groundTilemap != null ? groundTilemap.WorldToCell(playerTransform.position) : Vector3Int.zero;
        for (int i = 1; i <= shotgunRangeTiles; i++)
        {
            Vector3Int c = baseCell + new Vector3Int(dir.x * i, dir.y * i, 0);
            // 无地面地块时停止
            if (useTilemapCoords && groundTilemap != null && !groundTilemap.HasTile(c)) break;

            cells.Add(c);

            // 若存在障碍物则停止
            if (useTilemapCoords && obstacleTilemap != null && obstacleTilemap.HasTile(c))
                if (shotgunStopAtObstacle) break;
        }

        EnsureShotgunIndicators(cells.Count);

        // 设置每个单元格中心（或世界坐标）的指示器位置
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 centerWorld = useTilemapCoords && groundTilemap != null ?
                groundTilemap.GetCellCenterWorld(cells[i]) :
                (Vector3)(playerTransform.position + new Vector3(dir.x * i * cachedTileW, dir.y * i * cachedTileH, 0));
            var inst = shotgunIndicatorInstances[i];
            if (inst != null)
                PositionIndictor(inst, centerWorld, shotgunIndicatorScale);
        }

        // 禁用超出范围的实例
        for (int j = cells.Count; j < shotgunIndicatorInstances.Count; j++)
        {
            if (shotgunIndicatorInstances[j] != null) shotgunIndicatorInstances[j].SetActive(false);
        }

        // 绘制瞄准线
        if(useShotgunLine && shotgunLineRenderer != null)
        {
            shotgunLineRenderer.enabled = true;
            Vector3 start = playerTransform.position;
            Vector3 end;
            if (cells.Count > 0)
                end = useTilemapCoords && groundTilemap != null ?
                groundTilemap.GetCellCenterWorld(cells[cells.Count - 1]) :
                playerTransform.position + new Vector3(dir.x * shotgunRangeTiles * cachedTileW, dir.y * shotgunRangeTiles * cachedTileH, 0);
            else
                end = playerTransform.position + new Vector3(dir.x * shotgunRangeTiles * cachedTileW, dir.y * shotgunRangeTiles * cachedTileH, 0);

            shotgunLineRenderer.positionCount = 2;
            shotgunLineRenderer.SetPosition(0, start);
            shotgunLineRenderer.SetPosition(1, end);
        }
        else if(shotgunLineRenderer != null)
        {
            shotgunLineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 创建并确保足够的 shotgun indicator 数量
    /// </summary>
    /// <param name="count">shotgun indicator 数量</param>
    private void EnsureShotgunIndicators(int count)
    {
        // 如果 shotgunIndicatorParent 未指定，使用 knifeIndicatorPrefab 作为备用
        GameObject prefab = shotgunIndicatorParent != null ? shotgunIndicatorPrefab : knifeIndicatorPrefab;
        if (prefab == null) return;

        for (int i = shotgunIndicatorInstances.Count; i < count; i++) 
        {
            GameObject go = Instantiate(prefab, shotgunIndicatorParent.transform);
            go.name = "ShotgunIndicator_" + i;
            go.SetActive(false);
            shotgunIndicatorInstances.Add(go);
        }
    }

    /// <summary>
    /// 猎枪攻击
    /// </summary>
    private void ShotgunAttack(Vector2Int dir)
    {
        if (shotgunTimer > 0f) return;
        shotgunTimer = shotgunCooldown;

        // 计算单元格
        List<Vector3Int> cells = new List<Vector3Int>();
        Vector3Int baseCell = groundTilemap != null ? groundTilemap.WorldToCell(playerTransform.position) : Vector3Int.zero;

        for (int i = 1; i <= shotgunRangeTiles; i++)
        {
            Vector3Int c = baseCell + new Vector3Int(dir.x * i, dir.y * i, 0);
            if (useTilemapCoords && groundTilemap != null && !groundTilemap.HasTile(c)) break;
            cells.Add(c);
            if (useTilemapCoords && obstacleTilemap != null && obstacleTilemap.HasTile(c) && shotgunStopAtObstacle) break;
        }

        // 对命中动物逐一处理（发送死亡消息）
        foreach (var c in cells)
        {
            Vector3 world = useTilemapCoords && groundTilemap != null ?
                groundTilemap.GetCellCenterWorld(c) :
                (Vector3)(playerTransform.position + new Vector3(c.x * cachedTileW, c.y * cachedTileH, 0));
            Collider2D[] hits = Physics2D.OverlapCircleAll(world, detectionRadius, animalLayer);
            foreach (var h in hits)
            {
                if (h == null) continue;
                h.SendMessage("OnKilled", SendMessageOptions.DontRequireReceiver);
            }

            // 对可摧毁障碍的伤害/摧毁
            if (destructibleLayer != (LayerMask)0)
            {
                Collider2D[] obs = Physics2D.OverlapCircleAll(world, detectionRadius, destructibleLayer);
                foreach (var o in obs)
                {
                    o.SendMessage("OnDestroyed", SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    private void ReduceShotgunBullets(int count)
    {
        if (currentShotgunBulletsCount - count < 0 || count <= 0) 
            return;

        currentShotgunBulletsCount -= count;
    }

    public void increaseShotgunBullets(int count)
    {
        if (count <= 0)
            return;

        AudioManager.Instance.PlaySFX(4, 0.2f, 1f);

        if (currentShotgunBulletsCount + count > shotgunBulletsCount)
        {
            currentShotgunBulletsCount = shotgunBulletsCount;
            return;
        }

        currentShotgunBulletsCount += count;
    }

    private bool haveShotgunBullets()
    {
        return currentShotgunBulletsCount > 0;
    }

    private IEnumerator PlayShotgunSFX()
    {
        // 播放射击
        AudioManager.Instance.PlaySFX(1, 0.8f, 1.8f);

        // 等射击音效播放完
        yield return new WaitWhile(() => AudioManager.Instance.sfxAudioSource.isPlaying);

        // 播放换弹
        AudioManager.Instance.PlaySFX(2, 0.8f, 1.8f);
    }
    #endregion

    #region < 辅助方法 >

    /// <summary>
    /// 可以调用来隐藏所有视觉
    /// </summary>
    public void HideAllIndicators()
    {
        HideKnifeIndicator();
        HideShotgunIndicators();
    }
    #endregion

#if UNITY_EDITOR
    #region < 可视化 >
    private void OnDrawGizmos()
    {
        if (!debugDrawGizmos) return;

        // 绘制玩家中心位置
        if (playerTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform.position, 0.08f);

        // 绘制指示器目标
        Vector2 mouseWorld = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition) : (Vector2)playerTransform.position;
        Vector2Int kd = GetNearestCardinal(mouseWorld - (Vector2)playerTransform.position);
        Vector3 kpos = GetTargetWorldPositionForCell(kd, knifeRangeTiles);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(kpos, new Vector3(cachedTileW * 0.9f, cachedTileH * 0.9f, 0.01f));
    }

    #endregion
#endif
}
