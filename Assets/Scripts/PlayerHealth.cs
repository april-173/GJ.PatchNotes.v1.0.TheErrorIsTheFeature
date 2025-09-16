using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("����ֵ����")]
    [Tooltip("����������ֵ")]
    [SerializeField] private int maxHealth = 6;

    [Tooltip("��ǰ����ֵ")]
    [SerializeField] private int currentHealth;

    public Die die;

    public int CurrentHealth => currentHealth;

    public int MaxHealth => maxHealth;

    private void Awake()
    {
        // ��ʼ��ʱ�ѵ�ǰ��������Ϊ���ֵ
        currentHealth = maxHealth;
    }

    /// <summary>
    /// ��Ѫ
    /// </summary>
    /// <param name="amount">�۳�����ֵ</param>
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
    /// ��Ѫ
    /// </summary>
    /// <param name="amount">�ظ�����ֵ</param>
    public void Heal(int amount = 1)
    {
        if (amount <= 0) return;

        AudioManager.Instance.PlayAnimalSFX(2, 1f, 1f);

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    /// <summary>
    /// ��������߼�
    /// </summary>
    private void Die()
    {
        die.PlayerDie();
    }
}
