using UnityEngine;
using System.Text;

public class PerformanceMonitor : MonoBehaviour
{
    [Header("Основные настройки")]
    [SerializeField] private int targetFPS = 60;
    [SerializeField] private float checkInterval = 3f;
    [SerializeField] private bool autoAdjustQuality = true;
    
    [Header("Настройки снижения качества")]
    [SerializeField] private int lowQualityLevel = 1;
    [SerializeField] private int highQualityLevel = 4; // Индекс максимального качества
    [SerializeField] private float lowFPSThreshold = 0.7f; // 70% от targetFPS
    
    [Header("Отладка")]
    [SerializeField] private bool showDebugInfo = false;
    
    private float fpsAccumulator = 0f;
    private int fpsFrames = 0;
    private float lastCheckTime = 0f;
    private float currentAvgFPS = 60f;
    private bool isLowQualityMode = false;
    private readonly StringBuilder _statusBuilder = new(64);
    private readonly GUIContent _statusContent = new();
    private string _cachedStatusText = "FPS: 60.0\nQuality: HIGH\nMode: Auto";
    
    void Start()
    {
        // Получаем максимальный уровень качества
        highQualityLevel = QualitySettings.names.Length - 1;
        
        if (showDebugInfo)
        {
            Debug.Log($"Performance Monitor started. Quality levels: {QualitySettings.names.Length}");
            Debug.Log($"Current quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        }
    }
    
    void Update()
    {
        // Сбор данных FPS
        fpsAccumulator += Time.unscaledDeltaTime;
        fpsFrames++;
        
        // Проверка каждые checkInterval секунд
        if (Time.unscaledTime - lastCheckTime > checkInterval)
        {
            CalculateAndAdjustPerformance();
        }
    }
    
    void CalculateAndAdjustPerformance()
    {
        if (fpsAccumulator > 0)
        {
            currentAvgFPS = fpsFrames / fpsAccumulator;
            RebuildStatusText();
            
            if (showDebugInfo)
            {
                Debug.Log($"Avg FPS: {currentAvgFPS:F1}, Target: {targetFPS}");
            }
            
            if (autoAdjustQuality)
            {
                AdjustQualityBasedOnFPS();
            }
        }
        
        // Сброс счетчиков
        fpsAccumulator = 0f;
        fpsFrames = 0;
        lastCheckTime = Time.unscaledTime;
    }
    
    void AdjustQualityBasedOnFPS()
    {
        float thresholdFPS = targetFPS * lowFPSThreshold;
        
        if (currentAvgFPS < thresholdFPS && !isLowQualityMode)
        {
            // Переход в режим низкого качества
            QualitySettings.SetQualityLevel(lowQualityLevel, true);
            isLowQualityMode = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"Switching to LOW quality. FPS: {currentAvgFPS:F1}");
            }
            
            // Дополнительные оптимизации
            OnEnterLowQualityMode();
            RebuildStatusText();
        }
        else if (currentAvgFPS > targetFPS * 0.9f && isLowQualityMode)
        {
            // Возврат к высокому качеству
            QualitySettings.SetQualityLevel(highQualityLevel, true);
            isLowQualityMode = false;
            
            if (showDebugInfo)
            {
                Debug.Log($"Switching to HIGH quality. FPS: {currentAvgFPS:F1}");
            }
            
            OnExitLowQualityMode();
            RebuildStatusText();
        }
    }
    
    // Кеш систем частиц — заполняется один раз при старте
    private ParticleSystem[] cachedParticleSystems;

    void OnEnterLowQualityMode()
    {
        Application.targetFrameRate = targetFPS;

        if (cachedParticleSystems == null)
            cachedParticleSystems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);

        foreach (var ps in cachedParticleSystems)
        {
            if (ps == null) continue;
            var emission = ps.emission;
            if (!ps.gameObject.TryGetComponent<ParticleSystemCache>(out var cache))
            {
                cache = ps.gameObject.AddComponent<ParticleSystemCache>();
                cache.originalRateMultiplier = emission.rateOverTimeMultiplier;
            }
            emission.rateOverTimeMultiplier = cache.originalRateMultiplier * 0.5f;
        }
    }

    void OnExitLowQualityMode()
    {
        Application.targetFrameRate = -1;

        if (cachedParticleSystems == null) return;

        foreach (var ps in cachedParticleSystems)
        {
            if (ps == null) continue;
            if (ps.TryGetComponent<ParticleSystemCache>(out var cache))
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = cache.originalRateMultiplier;
                Destroy(cache);
            }
        }
    }
    
    // Публичные методы для внешнего управления
    /// <summary>
    /// Принудительно переводит систему в режим низкого качества.
    /// </summary>
    public void ForceLowQualityMode()
    {
        QualitySettings.SetQualityLevel(lowQualityLevel, true);
        isLowQualityMode = true;
        OnEnterLowQualityMode();
        RebuildStatusText();
    }

    /// <summary>
    /// Принудительно переводит систему в режим высокого качества.
    /// </summary>
    public void ForceHighQualityMode()
    {
        QualitySettings.SetQualityLevel(highQualityLevel, true);
        isLowQualityMode = false;
        OnExitLowQualityMode();
        RebuildStatusText();
    }

    /// <summary>
    /// Возвращает текущее усреднённое значение FPS.
    /// </summary>
    public float GetCurrentFPS()
    {
        return currentAvgFPS;
    }

    /// <summary>
    /// Возвращает <see langword="true"/>, если включён режим низкого качества.
    /// </summary>
    public bool IsLowQualityModeActive()
    {
        return isLowQualityMode;
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUI.color = currentAvgFPS > targetFPS * 0.8f ? Color.green : 
                   currentAvgFPS > targetFPS * 0.6f ? Color.yellow : Color.red;
        
        _statusContent.text = _cachedStatusText;
        GUI.Box(new Rect(10, 10, 200, 80), _statusContent);
    }

    private void RebuildStatusText()
    {
        _statusBuilder.Clear();
        _statusBuilder.Append("FPS: ");
        _statusBuilder.Append(currentAvgFPS.ToString("F1"));
        _statusBuilder.Append('\n');
        _statusBuilder.Append("Quality: ");
        _statusBuilder.Append(isLowQualityMode ? "LOW" : "HIGH");
        _statusBuilder.Append('\n');
        _statusBuilder.Append("Mode: ");
        _statusBuilder.Append(autoAdjustQuality ? "Auto" : "Manual");

        _cachedStatusText = _statusBuilder.ToString();
    }
}