using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RabbitAI : MonoBehaviour, ITurnActor
{
    #region < �ֶ� >
    [Header("Ȩ��")]
    [Tooltip("����Ȩ��")]
    [SerializeField] private int idleWeight = 60;
    [Tooltip("1���ƶ���8��Ȩ��")]
    [SerializeField] private int move1Weight = 25;
    [Tooltip("2���ƶ���4��Ȩ��")]
    [SerializeField] private int move2Weight = 15;

    [Header("Tilemap ֧��")]
    [Tooltip("�Ƿ�ʹ�� Tilemap ��������")]
    [SerializeField] private bool useTilemapCoords = true;
    [Tooltip("���� Tilemap")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("�ϰ� Tilemap")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("��� / ������")]
    [Tooltip("�ϰ� LayerMask���������ϰ������� layer��")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("�ɴݻ����� LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("���� LayerMask�������������")]
    [SerializeField] private LayerMask animalLayer;
    [Tooltip("��� LayerMask")]
    [SerializeField] private LayerMask playerLayer;

    [Header("��Ϊ����")]
    [Tooltip("�����ƶ�ʱ��һ����ڶ���֮���ͣ��ʱ�䣨�룩")]
    [SerializeField] private float stepPause = 0.12f;
    [Tooltip("������ʱ�� Overlap �뾶��world units��")]
    [SerializeField] private float collisionCheckRadius = 0.15f;

    [Header("ʬ����ʾ & ��Ⱦ")]
    [Tooltip("����״̬�� Sprite")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("������ʹ�õ�ʬ�� Sprite")]
    [SerializeField] private Sprite corpseSprite;
    [Tooltip("���� SpriteRenderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("���� AnimalVisibility")]
    [SerializeField] private AnimalVisibility animalVisibility;

    [Header("����")]
    [SerializeField] private Transform playerTransform;

    // ����ʱ״̬
    private bool isDead = false;

    // ���򼯣��� SpiderAI һ�£�
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
            Debug.LogWarning("[RabbitAI] δ�ҵ� TurnManager����ȷ���������� TurnManager��");
    }

    private void OnDestroy()
    {
        // ע��
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
    #region < �غ��߼� >
    /// <summary>
    /// �� TurnManager ���ã�ִ�б��غ��߼�
    /// �򻯾��ߣ�����Ȩ�����ѡ�� Idle / Move1 / Move2
    /// Move1��8��Ϊ˲�� 1 ��
    /// Move2��4��Ϊ������ĳ�����ƶ�������˲�Ƶ���һ�� -> �ȴ� stepPause -> �ٳ��Եڶ���
    /// </summary>
    public IEnumerator TakeTurn()
    {
        if (isDead) yield break;

        int total = Mathf.Max(1, idleWeight + move1Weight + move2Weight);
        int roll = Random.Range(0, total);

        if (roll < idleWeight)
        {
            // Idle��ʲô��������
            yield break;
        }
        else if (roll < idleWeight + move1Weight)
        {
            // Move1����� 8 ���ƶ� 1 ��
            TryRandomMove1();
            yield break;
        }
        else
        {
            // Move2����� 4 �����ƶ� 2 �񣨴�ͣ�٣�
            yield return TryRandomMove2Coroutine();
            yield break;
        }
    }
    #endregion

    #region < ��Ϊʵ�� >
    /// <summary>
    /// ��� 8 ��ѡ��һ����ռ�ݵĸ��Ӳ�˲�� 1 ��
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
        // ���޿��з����� Idle
    }

    /// <summary>
    /// ��� 4 �����ƶ�����Э�̣���һ��˲�� + �ȴ� + �ڶ����ԣ�
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

            // ��ȷ����һ���ռ����������������
            if (!CanOccupyCell(firstCell)) continue;

            // �ƶ�����һ��
            transform.position = GetCellCenterWorld(firstCell);

            // �ȴ�һ������ͣ�٣�������Ӿ�������
            if (stepPause > 0f) yield return new WaitForSeconds(stepPause);

            // ���Եڶ��������ռ���ƶ���ȥ��
            if (CanOccupyCell(secondCell))
            {
                transform.position = GetCellCenterWorld(secondCell);
            }

            // ���۵ڶ����Ƿ�ɹ�����ɱ��غϣ���Ҫ����������������
            yield break;
        }

        // �����з��򶼲����У��� Idle
        yield break;
    }
    #endregion

    #region < ���� / ��� >
    /// <summary>
    /// �жϸ� Cell �Ƿ�ɱ�ռ�ݣ������ϰ����ɴݻ���������������ص���
    /// ���Ƚ��غ�����������ͬһ root��
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
            // ���������Լ�����������
            if (h.transform.root == transform.root) continue;
            return false;
        }

        return true;
    }

    /// <summary>
    /// ��ȡ��ǰ�������ӣ��������꣩
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
    /// ���� cell ���� ������������
    /// </summary>
    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        if (useTilemapCoords && groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);
        else
            return new Vector3(cell.x, cell.y, transform.position.z);
    }
    #endregion

    #region < �������� >
    /// <summary>
    /// ����ɱ���л�ʬ�� sprite��ע�� TurnManager�����ÿɼ��ԡ�������ײ��ͣ����Ϊ�ű������ֶ�������ʬ��չʾ��
    /// </summary>
    public void OnKilled()
    {
        if (isDead) return;
        isDead = true;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && corpseSprite != null)
            spriteRenderer.sprite = corpseSprite;

        // ע�� TurnManager
        if (TurnManager.Instance != null)
            TurnManager.Instance.Unregister(this);

        // ���� AnimalVisibility�����Խ�������ʾ����
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

        // ������ײ������ BoxCollider2D ��Ѵ�С���㣬����ֱ�ӽ������� Collider2D
        var box = GetComponent<BoxCollider2D>();
        if (box != null) box.size = Vector2.zero;
        else
        {
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols) if (c != null) c.enabled = false;
        }

        // ���ýű���ֹͣ��һ���ж���
        this.enabled = false;
    }
    #endregion
}

