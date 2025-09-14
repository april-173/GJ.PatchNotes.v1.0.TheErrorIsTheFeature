using System.Collections;

/// <summary>
/// ITurnActor：回合角色契约。此接口的组件将被 TurnManager 调度。
/// </summary>
public interface ITurnActor
{
    IEnumerator TakeTurn();
}
