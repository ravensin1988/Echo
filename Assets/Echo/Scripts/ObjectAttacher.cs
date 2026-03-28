using UnityEngine;

/// <summary>
/// Скрипт для прилипания объекта (А) к другому объекту (Б).
/// При старте объект со скриптом движется к целевому объекту и прилипает к нему.
/// </summary>
public class ObjectAttacher : MonoBehaviour
{
    [Header("Настройки прилипания")]
    [Tooltip("Целевой объект (Б), к которому нужно прилипнуть")]
    [SerializeField] private Transform targetObject;
    
    [Tooltip("Скорость движения к цели")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Tooltip("Расстояние, на котором объект считается прилипшим")]
    [SerializeField] private float attachDistance = 0.5f;
    
    [Tooltip("Задержка перед началом движения (сек)")]
    [SerializeField] private float startDelay = 0f;
    
    [Tooltip("Нужно ли сохранять смещение относительно цели")]
    [SerializeField] private bool keepOffset = true;
    
    [Tooltip("Смещение относительно центра цели")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    private bool isAttached = false;
    private Vector3 attachOffset;

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogWarning($"Объект {gameObject.name}: Целевой объект не назначен!");
            return;
        }

        // Запускаем движение к цели с задержкой
        if (startDelay > 0)
        {
            Invoke(nameof(MoveToTarget), startDelay);
        }
        else
        {
            MoveToTarget();
        }
    }

    private void MoveToTarget()
    {
        if (targetObject == null) return;

        // Вычисляем начальное смещение если нужно сохранять offset
        if (keepOffset && offset == Vector3.zero)
        {
            attachOffset = transform.position - targetObject.position;
        }
        else
        {
            attachOffset = offset;
        }
    }

    private void Update()
    {
        if (targetObject == null) return;

        if (!isAttached)
        {
            // Двигаемся к цели
            Vector3 targetPosition = targetObject.position + attachOffset;
            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance > attachDistance)
            {
                // Плавное движение к цели
                transform.position = Vector3.MoveTowards(
                    transform.position, 
                    targetPosition, 
                    moveSpeed * Time.deltaTime
                );
                
                // Поворачиваемся к цели (опционально)
                Vector3 direction = targetPosition - transform.position;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                }
            }
            else
            {
                // Прилипаем к объекту
                isAttached = true;
                OnAttached();
            }
        }
    }

    private void LateUpdate()
    {
        // Если прилипли, следуем за объектом
        if (isAttached && targetObject != null)
        {
            transform.position = targetObject.position + attachOffset;
        }
    }

    private void OnAttached()
    {
        Debug.Log($"Объект {gameObject.name} прилип к {targetObject.name}");
    }

    /// <summary>
    /// Установить целевой объект программно
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetObject = target;
        isAttached = false;
        if (target != null)
        {
            MoveToTarget();
        }
    }

    /// <summary>
    /// Отцепиться от объекта
    /// </summary>
    public void Detach()
    {
        isAttached = false;
        Debug.Log($"Объект {gameObject.name} отцепился");
    }
}