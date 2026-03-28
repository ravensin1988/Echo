using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class OnDrawGizmos_Script : MonoBehaviour
{
    // Настройка формы gizmo
    [Header("Настройки Gizmo")]
    [Tooltip("Форма gizmo: Cube, Sphere, WireCube, WireSphere")]
    public GizmoShape gizmoShape = GizmoShape.Cube;
    
    // Настройка размера gizmo
    [Tooltip("Размер gizmo")]
    public Vector3 gizmoSize = Vector3.one;
    
    // Настройка цвета gizmo
    [Tooltip("Цвет gizmo (R, G, B, A)")]
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.5f);
    
    // Цвет при выделении
    [Tooltip("Цвет gizmo при выделении объекта")]
    public Color selectedColor = new Color(1f, 0.92f, 0.016f, 0.8f);
    
    // Прозрачность заполнения
    [Range(0f, 1f)]
    public float fillAlpha = 0.3f;
    
    // Показывать ли gizmo
    [Tooltip("Показывать ли gizmo в редакторе")]
    public bool showGizmo = true;
    
    // Включить клик для выбора объекта
    [Tooltip("При клике на gizmo выбирается объект")]
    public bool clickToSelect = true;

    // Храним ID контрола для обработки клика
    private int controlID;
    private bool isHovered;

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Получение позиции и размера
        Vector3 position = transform.position;
        Vector3 size = Vector3.Scale(transform.localScale, gizmoSize);

        // Определяем цвет - обычный или при выделении
        #if UNITY_EDITOR
        bool isSelected = Selection.Contains(gameObject);
        Gizmos.color = isSelected ? selectedColor : gizmoColor;
        #else
        Gizmos.color = gizmoColor;
        #endif

        // Рисование gizmo в зависимости от выбранной формы
        switch (gizmoShape)
        {
            case GizmoShape.Cube:
                Gizmos.DrawCube(position, size);
                break;
                
            case GizmoShape.Sphere:
                float radius = Mathf.Max(size.x, size.y, size.z) * 0.5f;
                Gizmos.DrawSphere(position, radius);
                break;
                
            case GizmoShape.WireCube:
                Gizmos.DrawWireCube(position, size);
                break;
                
            case GizmoShape.WireSphere:
                float wireRadius = Mathf.Max(size.x, size.y, size.z) * 0.5f;
                Gizmos.DrawWireSphere(position, wireRadius);
                break;
                
            case GizmoShape.Line:
                Gizmos.DrawLine(position, position + transform.forward * size.z);
                break;
        }

        // Обработка клика для выбора объекта
        #if UNITY_EDITOR
        if (clickToSelect)
        {
            HandleGizmoClick(position, size);
        }
        #endif
    }

    #if UNITY_EDITOR
    private void HandleGizmoClick(Vector3 position, Vector3 size)
    {
        // Получаем ID контрола
        controlID = GUIUtility.GetControlID(FocusType.Passive);
        
        // Определяем область gizmo для обработки событий мыши
        Bounds bounds = new Bounds(position, size);
        
        // Добавляем контрол для обработки мыши
        HandleUtility.AddDefaultControl(controlID);
        
        // Вычисляем расстояние от камеры до gizmo
        Camera sceneCamera = SceneView.lastActiveSceneView?.camera;
        float distance = 0f;
        
        if (sceneCamera != null)
        {
            distance = Vector3.Distance(sceneCamera.transform.position, position);
        }

        // Обрабатываем событие мыши
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            // Проверяем расстояние от мыши до gizmo
            Vector2 mousePosition = Event.current.mousePosition;
            Vector3 gizmoScreenPos = sceneCamera != null ? sceneCamera.WorldToScreenPoint(position) : Vector3.zero;
            
            float screenDistance = Vector2.Distance(mousePosition, new Vector2(gizmoScreenPos.x, gizmoScreenPos.y));
            float pickSize = Mathf.Max(size.x, size.y, size.z) * 50f / (distance + 1f);
            
            if (screenDistance < pickSize)
            {
                // Выбираем объект
                Selection.activeGameObject = gameObject;
                Event.current.Use();
            }
        }
        
        // Меняем цвет при наведении - используем nearestControl
        if (HandleUtility.nearestControl == controlID)
        {
            Gizmos.color = selectedColor;
            switch (gizmoShape)
            {
                case GizmoShape.Cube:
                    Gizmos.DrawCube(position, size);
                    break;
                case GizmoShape.Sphere:
                    float radius = Mathf.Max(size.x, size.y, size.z) * 0.5f;
                    Gizmos.DrawSphere(position, radius);
                    break;
                case GizmoShape.WireCube:
                    Gizmos.DrawWireCube(position, size);
                    break;
                case GizmoShape.WireSphere:
                    float wireRadius = Mathf.Max(size.x, size.y, size.z) * 0.5f;
                    Gizmos.DrawWireSphere(position, wireRadius);
                    break;
                case GizmoShape.Line:
                    Gizmos.DrawLine(position, position + transform.forward * size.z);
                    break;
            }
        }
    }
    #endif

    // Перечисление форм gizmo
    public enum GizmoShape
    {
        Cube,
        Sphere,
        WireCube,
        WireSphere,
        Line
    }
}
