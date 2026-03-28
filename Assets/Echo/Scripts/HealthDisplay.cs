using UnityEngine;
using TMPro;

[RequireComponent(typeof(Billboard))]
public class HealthDisplay : MonoBehaviour
{
    [Header("Настройки")]
    public Health targetHealth;
    public TMP_Text healthText;
    public string format = "{0}/{1}";
    public Vector3 textOffset = Vector3.up; // Смещение текста над объектом

    private bool _isSubscribed;

    private void Start()
    {
        // Находим Health на родительском объекте
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
        }

        // Находим или создаем TMP_Text
        if (healthText == null)
        {
            healthText = GetComponent<TMP_Text>();
            if (healthText == null)
            {
                // Создаем TMP_Text если его нет
                healthText = gameObject.AddComponent<TMP_Text>();
                // Настройки по умолчанию для TMP_Text
                healthText.fontSize = 12;
                healthText.alignment = TextAlignmentOptions.Center; // Вместо TextAnchor
                healthText.text = "0/0"; // Инициализация
            }
        }

        // Позиционируем текст над объектом
        transform.localPosition = textOffset;

        if (targetHealth != null)
        {
            SubscribeToHealthEvents();
            UpdateHealthText(targetHealth.GetCurrentHealth(), targetHealth.GetMaxHealth());
        }
    }

    private void OnEnable()
    {
        SubscribeToHealthEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealthEvents();
    }

    private void SubscribeToHealthEvents()
    {
        if (_isSubscribed || targetHealth == null)
            return;

        targetHealth.OnHealthChanged += UpdateHealthText;
        _isSubscribed = true;
    }

    private void UnsubscribeFromHealthEvents()
    {
        if (!_isSubscribed || targetHealth == null)
            return;

        targetHealth.OnHealthChanged -= UpdateHealthText;
        _isSubscribed = false;
    }

    private void UpdateHealthText(float current, float max)
    {
        if (healthText == null)
            return;

        healthText.SetText(format, Mathf.Round(current), Mathf.Round(max));
    }
}
