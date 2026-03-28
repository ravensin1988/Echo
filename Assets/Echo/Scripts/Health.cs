using UnityEngine;
using System;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Параметры здоровья")]
    public float maxHealth = 100f;
    [SerializeField] 
    public float currentHealth;

    // Событие при изменении здоровья
    public event Action<float, float> OnHealthChanged;

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        // Вызываем событие изменения здоровья
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }

    // Публичные методы для получения значений здоровья
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}