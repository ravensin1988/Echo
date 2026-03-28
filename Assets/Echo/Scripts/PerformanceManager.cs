using UnityEngine;
using System.Collections;

public class PerformanceManager : MonoBehaviour
{
    [Header("Оптимизация стрельбы")]
    [SerializeField] private int maxBulletsPerFrame = 5;
    [SerializeField] private int maxVFXPerFrame = 10;
    
    [Header("Очистка памяти")]
    [SerializeField] private float memoryCleanupInterval = 30f;
    [SerializeField] private bool enableAutoCleanup = true;
    
    private int bulletsThisFrame = 0;
    private int vfxThisFrame = 0;
    private int currentFrame;
    
    void Start()
    {
        if (enableAutoCleanup)
        {
            StartCoroutine(MemoryCleanupRoutine());
        }
    }
    
    void Update()
    {
        // Сбрасываем счетчики каждый кадр
        if (currentFrame != Time.frameCount)
        {
            currentFrame = Time.frameCount;
            bulletsThisFrame = 0;
            vfxThisFrame = 0;
        }
    }
    
    public bool CanSpawnBullet()
    {
        return bulletsThisFrame < maxBulletsPerFrame;
    }
    
    public bool CanSpawnVFX()
    {
        return vfxThisFrame < maxVFXPerFrame;
    }
    
    public void RegisterBulletSpawn()
    {
        bulletsThisFrame++;
    }
    
    public void RegisterVFXSpawn()
    {
        vfxThisFrame++;
    }
    
    IEnumerator MemoryCleanupRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(memoryCleanupInterval);
            
            CleanupMemory();
        }
    }
    
    void CleanupMemory()
    {
        // Очищаем только пулы VFX — без GC.Collect и Resources.UnloadUnusedAssets
        // (они вызывают заметные фризы и не нужны при наличии пулинга)
        if (VFXPool.Instance != null)
        {
            VFXPool.Instance.CleanupUnusedEffects();
        }
    }

    void OnApplicationQuit()
    {
        StopAllCoroutines();
    }
}