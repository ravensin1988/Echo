using UnityEngine;
using System.Collections.Generic;

public class RespawnAllIfAllDestroyed : MonoBehaviour
{
    [Tooltip("Префаб объекта для спавна")]
    public GameObject targetPrefab;

    [Tooltip("Количество копий для спавна за раз")]
    public int spawnCount = 3;

    [Tooltip("Радиус разброса при спавне вокруг центра (чтобы объекты не наслаивались)")]
    public float spawnRadius = 1.0f;

    [Tooltip("Интервал проверки уничтожения (в секундах)")]
    public float checkInterval = 2f;

    private readonly List<GameObject> activeInstances = new ();
    private bool isWaitingForRespawn = false;

    void Start()
    {
        if (targetPrefab == null)
        {
            Debug.LogError("Target Prefab не назначен на объекте " + gameObject.name, this);
            return;
        }

        SpawnInstances();
        InvokeRepeating(nameof(CheckIfAllDestroyed), checkInterval, checkInterval);
    }

    void SpawnInstances()
    {
        activeInstances.Clear();

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPosition = transform.position;

            GameObject instance = Instantiate(targetPrefab, spawnPosition, Quaternion.identity, transform);
            activeInstances.Add(instance);
        }

        isWaitingForRespawn = false;
    }

    void CheckIfAllDestroyed()
    {
        if (isWaitingForRespawn) return;

        // Удаляем из списка все уничтоженные или null-объекты
        activeInstances.RemoveAll(obj => obj == null || obj.Equals(null));

        // Если все уничтожены — спавним заново
        if (activeInstances.Count == 0)
        {
            isWaitingForRespawn = true;
            SpawnInstances();
        }
    }

    // Опционально: для отладки в редакторе
    void OnDrawGizmosSelected()
    {
        if (spawnRadius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}