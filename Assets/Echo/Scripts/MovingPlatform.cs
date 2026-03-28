using UnityEngine;
using System.Collections;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveDistance = 2f; // Дистанция одного шага
    public float moveSpeed = 2f; // Скорость движения
    public float minPauseTime = 1f; // Минимальная пауза между движениями
    public float maxPauseTime = 3f; // Максимальная пауза

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private float pauseTimer = 0f;
    private float currentPauseTime;

    void Start()
    {
        startPosition = transform.position;
        // Устанавливаем случайное время первой паузы
        currentPauseTime = Random.Range(minPauseTime, maxPauseTime);
    }

    void Update()
    {
        if (!isMoving)
        {
            // Обратный отсчет паузы
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= currentPauseTime)
            {
                // Пауза закончилась, начинаем новое движение
                StartNewMovement();
                pauseTimer = 0f;
                // Генерируем новое случайное время паузы для следующего цикла
                currentPauseTime = Random.Range(minPauseTime, maxPauseTime);
            }
        }
        else
        {
            // Плавно двигаемся к цели
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            // Если достигли цели, останавливаемся
            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                transform.position = targetPosition;
                isMoving = false;
                // Обновляем стартовую позицию для следующего движения
                startPosition = targetPosition;
            }
        }
    }

    void StartNewMovement()
    {
        // 1. Выбираем случайную ось (X, Y или Z)
        int axis = Random.Range(0, 3); // 0=X, 1=Y, 2=Z

        // 2. Выбираем направление (+ или -)
        int direction = Random.Range(0, 2) * 2 - 1; // -1 или 1

        // 3. Рассчитываем целевую позицию
        targetPosition = startPosition;
        switch (axis)
        {
            case 0: // X
                targetPosition.x += moveDistance * direction;
                break;
            case 1: // Y
                targetPosition.y += moveDistance * direction;
                break;
            case 2: // Z
                targetPosition.z += moveDistance * direction;
                break;
        }

        // 4. Начинаем движение
        isMoving = true;
    }

    // Очень важный метод! Позволяет двигать игрока вместе с платформой.
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Делаем игрока дочерним объектом платформы
            other.transform.SetParent(transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Открепляем игрока от платформы
            other.transform.SetParent(null);
        }
    }
}