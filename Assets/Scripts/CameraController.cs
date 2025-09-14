using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public class CameraController:MonoBehaviour
{
    #region < �ֶ� >
    [Header("��������")]
    [Tooltip("��ҵ� Transform ���")]
    [SerializeField] private Transform follow;
    [Tooltip("���ڵ�Ԫ��/����ת����ͼ��")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("����� Camera ���")]
    [SerializeField] private Camera targetCamera;

    [Header("��������")]
    [Tooltip("�����ȣ�����ƬΪ��λ��")]
    [SerializeField] private int chunkWidth = 33;
    [Tooltip("����߶ȣ�����ƬΪ��λ��")]
    [SerializeField] private int chunkHeight = 16;

    [Header("�������Ϊ")]
    [Tooltip("�Ƿ������������ƽ���ƶ�")]
    [SerializeField] private bool useSmoothTransition = false;
    [Tooltip("ƽ���ƶ��ٶ�")]
    [SerializeField] private float smoothTransitionSpeed = 8f;
    [Tooltip("�Ƿ���������ʱ�Զ�����������λ��")]
    [SerializeField] private bool useSnapToChunkOnStart = true;

    // �ڲ�״̬
    private int currentChunkX, currentChunkY;       // ��ǰ��������
    private Vector3 targetPosition;                 // Ŀ��λ��
    private bool isMoving = false;                  // �Ƿ������ƶ�

    private int lastScreenW = 0, lastScreenH = 0;   // ������Ļ�ߴ���и��٣��Ա�����Ҫʱ�Էֱ��ʵı仯������Ӧ
    #endregion
    private void Start()
    {
        if(follow == null)
        {
            Debug.LogError("[CameraController] follow δָ����");
            gameObject.SetActive(false);
            return;
        }
        if (groundTilemap == null)
        {
            Debug.LogError("[CameraController] groundTilemap δָ����");
            gameObject.SetActive(false);
            return;
        }
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>() ?? Camera.main;
            if(targetCamera == null )
            {
                Debug.LogError("[CameraController] δ�ҵ� Camera �������ָ�� targetCamera ����� Camera �����");
                gameObject.SetActive(false);
                return;
            }
        }

        // ���㵱ǰ������������������飨������������ĵ�Ԫ��
        Vector3 cameraWorld = transform.position;
        Vector3Int cameraCell = groundTilemap.WorldToCell(cameraWorld);
        currentChunkX = FloorDiv(cameraCell.x, Mathf.Max(1, chunkWidth));
        currentChunkY = FloorDiv(cameraCell.y, Mathf.Max(1, chunkHeight));

        // ȷ����ʼ�����λ�ã���׼������ģ��򱣳ֵ�ǰ�����λ��
        UpdateTargetPositionFromChunk(currentChunkX, currentChunkY);
        if (useSnapToChunkOnStart)
        {
            transform.position = targetPosition;
            isMoving = false;
        }

        // ��ʼ����Ļ����
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

    #region < ������� >
    /// <summary>
    /// ��������ܿ�
    /// </summary>
    private void CameraFollow()
    {
        if (isMoving)
        {
            if (useSmoothTransition)
            {
                // ����ָ����ֵʵ��ƽ������
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

        // �������������������ϵ�еľ���λ�ã���ǰ֡��
        Rect camRect = GetCameraWorldRect();

        // �����������������Ұ��Χ�ڣ��򲻲�ȡ�κ��ж�
        Vector3 playerWorld = follow.position;
        if (camRect.Contains(new Vector2(playerWorld.x, playerWorld.y))) return;

        // ����뿪������������� -> ����������ڵ����򣬲���������ƶ��������������λ��
        Vector3Int playerCell = groundTilemap.WorldToCell(playerWorld);
        int playerChunkX = FloorDiv(playerCell.x, Mathf.Max(1, chunkWidth));
        int playerChunkY = FloorDiv(playerCell.y, Mathf.Max(1, chunkHeight));

        // �����µ�Ŀ������
        currentChunkX = playerChunkX;
        currentChunkY = playerChunkY;
        UpdateTargetPositionFromChunk(currentChunkX, currentChunkY);

        // ��ʼ�ƶ�
        isMoving = true;
    }

    /// <summary>
    /// ���ݵ�ǰ�������λ�á������ߴ��Լ���Ļ�����������絥λ�����������ͼ�ľ�������
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
    /// ���������������X ���ꡢY ���꣩�����������ĵġ��������ꡱλ�á�
    /// ͨ��ʹ�������ǿ��ͼԪ��Ԫ������ʵ�֣�����ܹ��ܺõ���Ӧ����ƫ�������
    /// </summary>
    /// <param name="chunkX">����X������</param>
    /// <param name="chunkY">����Y������</param>
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
    /// ʹ�û��ڸ�����������ȡ����ʽ���г������㣬����ȷ���������
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
    #region < ���ӻ� >
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