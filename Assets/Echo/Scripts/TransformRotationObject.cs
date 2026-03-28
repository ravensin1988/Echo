using UnityEngine;

public class TransformRotationObject : MonoBehaviour
{
    public enum RotationMode
    {
        Continuous,     // Непрерывное вращение с заданной скоростью
        StepByStep,     // Вращение с заданным шагом
        TimeBased,      // Вращение за указанное время
        ManualControl   // Ручное управление из другого скрипта
    }

    [Header("Настройки вращения")]
    public RotationMode rotationMode = RotationMode.Continuous;
    
    [Header("Оси вращения")]
    public bool rotateX = false;
    public bool rotateY = true;
    public bool rotateZ = false;

    [Header("Настройки скорости")]
    [Tooltip("Скорость вращения (градусы в секунду)")]
    public float rotationSpeed = 90f;

    [Header("Настройки шагового режима")]
    [Tooltip("Шаг вращения в градусах")]
    public float rotationStep = 45f;
    [Tooltip("Интервал между шагами в секундах")]
    public float stepInterval = 0.5f;

    [Header("Настройки временного режима")]
    [Tooltip("Время для полного оборота в секундах")]
    public float fullRotationTime = 10f;
    [Tooltip("Запускать вращение при старте")]
    public bool autoStart = true;

    [Header("Дополнительные настройки")]
    [Tooltip("Вращение в локальных координатах")]
    public bool useLocalSpace = true;
    [Tooltip("Плавное вращение (только для Continuous и TimeBased)")]
    public bool smoothRotation = true;
    [Tooltip("Использовать Time.deltaTime")]
    public bool useDeltaTime = true;

    // Приватные переменные
    private Vector3 currentRotation = Vector3.zero;
    private float stepTimer = 0f;
    private float timeBasedAngle = 0f;
    private bool isRotating = true;

    void Start()
    {
        isRotating = autoStart;
        
        // Инициализация текущего вращения
        currentRotation = useLocalSpace ? transform.localEulerAngles : transform.eulerAngles;
        
        // Для временного режима вычисляем скорость на основе времени
        if (rotationMode == RotationMode.TimeBased && fullRotationTime > 0)
        {
            rotationSpeed = 360f / fullRotationTime;
        }
    }

    void Update()
    {
        if (!isRotating) return;

        switch (rotationMode)
        {
            case RotationMode.Continuous:
                ContinuousRotation();
                break;

            case RotationMode.StepByStep:
                StepByStepRotation();
                break;

            case RotationMode.TimeBased:
                TimeBasedRotation();
                break;

            case RotationMode.ManualControl:
                // В этом режиме вращение управляется извне через методы
                break;
        }
    }

    /// <summary>
    /// Непрерывное вращение
    /// </summary>
    private void ContinuousRotation()
    {
        float deltaAngle = rotationSpeed * (useDeltaTime ? Time.deltaTime : Time.unscaledDeltaTime);
        
        ApplyRotation(new Vector3(
            rotateX ? deltaAngle : 0f,
            rotateY ? deltaAngle : 0f,
            rotateZ ? deltaAngle : 0f
        ));
    }

    /// <summary>
    /// Шаговое вращение
    /// </summary>
    private void StepByStepRotation()
    {
        stepTimer += useDeltaTime ? Time.deltaTime : Time.unscaledDeltaTime;
        
        if (stepTimer >= stepInterval)
        {
            ApplyRotation(new Vector3(
                rotateX ? rotationStep : 0f,
                rotateY ? rotationStep : 0f,
                rotateZ ? rotationStep : 0f
            ));
            stepTimer = 0f;
        }
    }

    /// <summary>
    /// Вращение на основе времени
    /// </summary>
    private void TimeBasedRotation()
    {
        float deltaAngle = rotationSpeed * (useDeltaTime ? Time.deltaTime : Time.unscaledDeltaTime);
        timeBasedAngle += deltaAngle;
        
        // Нормализуем угол от 0 до 360
        timeBasedAngle %= 360f;
        
        Vector3 targetRotation = new Vector3(
            rotateX ? timeBasedAngle : currentRotation.x,
            rotateY ? timeBasedAngle : currentRotation.y,
            rotateZ ? timeBasedAngle : currentRotation.z
        );

        if (smoothRotation)
        {
            Vector3 newRotation = Vector3.Lerp(currentRotation, targetRotation, 0.1f);
            SetRotation(newRotation);
        }
        else
        {
            SetRotation(targetRotation);
        }
    }

    /// <summary>
    /// Применяет вращение к текущим углам
    /// </summary>
    private void ApplyRotation(Vector3 rotationDelta)
    {
        currentRotation += rotationDelta;
        
        // Нормализуем углы от 0 до 360
        currentRotation.x %= 360f;
        currentRotation.y %= 360f;
        currentRotation.z %= 360f;
        
        SetRotation(currentRotation);
    }

    /// <summary>
    /// Устанавливает вращение объекта
    /// </summary>
    private void SetRotation(Vector3 rotation)
    {
        if (useLocalSpace)
        {
            transform.localEulerAngles = rotation;
        }
        else
        {
            transform.eulerAngles = rotation;
        }
        
        currentRotation = rotation;
    }

    // === Публичные методы для управления вращением ===

    /// <summary>
    /// Начать вращение
    /// </summary>
    public void StartRotation()
    {
        isRotating = true;
    }

    /// <summary>
    /// Остановить вращение
    /// </summary>
    public void StopRotation()
    {
        isRotating = false;
    }

    /// <summary>
    /// Переключить вращение (вкл/выкл)
    /// </summary>
    public void ToggleRotation()
    {
        isRotating = !isRotating;
    }

    /// <summary>
    /// Установить скорость вращения
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    /// <summary>
    /// Установить время для полного оборота
    /// </summary>
    public void SetFullRotationTime(float timeInSeconds)
    {
        if (timeInSeconds > 0)
        {
            fullRotationTime = timeInSeconds;
            rotationSpeed = 360f / fullRotationTime;
        }
    }

    /// <summary>
    /// Вращать на заданный угол (для ManualControl режима)
    /// </summary>
    public void RotateByAngle(float xAngle, float yAngle, float zAngle)
    {
        if (rotationMode == RotationMode.ManualControl)
        {
            ApplyRotation(new Vector3(
                rotateX ? xAngle : 0f,
                rotateY ? yAngle : 0f,
                rotateZ ? zAngle : 0f
            ));
        }
    }

    /// <summary>
    /// Вращать на один шаг (для StepByStep режима)
    /// </summary>
    public void RotateSingleStep()
    {
        if (rotationMode == RotationMode.StepByStep)
        {
            ApplyRotation(new Vector3(
                rotateX ? rotationStep : 0f,
                rotateY ? rotationStep : 0f,
                rotateZ ? rotationStep : 0f
            ));
        }
    }

    /// <summary>
    /// Сбросить вращение к начальному состоянию
    /// </summary>
    public void ResetRotation()
    {
        currentRotation = Vector3.zero;
        timeBasedAngle = 0f;
        stepTimer = 0f;
        SetRotation(currentRotation);
    }

    /// <summary>
    /// Установить активные оси вращения
    /// </summary>
    public void SetRotationAxes(bool x, bool y, bool z)
    {
        rotateX = x;
        rotateY = y;
        rotateZ = z;
    }
}