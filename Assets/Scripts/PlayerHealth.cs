using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("生命值设置")]
    [Tooltip("玩家最大生命值")]
    [SerializeField] private int maxHealth = 6;

    [Tooltip("当前生命值")]
    [SerializeField] private int currentHealth;

    public Die die;

    public int CurrentHealth => currentHealth;

    public int MaxHealth => maxHealth;

    private void Awake()
    {
        // 初始化时把当前生命设置为最大值
        currentHealth = maxHealth;
    }

    /// <summary>
    /// 扣血
    /// </summary>
    /// <param name="amount">扣除的数值</param>
    public void TakeDamage(int amount = 1)
    {
        if (amount <= 0) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 加血
    /// </summary>
    /// <param name="amount">回复的数值</param>
    public void Heal(int amount = 1)
    {
        if (amount <= 0) return;

        AudioManager.Instance.PlayAnimalSFX(2, 1f, 1f);

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    /// <summary>
    /// 玩家死亡逻辑
    /// </summary>
    private void Die()
    {
        die.PlayerDie();
    }
}
