using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    private Vector3 originalPosition;
    private float shakeDuration;
    private float shakeMagnitude;
    private bool isShaking = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        if (isShaking)
        {
            if (shakeDuration > 0)
            {
                // Генерируем случайное смещение
                Vector3 randomOffset = Random.insideUnitSphere * shakeMagnitude;
                randomOffset.z = 0; // Только по X и Y (можно и Z если нужно)

                transform.localPosition = originalPosition + randomOffset;
                shakeDuration -= Time.deltaTime;
            }
            else
            {
                // Завершаем тряску
                transform.localPosition = originalPosition;
                isShaking = false;
            }
        }
    }

    /// <summary>
    /// Запускает тряску камеры
    /// </summary>
    /// <param name="magnitude">Амплитуда тряски</param>
    /// <param name="duration">Длительность в секундах</param>
    public void Shake(float magnitude, float duration)
    {
        shakeMagnitude = magnitude;
        shakeDuration = duration;
        isShaking = true;
    }
}