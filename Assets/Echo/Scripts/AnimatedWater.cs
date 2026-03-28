// анимация воды
using UnityEngine;

[ExecuteInEditMode] // Позволяет видеть эффект в редакторе
public class AnimatedWater : MonoBehaviour
{
    [Tooltip("Материал с текстурой воды, к которому будет применяться анимация")]
    public Material waterMaterial;

    [Header("Параметры анимации")]
    [Tooltip("Скорость смещения текстуры по оси X (горизонтальное движение)")]
    public float speedX = 0.5f;

    [Tooltip("Скорость смещения текстуры по оси Y (вертикальное движение)")]
    public float speedY = 0.2f;

    [Tooltip("Направление смещения (1 или -1 для изменения направления)")]
    public Vector2 direction = new(1, 1);
    private Vector2 offset;

    void Update()
    {
        // Обновляем смещение текстуры
        offset.x += speedX * direction.x * Time.deltaTime;
        offset.y += speedY * direction.y * Time.deltaTime;

        // Применяем смещение к материалу
        if (waterMaterial != null)
        {
            waterMaterial.SetTextureOffset("_MainTex", offset);
        }
    }

    // Для отображения в редакторе (если нужно видеть анимацию без запуска игры)
    void OnValidate()
    {
        if (Application.isPlaying) return;

        if (waterMaterial != null)
        {
            waterMaterial.SetTextureOffset("_MainTex", offset);
        }
    }
}
