// Скрипт для эффектов погружения под воду
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class UnderwaterEffects : MonoBehaviour
{
    [Header("Визуальные эффекты")]
    public Color underwaterColor = new Color(0.1f, 0.3f, 0.4f, 0.8f);
    public float colorBlendSpeed = 2f;
    public float fogDensity = 0.05f;
    public float fogBlendSpeed = 2f;
    
    [Header("Аудио")]
    public AudioClip underwaterSound;
    public AudioClip surfaceSound;
    public float volumeChangeSpeed = 1f;
    
    [Header("Пост-обработка")]
    public Volume underwaterVolume;
    public float volumeBlendSpeed = 2f;
    
    [Header("Опциональные компоненты")]
    public GameObject bubbleParticlesPrefab; // Префаб системы частиц пузырьков
    
    private Camera mainCamera;
    private AudioSource audioSource;
    private Color originalFogColor;
    private float originalFogDensity;
    private bool isUnderwater = false;
    private float targetVolumeWeight = 0f;
    private GameObject currentBubbles;

    private GameObject cachedWaterObject;
    
    void Start()
    {

        cachedWaterObject = GameObject.FindGameObjectWithTag("Water");
        
        mainCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();
        
        // Если AudioSource не найден, создаем его
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D звук
            audioSource.playOnAwake = false;
        }
        
        // Сохраняем оригинальные настройки тумана
        if (RenderSettings.fog)
        {
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
        }
        
        // Автоматически ищем Volume если не назначен
        if (underwaterVolume == null)
            underwaterVolume = FindAnyObjectByType<Volume>(); // Исправлено здесь
        
        // Гарантируем, что туман включен
        RenderSettings.fog = true;
    }
    
    void Update()
    {
        // Проверка находится ли камера под водой
        CheckUnderwaterStatus();
        
        // Плавное изменение эффектов
        UpdateEffects();
    }
    
    void CheckUnderwaterStatus()
    {
        // Проверяем все объекты с тегом "Water"
        GameObject waterObject = GameObject.FindGameObjectWithTag("Water");
        
        if (cachedWaterObject != null) // Используем кэшированную ссылку
        {
            Collider waterCollider = cachedWaterObject.GetComponent<Collider>();
            
            if (waterCollider != null)
            {
                bool newUnderwater = waterCollider.bounds.Contains(mainCamera.transform.position);
                
                if (newUnderwater != isUnderwater)
                {
                    isUnderwater = newUnderwater;
                    OnUnderwaterStatusChanged();
                }
            }
        }
    }
    
    void OnUnderwaterStatusChanged()
    {
        if (isUnderwater)
        {
            // Эффекты при погружении
            PlaySoundWithFade(underwaterSound);
            targetVolumeWeight = 1f;
            
            // Запускаем частицы пузырьков
            SpawnBubbles();
        }
        else
        {
            // Эффекты при всплытии
            PlaySoundWithFade(surfaceSound);
            targetVolumeWeight = 0f;
            
            // Останавливаем частицы пузырьков
            StopBubbles();
        }
    }
    
    void UpdateEffects()
    {
        // Плавное изменение пост-обработки
        if (underwaterVolume != null)
        {
            underwaterVolume.weight = Mathf.Lerp(
                underwaterVolume.weight, 
                targetVolumeWeight, 
                Time.deltaTime * volumeBlendSpeed
            );
        }
        
        // Изменение тумана
        if (RenderSettings.fog)
        {
            float blendFactor = underwaterVolume != null ? underwaterVolume.weight : 0;
            
            RenderSettings.fogColor = Color.Lerp(
                originalFogColor,
                underwaterColor,
                blendFactor
            );
            
            RenderSettings.fogDensity = Mathf.Lerp(
                originalFogDensity,
                fogDensity,
                blendFactor
            );
        }
        
        // Изменение звука (делаем звук приглушенным под водой)
        if (audioSource != null)
        {
            float targetVolume = isUnderwater ? 0.5f : 1f;
            float targetPitch = isUnderwater ? 0.8f : 1f;
            
            audioSource.volume = Mathf.Lerp(
                audioSource.volume,
                targetVolume,
                Time.deltaTime * volumeChangeSpeed
            );
            
            audioSource.pitch = Mathf.Lerp(
                audioSource.pitch,
                targetPitch,
                Time.deltaTime * volumeChangeSpeed
            );
        }
    }
    
    void PlaySoundWithFade(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            // Останавливаем текущий звук и запускаем новый
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
    
    void SpawnBubbles()
    {
        if (bubbleParticlesPrefab != null && currentBubbles == null)
        {
            // Создаем частицы пузырьков у позиции камеры
            currentBubbles = Instantiate(
                bubbleParticlesPrefab, 
                mainCamera.transform.position, 
                Quaternion.identity
            );
            
            // Делаем пузырьки дочерним объектом камеры
            currentBubbles.transform.parent = mainCamera.transform;
            currentBubbles.transform.localPosition = Vector3.forward * 0.5f;
        }
    }
    
    void StopBubbles()
    {
        if (currentBubbles != null)
        {
            // Отключаем эмиттер частиц
            var particleSystem = currentBubbles.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var emission = particleSystem.emission;
                emission.enabled = false;
            }
            
            // Уничтожаем объект через 2 секунды
            Destroy(currentBubbles, 2f);
            currentBubbles = null;
        }
    }
    
    // Метод для ручного переключения состояния (для отладки)
    public void ToggleUnderwater()
    {
        isUnderwater = !isUnderwater;
        OnUnderwaterStatusChanged();
    }
    
    void OnDestroy()
    {
        // Восстанавливаем оригинальные настройки тумана
        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
        }
    }
}