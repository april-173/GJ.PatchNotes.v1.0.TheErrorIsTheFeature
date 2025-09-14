using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("��������")]
    [Tooltip("ÿһ����ɫ�ж�֮���΢С�ӳ٣�0Ϊ�޼����")]
    [SerializeField, Range(0, 1)] private float perActorDelay = 0.05f;

    // ע�ᶯ���
    private readonly List<ITurnActor> actors = new List<ITurnActor>();

    private bool isProcessing = false;
    public bool IsProcessing => isProcessing;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TurnManager] �Ѵ�����һ��ʵ������ǰʵ�������ݻ١�");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// ע��һ���غϽ�ɫ
    /// </summary>
    /// <param name="actor"></param>
    public void Register(ITurnActor actor)
    {
        if (actor == null) return;
        if (!actors.Contains(actor)) actors.Add(actor);
    }

    /// <summary>
    /// ע��һ���غϽ�ɫ
    /// </summary>
    /// <param name="actor"></param>
    public void Unregister(ITurnActor actor)
    {
        if (actor == null) return;
        actors.Remove(actor);
    }

    public void PlayerMoved()
    {
        if (isProcessing) return;
        StartCoroutine(ProcessTurnsCoroutine());
    }

    private IEnumerator ProcessTurnsCoroutine()
    {
        isProcessing = true;

        // ����һ�ݿ����Է��ڱ���ʱ�б��޸�
        var snapshot = new List<ITurnActor>(actors);

        foreach (var actor in snapshot)
        {
            if (actor == null) continue;
            // ÿ�� actor �� TakeTurn ���� IEnumerator
            yield return actor.TakeTurn();

            if (perActorDelay > 0f)
                yield return new WaitForSeconds(perActorDelay);
        }

        isProcessing = false;
    }
}
