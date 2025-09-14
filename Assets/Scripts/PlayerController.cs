using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    #region < �ֶ� >
    [Header("��������")]
    [Tooltip("������Ƭ��ͼ")]
    [SerializeField]private Tilemap groundTilemap;
    [Tooltip("�ϰ���Ƭ��ͼ")]
    [SerializeField] private Tilemap obstacleTilemap;

    [Header("�ƶ�����")]
    [Tooltip("�����ƶ�")]
    [SerializeField] private bool canMove;
    [Tooltip("�ƶ����ʱ��")]
    [SerializeField, Range(0.01f, 1f)] private float moveIntervalTime;
    [Tooltip("�Ƿ�����ƽ���ƶ�")]
    [SerializeField] private bool useSmoothMove;
    [Tooltip("ƽ���ƶ�ʱ��")]
    [SerializeField]private float smoothMoveSpeed;

    [Header("�ϰ����")]
    [Tooltip("������ʱ�� Overlap �뾶")]
    [SerializeField] private float collisionCheckRadius = 0.15f;
    [Tooltip("�ɴݻ����� LayerMask")]
    [SerializeField] private LayerMask destructibleLayer;
    [Tooltip("���� LayerMask")]
    [SerializeField] private LayerMask animalLayer;


    // �ڲ�״̬
    private bool isMoving = false;

    private bool canUpMove;
    private bool canDownMove;
    private bool canLeftMove;
    private bool canRightMove;

    private float moveIntervalTimer;        // �ƶ������ʱ��

    private Vector3Int currentCell;         // ��ҵ�ǰ���ڵĸ���
    private Vector3Int targetCell;          // ���Ŀ�����
    private Vector3 targetWorldPosition;    // ���Ŀ�����������

    private List<KeyCode> keyStack = new List<KeyCode>();   // ���ȼ�ջ
    private Vector3Int inputDir = Vector3Int.zero;          // ���뷽��
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
            Debug.LogError("[PlayerController] groundTilemap �� obstacleTilemap δָ����");
            gameObject.SetActive(false);
            return;
        }

        if(groundTilemap == obstacleTilemap)
        {
            Debug.LogError("[PlayerController] groundTilemap �� obstacleTilemap �����쳣��");
            gameObject.SetActive(false);
            return;
        }

        // ��ʼ��������ڵĸ���
        currentCell = groundTilemap.WorldToCell(transform.position);
        targetCell = currentCell;
        targetWorldPosition = groundTilemap.GetCellCenterWorld(currentCell);
        transform.position = targetWorldPosition;

        // ��ʼ��ⷽ�������
        MoveCheck();
    }

    private void Update()
    {
        // ����������/̧���¼���ά�� keyStack�����ȼ���ջβ���ȣ�
        HandleKeyStackEvents();
        // ���㵱ǰ���ȷ���
        inputDir = GetDirFromKeyStack();
        // ���¼�ʱ��
        Timer();
        // ����ƶ����
        MoveCheck();
        // ����ƶ��ܿ�
        Move();
    }

    #region < ��ʱ�� >
    /// <summary>
    /// ��ʱ����ʵʱ���¼�ʱ��
    /// </summary>
    private void Timer()
    {
        if (moveIntervalTimer > 0) moveIntervalTimer -= Time.deltaTime;
    }
    #endregion

    #region < ������� >
    public void ClearKeyStack()
    {
        keyStack.Clear();
    }

    /// <summary>
    /// ������ջ�¼������� KeyDown / KeyUp ��ά��������ջ
    /// </summary>
    private void HandleKeyStackEvents()
    {
        foreach (var key in trackedKeys)
        {
            // KeyDown���� key �ŵ�ջβ�����Ƴ�ͬ key �ľ��
            if (Input.GetKeyDown(key)) 
            {
                for(int i = keyStack.Count - 1; i >= 0; i--)
                {
                    if (keyStack[i] == key) keyStack.RemoveAt(i);
                }
                keyStack.Add(key);
            }
            // KeyUp���Ƴ��� key�����̧�𣬵���ͬ����������������ڰ�ס���ᱣ������������ջ�У�
            if (Input.GetKeyUp(key))
            {
                for (int i = keyStack.Count - 1; i >= 0; i--)
                {
                    if (keyStack[i] == key) keyStack.RemoveAt(i);
                }
            }
        }
        // ��ջΪ�յ��м�����ס�������ڽű�����ǰ�Ͱ��ţ����ѵ�һ����⵽�İ�ס������ջ����֤�����ƶ��ܹ�����
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
    /// �� keyStack����β��ͷ��ȡ��һ����Ч����
    /// </summary>
    /// <returns>��Ч����</returns>
    private Vector3Int GetDirFromKeyStack()
    {
        for(int i = keyStack.Count -1; i >= 0; i--)
        {
            if (TryKeyToDir(keyStack[i], out Vector3Int dir)) return dir;
        }
        return Vector3Int.zero;
    }

    /// <summary>
    /// ������ӳ��Ϊ����
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

    #region < ����ƶ� >
    /// <summary>
    /// ����ƶ��ܿأ����й��������й��ƶ��ķ���
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
    /// �ƶ���⣺�ж�������������ĸ������ܷ�����ƶ�
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
    /// �жϸ� Cell �Ƿ�ռ��
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
    #region < ���ӻ� >
    private void OnDrawGizmos()
    {
        if (groundTilemap == null) return;

        // �����׼���ӣ����ڱ༭��δ�����У�ʹ�� transform.position��
        Vector3Int drawBaseCell;
        if (Application.isPlaying)
            drawBaseCell = currentCell;
        else
            drawBaseCell = groundTilemap.WorldToCell(transform.position);

        // �����͸����ɫ
        float alpha = 0.2f;
        Color okColor = new Color(0f, 1f, 0f, alpha);
        Color noColor = new Color(1f, 0f, 0f, alpha);
        float boxSize = 1f;
        Color lineColor = new Color(Color.white.r, Color.white.g, Color.white.b, alpha);

        // ���� Up
        Vector3 upCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.up);
        Gizmos.color = (Application.isPlaying ? (canUpMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(upCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(upCenter, Vector3.one * boxSize);

        // ���� Down
        Vector3 downCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.down);
        Gizmos.color = (Application.isPlaying ? (canDownMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(downCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(downCenter, Vector3.one * boxSize);

        // ���� Left
        Vector3 leftCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.left);
        Gizmos.color = (Application.isPlaying ? (canLeftMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(leftCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(leftCenter, Vector3.one * boxSize);

        // ���� Right
        Vector3 rightCenter = groundTilemap.GetCellCenterWorld(drawBaseCell + Vector3Int.right);
        Gizmos.color = (Application.isPlaying ? (canRightMove ? okColor : noColor) : noColor);
        Gizmos.DrawCube(rightCenter, Vector3.one * boxSize);
        //Gizmos.color = lineColor;
        //Gizmos.DrawWireCube(rightCenter, Vector3.one * boxSize);

        // �����������С���
        Vector3 center = groundTilemap.GetCellCenterWorld(drawBaseCell);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, alpha);
        Gizmos.DrawSphere(center, 0.12f);
        Gizmos.color = lineColor;
        Gizmos.DrawWireSphere(center, 0.12f);
    }
    #endregion
#endif
}
