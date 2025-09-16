using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Transform))]
public class PlayerCombat : MonoBehaviour
{
    #region < �ֶ� >
    [Header("��������")]
    [Tooltip("��ҵ� Transform ���")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("������� CameraSheke ���")]
    [SerializeField] private CameraShake cameraShake; 
    [Tooltip("������Ƭ��ͼ")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("�ϰ���Ƭ��ͼ")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("С����Knife������")]
    [Tooltip("С������ָʾ��")]
    [SerializeField] private GameObject knifeIndicatorPrefab;
    [Tooltip("С��������ȴ���룩")]
    [SerializeField] private float knifeCooldown= 0.25f;
    [Tooltip("С�������ĸ��Ӿ���")]
    [SerializeField] private int knifeRangeTiles = 1;
    [Tooltip("С������ָʾ���ı�������")]
    [SerializeField] private Vector2 knifeIndicatorScale = Vector2.one;
    [Tooltip("С������ÿ�غϴ���")]
    public int knifeAttackNumber;

    [Header("��ǹ��Shotgun������")]
    [Tooltip("��ǹ����ָʾ��")]
    [SerializeField] private GameObject shotgunIndicatorPrefab;
    [Tooltip("��ǹ������ȴ���룩")]
    [SerializeField] private float shotgunCooldown = 0.25f;
    [Tooltip("��ǹ�����ĸ��Ӿ���")]
    [SerializeField] private int shotgunRangeTiles = 1;
    [Tooltip("��ǹ����ָʾ���ı�������")]
    [SerializeField] private Vector2 shotgunIndicatorScale = Vector2.one;
    [Tooltip("�Ƿ����ð�ס�Ҽ�������׼ģʽ����")]
    [SerializeField] private bool enableShotgun = true;
    [Tooltip("��ס�Ҽ��Ƿ���ʾ��׼��")]
    [SerializeField] private bool useShotgunLine = true;
    [Tooltip("����������׼�ߵ� LineRenderer")]
    [SerializeField] private LineRenderer shotgunLineRenderer;
    [Tooltip("�����ϰ�ʱ�Ƿ�ֹͣ���ƹ���ָʾ��")]
    [SerializeField] private bool shotgunStopAtObstacle = true;
    [Tooltip("�ӵ�����")]
    public int shotgunBulletsCount;

