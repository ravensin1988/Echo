using UnityEngine;
using TMPro;

public class UITextFollow : MonoBehaviour
{
    public Camera mainCamera;          // Основная камера
    public Canvas canvas;              // Ваш Canvas
    public TextMeshProUGUI textUI;    // Элемент Text (TMP)

    void Update()
    {
        // Преобразуем позицию объекта в экранные координаты
        Vector2 screenPos = mainCamera.WorldToScreenPoint(transform.position);
        
        // Устанавливаем позицию текста на Canvas
        textUI.rectTransform.position = screenPos + new Vector2(0, 50);  // +50 пикселей вверх
    }
}
