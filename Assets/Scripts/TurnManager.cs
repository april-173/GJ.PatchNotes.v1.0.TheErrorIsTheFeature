using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager�������棩
/// - �������� actor �� TakeTurn�����Է���������maxStartsPerFrame / perActorDelay�����⿨֡
/// - RunActorAndSignalCompletion ʹ���ֶ� MoveNext() ѭ���������� try/catch ��ʹ�� yield
/// - ���� actor.TakeTurn() �� MoveNext �������׳����쳣����ֹ���� actor �ж���������
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("��������")]
    [Tooltip("ÿһ����ɫ���� coroutine ֮���΢С�ӳ٣�0 Ϊ�޼������Ӱ����������Ӱ��ȴ���ɣ�")]
    [SerializeField, Range(0f, 1f)] private float perActorDelay = 0.00f;

    [Tooltip("ÿ֡����������ٸ� actor �� coroutine������һ������������ coroutine ����˲ʱ������")]
    [SerializeField, Range(1, 256)] private int maxStartsPerFrame = 32;

    [Tooltip("�����������")]
    [SerializeField] private bool debugLog = false;

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

    public void Register(ITurnActor actor)
    {
        if (actor == null) return;
        if (!actors.Contains(actor)) actors.Add(actor);
    }

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

        var snapshot = new List<ITurnActor>(actors);

        if (debugLog) Debug.Log($"[TurnManager] Start processing {snapshot.Count} actors.");

        int remainingActors = 0;
        int startedThisFrame = 0;

        if (snapshot.Count == 0)
        {
            isProcessing = false;
            yield break;
        }

        foreach (var actor in snapshot)
        {
            if (actor == null) continue;

            remainingActors++;

            // ���� wrapper coroutine������ִ�У�
            StartCoroutine(RunActorAndSignalCompletion(actor, () => { remainingActors--; }));

            startedThisFrame++;

            if (perActorDelay > 0f)
            {
                yield return new WaitForSeconds(perActorDelay);
                startedThisFrame = 0;
            }
            else
            {
                if (startedThisFrame >= maxStartsPerFrame)
                {
                    startedThisFrame = 0;
                    yield return null; // �ȴ���һ֡���������� actor
                }
            }

            if (this == null || !this.gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.LogWarning("[TurnManager] ��ֹ����TurnManager �����á�");
                isProcessing = false;
                yield break;
            }
        }

        // �ȴ����� actor ��ɣ�ÿ֡��飩
        while (remainingActors > 0)
        {
            yield return null;
        }

        if (debugLog) Debug.Log("[TurnManager] All actors finished.");

        isProcessing = false;
    }

    /// <summary>
    /// wrapper����ȫ������ actor �� IEnumerator�����ڽ�������� onComplete��
    /// ע�⣺Ϊ�˱����� try/catch ��ʹ�� yield�����Ƕ� enumerator ʹ�� MoveNext() ���ֶ�ѭ����
    /// </summary>
    private IEnumerator RunActorAndSignalCompletion(ITurnActor actor, System.Action onComplete)
    {
        if (actor == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        IEnumerator enumerator = null;
        // ��ȡ enumerator�������� TakeTurn() ���쳣��
        try
        {
            enumerator = actor.TakeTurn();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            onComplete?.Invoke();
            yield break;
        }

        if (enumerator == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        // �ֶ�ѭ������ MoveNext()���� MoveNext() ���� try/catch ���Բ���ִ���ڼ��쳣
        while (true)
        {
            bool hasNext = false;
            try
            {
                hasNext = enumerator.MoveNext();
            }
            catch (System.Exception ex)
            {
                // ���� actor ��ִ�й����е��쳣����¼���жϸ� actor �ĵ���
                Debug.LogException(ex);
                break;
            }

            if (!hasNext) break;

            // �� try/catch ֮�� yield ��ǰ������� null��WaitForSeconds��IEnumerator �ȣ�
            yield return enumerator.Current;
        }

        // ������ɻص���֪ͨ TurnManager��
        onComplete?.Invoke();
    }
}

