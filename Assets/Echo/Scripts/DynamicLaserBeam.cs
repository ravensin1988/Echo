using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways] // Чтобы работало в редакторе
public class DynamicLaserBeam : MonoBehaviour
{
    [Header("Точки луча")]
    public Transform[] points;

    [Header("Настройки обновления")]
    [Tooltip("Обновлять луч в режиме редактора (без запуска игры)")]
    public bool updateInEditor = true;
    [Tooltip("Обновлять каждую точку в LateUpdate для плавности")]
    public bool useLateUpdate = true;
    
    private LineRenderer lineRenderer;
    private bool isInitialized = false;
    
    void Start()
    {
        Initialize();
    }
    
    void Initialize()
    {
        lineRenderer = GetComponent<LineRenderer>();
        
        // Не трогаем настройки Line Renderer, они уже заданы в инспекторе
        // Просто убедимся, что позиций достаточно
        if (points != null && points.Length > 0)
        {
            lineRenderer.positionCount = points.Length;
            UpdateLinePositions();
        }
        
        isInitialized = true;
    }
    
    void Update()
    {
        if (!useLateUpdate)
        {
            UpdateLaser();
        }
    }
    
    void LateUpdate()
    {
        if (useLateUpdate)
        {
            UpdateLaser();
        }
    }

    void UpdateLaser()
    {
        if (!isInitialized || lineRenderer == null) return;

        int validPoints = 0;

        // Считаем валидные точки
        if (points != null)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {
                    validPoints++;
                }
            }
        }

        // Если количество валидных точек изменилось
        if (lineRenderer.positionCount != validPoints)
        {
            lineRenderer.positionCount = validPoints;
        }

        // Обновляем позиции
        if (validPoints > 0)
        {
            int index = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {
                    lineRenderer.SetPosition(index, points[i].position);
                    index++;
                }
            }
        }
    }

    void UpdateLinePositions()
    {
        if (lineRenderer == null || points == null) return;
        
        int index = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null)
            {
                if (index < lineRenderer.positionCount)
                {
                    lineRenderer.SetPosition(index, points[i].position);
                }
                index++;
            }
        }
        
        // Обрезаем лишние позиции
        if (index < lineRenderer.positionCount)
        {
            lineRenderer.positionCount = index;
        }
    }
    
    // Методы для работы с точками в runtime
    public void AddPoint(Transform newPoint)
    {
        if (newPoint == null) return;
        
        // Создаем новый массив
        Transform[] newPoints;
        if (points == null)
        {
            newPoints = new Transform[1];
            newPoints[0] = newPoint;
        }
        else
        {
            newPoints = new Transform[points.Length + 1];
            points.CopyTo(newPoints, 0);
            newPoints[points.Length] = newPoint;
        }
        
        points = newPoints;
        
        // Обновляем LineRenderer
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = points.Length;
            UpdateLinePositions();
        }
    }
    
    public void RemovePoint(int index)
    {
        if (points == null || index < 0 || index >= points.Length) return;
        
        Transform[] newPoints = new Transform[points.Length - 1];
        for (int i = 0, j = 0; i < points.Length; i++)
        {
            if (i != index)
            {
                newPoints[j++] = points[i];
            }
        }
        
        points = newPoints;
        
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = points.Length;
            UpdateLinePositions();
        }
    }
    
    public void ClearPoints()
    {
        points = new Transform[0];
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }
    
    // Визуализация в редакторе
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Рисуем только если нет LineRenderer или в режиме редактора
        if (!Application.isPlaying && updateInEditor)
        {
            DrawEditorPreview();
        }
    }
    
    void DrawEditorPreview()
    {
        if (points == null || points.Length < 2) return;
        
        // Рисуем точки
        Gizmos.color = Color.green;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null)
            {
                Gizmos.DrawSphere(points[i].position, 0.1f);
            }
        }
        
        // Рисуем линии между точками
        Gizmos.color = Color.red;
        for (int i = 0; i < points.Length - 1; i++)
        {
            if (points[i] != null && points[i + 1] != null)
            {
                Gizmos.DrawLine(points[i].position, points[i + 1].position);
                
                // Рисуем стрелочки в середине линии
                Vector3 midPoint = Vector3.Lerp(points[i].position, points[i + 1].position, 0.5f);
                Vector3 direction = (points[i + 1].position - points[i].position).normalized;
                if (direction.magnitude > 0.01f)
                {
                    DrawArrow(midPoint, direction, 0.2f);
                }
            }
        }
    }

    void DrawArrow(Vector3 position, Vector3 direction, float size)
    {
        Gizmos.DrawRay(position, direction * size);

        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 30, 0) * Vector3.back;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -30, 0) * Vector3.back;

        // Reordered operands for better performance
        Gizmos.DrawRay(position + direction * size, right * (0.3f * size));
        Gizmos.DrawRay(position + direction * size, left * (0.3f * size));
    }

    void OnValidate()
    {
        // Автоматически обновляем в редакторе
        if (!Application.isPlaying && updateInEditor)
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
            
            if (lineRenderer != null && points != null)
            {
                UpdateLinePositions();
            }
        }
    }

    void Reset()
    {
        // При добавлении компонента автоматически находим LineRenderer
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();

            // Используем инициализатор объектов
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.material = new Material(Shader.Find("Unlit/Color"))
            {
                color = Color.red
            };
            lineRenderer.useWorldSpace = true;
        }
    }
#endif
}