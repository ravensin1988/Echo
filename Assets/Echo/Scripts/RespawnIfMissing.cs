using UnityEngine;
using System.Collections;

public class RespawnIfMissing : MonoBehaviour
{
    [Tooltip("Объект, который нужно отслеживать и воссоздавать при уничтожении")]
    public GameObject targetPrefab;

    [Tooltip("Интервал проверки в секундах")]
    public float checkInterval = 2f;

    private GameObject currentInstance;

    void Start()
    {
        if (targetPrefab == null)
        {
            Debug.LogError("Target Prefab не назначен в инспекторе на объекте " + gameObject.name, this);
            return;
        }

        // Сразу создаём первую копию
        SpawnInstance();
        StartCoroutine(CheckExistence());
    }

    IEnumerator CheckExistence()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (currentInstance == null || currentInstance.Equals(null))
            {
                SpawnInstance();
            }
        }
    }

    void SpawnInstance()
    {
        currentInstance = Instantiate(targetPrefab, transform.position, Quaternion.identity, transform);
    }
}