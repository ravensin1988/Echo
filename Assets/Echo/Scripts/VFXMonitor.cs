using UnityEngine;

public class VFXMonitor : MonoBehaviour
{
    [SerializeField] private float checkInterval = 5f;
    [SerializeField] private bool autoCleanup = true;
    
    void Start()
    {
        InvokeRepeating(nameof(CheckVFXStatus), checkInterval, checkInterval);
    }
    
    void CheckVFXStatus()
    {
        if (VFXPool.Instance == null) return;
        
        VFXPool.Instance.LogPoolStatus();
        
        if (autoCleanup)
        {
            VFXPool.Instance.CleanupUnusedEffects();
        }
    }
    
    // Консольная команда для очистки всех эффектов воды
    [ContextMenu("Clear Water Effects")]
    public void ClearWaterEffects()
    {
        if (VFXPool.Instance != null)
        {
            VFXPool.Instance.ClearAllEffects("water");
        }
    }
}