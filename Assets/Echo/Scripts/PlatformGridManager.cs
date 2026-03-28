using UnityEngine;

public class PlatformGridManager : MonoBehaviour
{
    public GameObject platformPrefab; // Префаб платформы
    public int gridSize = 5; // Количество платформ по X и Z
    public float spacing = 2.5f; // Расстояние между центрами платформ

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                // Рассчитываем позицию платформы в сетке
                Vector3 spawnPosition = new ();
                // Создаем экземпляр платформы
                GameObject platform = Instantiate(platformPrefab, spawnPosition, Quaternion.identity, transform);
                // Даем платформе осмысленное имя
                platform.name = $"Platform_{x}_{z}";

                // (Опционально) Можно рандомизировать параметры для каждой платформы
                MovingPlatform mp = platform.GetComponent<MovingPlatform>();
                if (mp != null)
                {
                    mp.moveDistance = Random.Range(1.5f, 2.5f);
                    mp.moveSpeed = Random.Range(1f, 3f);
                }
            }
        }
    }
}