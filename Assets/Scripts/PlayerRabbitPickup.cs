using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// PlayerRabbitPickup (������)
/// - ֻʰȡ������ Rabbit��ͨ�� RabbitAI ���ʶ��
/// - ����ʹ�� transform.root��������ø��������������
/// - ֧�� pickAllInRange / ֻʰ���һ��
/// - ��ѡ���Ƕ����� SetActive(false)��Ĭ�ϣ����ǽ����ø� RabbitAI ����ײ��/��Ⱦ��
/// </summary>
public class PlayerRabbitPickup : MonoBehaviour
{
    [Header("ʰȡ����")]
    [SerializeField] private KeyCode pickKey = KeyCode.E;

    [Header("���� / �������")]
    [SerializeField] private bool useTilemapCoords = true;
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("��ⵥԪ��ʱ�ڸ������ļ��İ뾶��World ��λ��")]
    [SerializeField] private float cellCheckRadius = 0.18f;
    [Tooltip("�������ڵ� LayerMask")]
    [SerializeField] private LayerMask animalLayer;

    [Header("ʰȡѡ��")]
    [Tooltip("�Ƿ�ʰȡ�÷�Χ���������ӣ�false = ֻʰȡ�����һ����")]
    [SerializeField] private bool pickAllInRange = false;
    [Tooltip("ʰȡ���Ƿ����� GameObject SetActive(false)")]
    [SerializeField] private bool deactivateOnPickup = true;

    [Header("ͳ�ƣ�ֻ����")]
    [SerializeField] private int rabbitCount = 0;

    [Header("����")]
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

        // ���� 8 �������
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector3Int cell = baseCell + new Vector3Int(dx, dy, 0);
                Vector3 center = GetCellCenterWorld(cell);

                Collider2D[] hits = Physics2D.OverlapCircleAll(center, cellCheckRadius, animalLayer);
                if (hits == null || hits.Length == 0) continue;

                // ֻ��ӵ�� RabbitAI �Ķ�����뼯��
                for (int i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (hit == null) continue;

                    // �����ڸ����в��� RabbitAI�������� collider ���Ӷ���Ҳ����ʶ��
                    var rabbit = hit.GetComponentInParent<RabbitAI>();
                    if (rabbit != null)
                    {
                        // ������� RabbitAI ʵ�����뼯�ϣ�HashSet �Զ�ȥ�أ�
                        foundSet.Add(rabbit);
                    }
                }
            }
        }

        if (foundSet.Count == 0)
        {
            if (debugLog) Debug.Log("[PlayerRabbitPickup] ��Χû�п�ʰȡ�����ӡ�");
            return;
        }

        if (pickAllInRange)
        {
            // ���ʰȡ
            foreach (var rabbit in foundSet)
            {
                DoPickup(rabbit);
            }
        }
        else
        {
            // ֻʰȡ�����һ��
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
    /// �Ե��� RabbitAI ʵ��ִ��ʰȡ���� TurnManager ע����Ȼ�����ػ���ø����ӣ���Ӱ���ʵ����
    /// </summary>
    private void DoPickup(RabbitAI rabbit)
    {
        if (rabbit == null) return;

        AudioManager.Instance.PlayAnimalSFX(2, 1f, 1f);

        GameObject rabbitGO = rabbit.gameObject;

        // ע�� TurnManager����ʵ���� ITurnActor��
        if (TurnManager.Instance != null)
        {
            if (rabbit is ITurnActor actor)
            {
                TurnManager.Instance.Unregister(actor);
            }
        }
        else
        {
            if (debugLog) Debug.LogWarning("[PlayerRabbitPickup] TurnManager.Instance Ϊ null���޷�ע�������ӵĻغϹ���");
        }

        // ����/���ø�����
        if (deactivateOnPickup)
        {
            rabbitGO.SetActive(false);
        }
        else
        {
            // ���� RabbitAI������ AnimalVisibility�����У������� collider �� renderer
            // ���� AI �ű�
            rabbit.enabled = false;

            // AnimalVisibility �����������
            var av = rabbit.GetComponent<AnimalVisibility>();
            if (av != null) av.enabled = false;

            // ���������� Collider2D
            var cols = rabbitGO.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in cols) if (c != null) c.enabled = false;

            // ������Ⱦ
            var srs = rabbitGO.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs) if (sr != null) sr.enabled = false;
        }

        // ���¼�������־
        rabbitCount++;
        if (debugLog)
            Debug.Log($"[PlayerRabbitPickup] ��ʰȡ���ӣ�{rabbitGO.name}����ǰ���� = {rabbitCount}");
    }

    #region ������������
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