    [Header("�������")]
    [Tooltip("�Ƿ�ʹ�� Tilemap ��������")]
    [SerializeField] private bool useTilemapCoords = true;
    [Tooltip("��ʹ�� Tilemap ��������ʱ�ĸ��������С")]
    [SerializeField] private Vector2 worldTileSize = new Vector2(1f, 1f);
    [Tooltip("����� LayerMask")]
    [SerializeField] private LayerMask animalLayer;
    [Tooltip("�ɴݻ��ϰ��� layerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("���뾶")]
    [SerializeField] private float detectionRadius = 0.35f;

    [Header("���ӻ�")]
    [Tooltip("�Ƿ��� Scene �л��Ƶ�������")]
    [SerializeField] private bool debugDrawGizmos = true;

    // ����ʱ����
    private GameObject knifeIndicatorInstance;
    private GameObject shotgunIndicatorParent;  // �����壬���ڳ���һ�� indicator
    private List<GameObject> shotgunIndicatorInstances = new List<GameObject>();
    private float knifeTimer = 0f;
    private float shotgunTimer = 0f;
    private bool isAimingShotgun = false;

    // ���� tile �����С������ groundTilemap��
    private float cachedTileW = 1f;
    private float cachedTileH = 1f;

    private int currentShotgunBulletsCount;
    private int currentKnifeAttackNumber;

    public int CurrentShotgunBulletsCount => currentShotgunBulletsCount;
    public int CurrentKnifeAttackNumber => currentKnifeAttackNumber;

    // 8 ��ö��
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
        // ��ʱ������ȴ��
        if (knifeTimer > 0f) knifeTimer -= Time.deltaTime;
        if (shotgunTimer > 0f) shotgunTimer -= Time.deltaTime;

        // �������λ��������������
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 playerPos = playerTransform.position;
        Vector2 delta = mouseWorld - playerPos;

        // ������������������ҷ���
        Vector2Int knifeDir = GetNearestCardinal(delta);

        // ������׼״̬
        isAimingShotgun = enableShotgun && (Input.GetMouseButton(1));

        if(isAimingShotgun)
        {
            // ��׼������С���Ӿ�
            HideKnifeIndicator();

            int dirIndex = GetNearestOctantIndex(delta);
            Vector2Int shotgunDir = EIGHT_DIRS[dirIndex];

            // ���²���ʾ��ǹ��׼�Ӿ���line + indicator��
            UpdateShotgunIndicators(shotgunDir);

            // ��ס�Ҽ�ͬʱ�����������ǹ����
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
            // ����׼��������ǹ�Ӿ�
            HideShotgunIndicators();

            // �������С������
            if (Input.GetMouseButton(0))
            {
                // ��ʾ��ս����ָʾ�����������ң�
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

    #region < ָʾ�� >
    /// <summary>
    /// ����ָʾ��ʵ��
    /// </summary>
    private void SetupIndicatorInstances()
    {
        // С��ָʾ��ʵ��
        if(knifeIndicatorPrefab != null)
        {
            knifeIndicatorInstance = Instantiate(knifeIndicatorPrefab, transform);
            knifeIndicatorInstance.name = "KnifeIndicator";
            knifeIndicatorInstance.SetActive(false);
        }

        // ��ǹָʾ��������
        shotgunIndicatorParent = new GameObject("ShotgunIndicators");
        shotgunIndicatorParent.transform.SetParent(transform, false);
        shotgunIndicatorParent.SetActive(false);
    }

    /// <summary>
    /// ����ָʾ����С
    /// </summary>
    private void CacheTileSize()
    {
        if(useTilemapCoords && groundTilemap != null)
        {
            // ȡ�ο� cell
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
    /// ָʾ����λ
    /// </summary>
    /// <param name="go">ָʾ��Ԥ�Ƽ�</param>
    /// <param name="worldPos">��������</param>
    private void PositionIndictor(GameObject go, Vector3 worldPos, Vector2 indicatorScale)
    {
        if (go == null) return;
        go.transform.position = worldPos;
        // ����������Ϊ��ͼ���С�ɱ���
        go.transform.localScale = new Vector3(indicatorScale.x * cachedTileW, indicatorScale.y * cachedTileH, 1f);
        if (!go.activeSelf) go.SetActive(true);
    }

    /// <summary>
    /// ����С������ָʾ��
    /// </summary>
    private void HideKnifeIndicator()
    {
        if (knifeIndicatorInstance != null) knifeIndicatorInstance.SetActive(false);
    }

    /// <summary>
    /// ������ǹ����ָʾ��
    /// </summary>
    private void HideShotgunIndicators()
    {
        shotgunIndicatorParent.SetActive(false);
        if (shotgunLineRenderer != null) shotgunLineRenderer.enabled = false;
        foreach (var ch in shotgunIndicatorInstances) if (ch != null) ch.SetActive(false);
    }
    #endregion

    #region < С������ >
    /// <summary>
    /// ��ȡ�����λ
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
    /// ��ȡĿ�굥Ԫ��������������
    /// </summary>
    /// <param name="dir">С����������</param>
    /// <param name="rangeTile">��Ƭ��Χ</param>
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
    /// ����С������ָʾ��
    /// </summary>
    /// <param name="dir">С����������</param>
    private void UpdateKnifeIndicator(Vector2Int dir)
    {
        if (knifeIndicatorPrefab == null) return;

        Vector3 worldPos = GetTargetWorldPositionForCell(dir, knifeRangeTiles);
        PositionIndictor(knifeIndicatorInstance, worldPos, knifeIndicatorScale);
    }

    /// <summary>
    /// С������
    /// </summary>
    /// <param name="dir">С����������</param>
    private void KnifeAttack(Vector2Int dir)
    {
        if (knifeTimer > 0f) return;
        knifeTimer = knifeCooldown;

        Vector3 targetWorld = GetTargetWorldPositionForCell(dir, knifeRangeTiles);

        // �� Physics2D ��� animal Layer
        Collider2D[] hits;
        if(useTilemapCoords)
        {
            hits = Physics2D.OverlapCircleAll(targetWorld, detectionRadius, animalLayer);
        }
        else
        {
            hits = Physics2D.OverlapCircleAll(targetWorld, detectionRadius, animalLayer);
        }

        // �����ж�����һ��������������Ϣ��
        foreach (var h in hits)
        {
            if (h == null) continue;
            h.SendMessage("OnKilled", SendMessageOptions.DontRequireReceiver);

        }

        // �Կɴݻ��ϰ����˺�/�ݻ�
        if (destructibleLayer != (LayerMask)0)
        {
            // ����ϰ������� Destroy ����
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

    #region < ��ǹ���� >
    /// <summary>
    /// ��ȡ�����λ��8��������
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
    /// ������ǹָʾ��
    /// </summary>
    private void UpdateShotgunIndicators(Vector2Int dir)
    {
        if (!enableShotgun) return;
        CacheTileSize();

        shotgunIndicatorParent.SetActive(true);

        // ����ָ���������ϸ����ֱ������߽�������ϰ���
        var cells = new List<Vector3Int>();
        Vector3Int baseCell = groundTilemap != null ? groundTilemap.WorldToCell(playerTransform.position) : Vector3Int.zero;
        for (int i = 1; i <= shotgunRangeTiles; i++)
        {
            Vector3Int c = baseCell + new Vector3Int(dir.x * i, dir.y * i, 0);
            // �޵���ؿ�ʱֹͣ
            if (useTilemapCoords && groundTilemap != null && !groundTilemap.HasTile(c)) break;

            cells.Add(c);

            // �������ϰ�����ֹͣ
            if (useTilemapCoords && obstacleTilemap != null && obstacleTilemap.HasTile(c))
                if (shotgunStopAtObstacle) break;
        }

        EnsureShotgunIndicators(cells.Count);

        // ����ÿ����Ԫ�����ģ����������꣩��ָʾ��λ��
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 centerWorld = useTilemapCoords && groundTilemap != null ?
                groundTilemap.GetCellCenterWorld(cells[i]) :
                (Vector3)(playerTransform.position + new Vector3(dir.x * i * cachedTileW, dir.y * i * cachedTileH, 0));
            var inst = shotgunIndicatorInstances[i];
            if (inst != null)
                PositionIndictor(inst, centerWorld, shotgunIndicatorScale);
        }

        // ���ó�����Χ��ʵ��
        for (int j = cells.Count; j < shotgunIndicatorInstances.Count; j++)
        {
            if (shotgunIndicatorInstances[j] != null) shotgunIndicatorInstances[j].SetActive(false);
        }

        // ������׼��
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
    /// ������ȷ���㹻�� shotgun indicator ����
    /// </summary>
    /// <param name="count">shotgun indicator ����</param>
    private void EnsureShotgunIndicators(int count)
    {
        // ��� shotgunIndicatorParent δָ����ʹ�� knifeIndicatorPrefab ��Ϊ����
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
    /// ��ǹ����
    /// </summary>
    private void ShotgunAttack(Vector2Int dir)
    {
        if (shotgunTimer > 0f) return;
        shotgunTimer = shotgunCooldown;

        // ���㵥Ԫ��
        List<Vector3Int> cells = new List<Vector3Int>();
        Vector3Int baseCell = groundTilemap != null ? groundTilemap.WorldToCell(playerTransform.position) : Vector3Int.zero;

        for (int i = 1; i <= shotgunRangeTiles; i++)
        {
            Vector3Int c = baseCell + new Vector3Int(dir.x * i, dir.y * i, 0);
            if (useTilemapCoords && groundTilemap != null && !groundTilemap.HasTile(c)) break;
            cells.Add(c);
            if (useTilemapCoords && obstacleTilemap != null && obstacleTilemap.HasTile(c) && shotgunStopAtObstacle) break;
        }

        // �����ж�����һ��������������Ϣ��
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

            // �Կɴݻ��ϰ����˺�/�ݻ�
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
        // �������
        AudioManager.Instance.PlaySFX(1, 0.8f, 1.8f);

        // �������Ч������
        yield return new WaitWhile(() => AudioManager.Instance.sfxAudioSource.isPlaying);

        // ���Ż���
        AudioManager.Instance.PlaySFX(2, 0.8f, 1.8f);
    }
    #endregion

    #region < �������� >

    /// <summary>
    /// ���Ե��������������Ӿ�
    /// </summary>
    public void HideAllIndicators()
    {
        HideKnifeIndicator();
        HideShotgunIndicators();
    }
    #endregion

#if UNITY_EDITOR
    #region < ���ӻ� >
    private void OnDrawGizmos()
    {
        if (!debugDrawGizmos) return;

        // �����������λ��
        if (playerTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform.position, 0.08f);

        // ����ָʾ��Ŀ��
        Vector2 mouseWorld = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition) : (Vector2)playerTransform.position;
        Vector2Int kd = GetNearestCardinal(mouseWorld - (Vector2)playerTransform.position);
        Vector3 kpos = GetTargetWorldPositionForCell(kd, knifeRangeTiles);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(kpos, new Vector3(cachedTileW * 0.9f, cachedTileH * 0.9f, 0.01f));
    }

    #endregion
#endif
}
