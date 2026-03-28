using UnityEngine;

public class TriggerChecker : MonoBehaviour
{
    [SerializeField] private bool showDebugMessages = true;

    private void OnTriggerEnter(Collider other)
    {
        if (showDebugMessages)
        {
            Debug.Log($"Триггер сработал! Объект: {other.name} вошел в триггер {gameObject.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (showDebugMessages)
        {
            Debug.Log($"Выход из триггера! Объект: {other.name} покинул триггер {gameObject.name}");
        }
    }

    private void Start()
    {
        if (GetComponent<Collider>() == null || !GetComponent<Collider>().isTrigger)
        {
            Debug.LogWarning($"Объект {gameObject.name} не имеет триггерного коллайдера!", this);
        }
    }
}