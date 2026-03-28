using UnityEngine;

public class Teleport : MonoBehaviour
{
    // Точка выхода телепорта (перетащите сюда объект-выход)
    [SerializeField] private Transform targetTeleport;

    // Смещение позиции при телепортации (чтобы не попасть внутрь коллайдера)
    [SerializeField] private Vector3 exitOffset = new ();

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что вошёл объект с тегом "Player"
        if (other.CompareTag("Player"))
        {
            // Телепортируем игрока в точку выхода с заданным смещением
            other.transform.position = targetTeleport.position + exitOffset;

            // Дополнительно можно воспроизвести эффект (звук, частицы и т.п.)
            // Например: PlayTeleportEffect();
        }
    }

    // Пример метода для визуального эффекта телепортации
    private void PlayTeleportEffect()
    {
        // Здесь можно добавить:
        // - воспроизведение звука: GetComponent<AudioSource>().Play();
        // - создание частиц: Instantiate(particleEffect, transform.position, Quaternion.identity);
        // - мигание экрана и т.д.
    }

    // Визуализация зоны телепорта в редакторе (опционально)
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}
