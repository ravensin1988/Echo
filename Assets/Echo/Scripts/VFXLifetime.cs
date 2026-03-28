using UnityEngine;
using System;

[RequireComponent(typeof(ParticleSystem))]
public class VFXLifetime : MonoBehaviour
{
    public event Action<VFXLifetime> OnCompleted;
    
    [Tooltip("Автоматически деактивировать объект после завершения")]
    public bool autoDeactivate = true;
    
    private ParticleSystem ps;
    private float totalDuration;
    private float elapsedTime;
    private bool isPlaying;
    
    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("VFXLifetime requires a ParticleSystem!");
            enabled = false;
            return;
        }

        CalculateDuration();
    }
    
    void CalculateDuration()
    {
        var main = ps.main;
        float maxLifetime = 1f;
        
        if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
        {
            maxLifetime = main.startLifetime.constant;
        }
        else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
        {
            maxLifetime = Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
        }
        else if (main.startLifetime.mode == ParticleSystemCurveMode.Curve || 
                 main.startLifetime.mode == ParticleSystemCurveMode.TwoCurves)
        {
            maxLifetime = 5f; // Консервативная оценка для кривых
        }

        totalDuration = main.duration + maxLifetime;
    }

    void OnEnable()
    {
        elapsedTime = 0f;
        isPlaying = true;
        
        if (ps != null && !ps.isPlaying)
        {
            ps.Play();
        }
    }

    void OnDisable()
    {
        isPlaying = false;
        elapsedTime = 0f;
    }

    void Update()
    {
        if (!isPlaying) return;
        
        elapsedTime += Time.deltaTime;
        
        // Проверяем, завершился ли эффект
        if (elapsedTime >= totalDuration)
        {
            // Дополнительная проверка для ParticleSystem
            if (ps != null && !ps.IsAlive(true))
            {
                CompleteEffect();
            }
            else if (elapsedTime >= totalDuration * 1.5f) // Fail-safe
            {
                CompleteEffect();
            }
        }
    }
    
    void CompleteEffect()
    {
        if (!isPlaying) return;
        
        isPlaying = false;
        
        // Вызываем событие
        OnCompleted?.Invoke(this);
        
        if (autoDeactivate && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Принудительно завершает эффект и уведомляет подписчиков.
    /// </summary>
    public void ForceComplete()
    {
        CompleteEffect();
    }
    
    /// <summary>
    /// Проверяет, продолжает ли эффект считаться активным.
    /// </summary>
    /// <returns><c>true</c>, если частицы ещё живы или не истекло расчётное время жизни.</returns>
    public bool IsStillAlive()
    {
        if (ps == null) return false;
        return ps.IsAlive(true) || elapsedTime < totalDuration;
    }
}