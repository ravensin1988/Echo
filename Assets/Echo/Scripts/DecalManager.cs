// File: DecalManager.cs
using UnityEngine;
using System.Collections;

public class DecalManager : MonoBehaviour
{
    [Header("Автоматическая очистка")]
    [SerializeField] private bool autoCleanup = true;
    [SerializeField] private float cleanupInterval = 60f;
    //[SerializeField] private int maxTotalDecals = 200;
    
    void Start()
    {
        if (autoCleanup)
        {
            StartCoroutine(AutoCleanupRoutine());
        }
    }
    
    IEnumerator AutoCleanupRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(cleanupInterval);
            
            if (BulletDecal.Instance != null)
            {
                // Здесь можно добавить логику для ограничения количества декалей
                // Например, удалять самые старые если их слишком много
                BulletDecal.Instance.ClearAllDecals();
            }
        }
    }
    
    [ContextMenu("Очистить все декали")]
    public void ClearAllDecals()
    {
        if (BulletDecal.Instance != null)
        {
            BulletDecal.Instance.ClearAllDecals();
            Debug.Log("Все декали очищены");
        }
    }
    
    [ContextMenu("Информация о декалях")]
    public void PrintDecalInfo()
    {
        // Этот метод можно использовать для отладки
        Debug.Log($"DecalManager активен. AutoCleanup: {autoCleanup}");
    }
}