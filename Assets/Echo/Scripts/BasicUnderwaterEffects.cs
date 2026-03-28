/// делает затенение в зависимости от высоты положения настроенной в скрипте эффект "под водой"
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class BasicUnderwaterEffects : MonoBehaviour
{
    public float waterLevel = 0f; // Уровень воды в мировых координатах
    public Color underwaterFogColor = new(0.1f, 0.3f, 0.4f, 1f);
    public float underwaterFogDensity = 0.05f;
    
    private Camera mainCamera;
    private AudioSource audioSource;
    private bool isUnderwater = false;
    private bool wasUnderwater = false;
    
    void Start()
    {
        mainCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();
    }
    
    void Update()
    {
        // Простая проверка по высоте
        isUnderwater = mainCamera.transform.position.y < waterLevel;
        
        if (isUnderwater != wasUnderwater)
        {
            if (isUnderwater)
            {
                EnterWater();
            }
            else
            {
                ExitWater();
            }
            wasUnderwater = isUnderwater;
        }
        
        // Плавное изменение звука
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Lerp(audioSource.volume, isUnderwater ? 0.3f : 1f, Time.deltaTime * 2f);
        }
    }
    
    void EnterWater()
    {
        // Включаем туман
        RenderSettings.fog = true;
        RenderSettings.fogColor = underwaterFogColor;
        RenderSettings.fogDensity = underwaterFogDensity;
        
        // Проигрываем звук погружения
        if (audioSource != null)
        {
            audioSource.Play();
        }
        
        Debug.Log("Вошли в воду");
    }
    
    void ExitWater()
    {
        // Выключаем туман (или восстанавливаем оригинальный)
        RenderSettings.fog = false;
        
        Debug.Log("Вышли из воды");
    }
}