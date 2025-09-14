using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("调试设置")]
    [Tooltip("每一个角色行动之间的微小延迟（0为无间隔）")]
    [SerializeField, Range(0, 1)] private float perActorDelay = 0.05f;

    // 注册动物表
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

    /// <summary>
    /// 注册一个回合角色
    /// </summary>
    /// <param name="actor"></param>
    public void Register(ITurnActor actor)
    {
        if (actor == null) return;
        if (!actors.Contains(actor)) actors.Add(actor);
    }

    /// <summary>
    /// 注销一个回合角色
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

        // 拷贝一份快照以防在遍历时列表被修改
        var snapshot = new List<ITurnActor>(actors);

        foreach (var actor in snapshot)
        {
            if (actor == null) continue;
            // 每个 actor 的 TakeTurn 返回 IEnumerator
            yield return actor.TakeTurn();

            if (perActorDelay > 0f)
                yield return new WaitForSeconds(perActorDelay);
        }

        isProcessing = false;
    }
}
