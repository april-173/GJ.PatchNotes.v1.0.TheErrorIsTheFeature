using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager（修正版）
/// - 并发启动 actor 的 TakeTurn，但以分批启动（maxStartsPerFrame / perActorDelay）避免卡帧
/// - RunActorAndSignalCompletion 使用手动 MoveNext() 循环来避免在 try/catch 中使用 yield
/// - 捕获 actor.TakeTurn() 在 MoveNext 过程中抛出的异常，防止单个 actor 中断整个流程
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("调试设置")]
    [Tooltip("每一个角色启动 coroutine 之间的微小延迟（0 为无间隔，仅影响启动，不影响等待完成）")]
    [SerializeField, Range(0f, 1f)] private float perActorDelay = 0.00f;

    [Tooltip("每帧最多启动多少个 actor 的 coroutine（避免一次性启动大量 coroutine 导致瞬时开销）")]
    [SerializeField, Range(1, 256)] private int maxStartsPerFrame = 32;

    [Tooltip("调试输出开关")]
    [SerializeField] private bool debugLog = false;

    private readonly List<ITurnActor> actors = new List<ITurnActor>();

    private bool isProcessing = false;
    public bool IsProcessing => isProcessing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TurnManager] 已存在另一个实例，当前实例将被摧毁。");
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

            // 启动 wrapper coroutine（并发执行）
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
                    yield return null; // 等待下一帧再启动更多 actor
                }
            }

            if (this == null || !this.gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.LogWarning("[TurnManager] 中止处理：TurnManager 不可用。");
                isProcessing = false;
                yield break;
            }
        }

        // 等待所有 actor 完成（每帧检查）
        while (remainingActors > 0)
        {
            yield return null;
        }

        if (debugLog) Debug.Log("[TurnManager] All actors finished.");

        isProcessing = false;
    }

    /// <summary>
    /// wrapper：安全地运行 actor 的 IEnumerator，并在结束后调用 onComplete。
    /// 注意：为了避免在 try/catch 中使用 yield，我们对 enumerator 使用 MoveNext() 的手动循环。
    /// </summary>
    private IEnumerator RunActorAndSignalCompletion(ITurnActor actor, System.Action onComplete)
    {
        if (actor == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        IEnumerator enumerator = null;
        // 获取 enumerator（可能在 TakeTurn() 抛异常）
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

        // 手动循环调用 MoveNext()，把 MoveNext() 放在 try/catch 中以捕获执行期间异常
        while (true)
        {
            bool hasNext = false;
            try
            {
                hasNext = enumerator.MoveNext();
            }
            catch (System.Exception ex)
            {
                // 捕获 actor 在执行过程中的异常，记录并中断该 actor 的迭代
                Debug.LogException(ex);
                break;
            }

            if (!hasNext) break;

            // 在 try/catch 之外 yield 当前项（可能是 null、WaitForSeconds、IEnumerator 等）
            yield return enumerator.Current;
        }

        // 回收完成回调（通知 TurnManager）
        onComplete?.Invoke();
    }
}

