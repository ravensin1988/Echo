using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;  // Камера будет найдена автоматически

    void Awake()
    {
        // Находим камеру с тэгом MainCamera
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        
        // Проверяем, найдена ли камера
        if (mainCamera == null)
        {
            Debug.LogError("Камера с тэгом MainCamera не найдена!");
        }
    }

    void Update()
    {
        if (mainCamera != null)
        {
            // Поворачиваем объект так, чтобы он смотрел на камеру
            transform.LookAt(mainCamera.transform.position);
            
            // Инвертируем поворот по Y, чтобы текст был читаемым
            transform.Rotate(0, 180, 0);
        }
    }
}
