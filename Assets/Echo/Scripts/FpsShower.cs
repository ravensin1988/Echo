using UnityEngine;
using System.Text;

[RequireComponent(typeof(Camera))]
public class FpsShower : MonoBehaviour
{
    [SerializeField] private bool show = true;
    [SerializeField] private bool showMs = true;
    [SerializeField] private bool showMinMax = false;
    
    [Header("Настройки отображения")]
    [SerializeField] private TextAnchor anchor = TextAnchor.UpperLeft;
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Color goodColor = new ();   // >60 FPS
    [SerializeField] private Color warnColor = new ();     // 30-60 FPS
    [SerializeField] private Color badColor = new ();    // <30 FPS

    private float fps = 60f; // Начальное значение, чтобы избежать деления на 0
    private float minFps = float.MaxValue;
    private float maxFps = 0f;
    private float accumulated;
    private int frames;
    private float lastUpdate;
    private GUIStyle style;
    private Rect position;
    private readonly StringBuilder _stringBuilder = new (96);
    private readonly GUIContent _labelContent = new();
    private string _cachedText = "60 FPS";

    void Start()
    {
        Application.targetFrameRate = -1;
        lastUpdate = Time.realtimeSinceStartup;
        // Стиль создаём в OnGUI при первом вызове — там GUI.skin гарантированно существует
    }

    void Update()
    {
        if (!show) return;
        
        accumulated += Time.unscaledDeltaTime;
        frames++;
        
        if (Time.realtimeSinceStartup - lastUpdate > 0.5f)
        {
            if (accumulated > 0f)
            {
                fps = frames / accumulated;
                minFps = Mathf.Min(minFps, fps);
                maxFps = Mathf.Max(maxFps, fps);
                RebuildCachedText();
            }
            frames = 0;
            accumulated = 0f;
            lastUpdate = Time.realtimeSinceStartup;
        }
    }

    void OnGUI()
    {
        if (!show || Event.current.type != EventType.Repaint) return;
        
        // Создаём стиль при первом вызове OnGUI (там GUI.skin точно существует)
        if (style == null)
        {
            CreateGUIStyle();
        }
        
        // Определяем позицию в углу
        float w = Screen.width, h = Screen.height;
        float x = anchor switch
        {
            TextAnchor.UpperLeft or TextAnchor.MiddleLeft or TextAnchor.LowerLeft => 10,
            TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => w * 0.5f - 75,
            _ => w - 160
        };
        float y = anchor switch
        {
            TextAnchor.UpperLeft or TextAnchor.UpperCenter or TextAnchor.UpperRight => 10,
            TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => h * 0.5f - 20,
            _ => h - 50
        };
        
        position = new Rect(x, y, 150, 40);
        
        // Выбираем цвет в зависимости от FPS (защита от 0)
        Color color = fps >= 60 ? goodColor : (fps >= 30 ? warnColor : badColor);
        style.normal.textColor = color;
        
        _labelContent.text = _cachedText;
        GUI.Label(position, _labelContent, style);
    }

    private void RebuildCachedText()
    {
        _stringBuilder.Clear();

        int roundedFps = Mathf.RoundToInt(fps);
        _stringBuilder.Append(roundedFps);
        _stringBuilder.Append(" FPS");

        if (showMs && fps > 0.1f)
        {
            int roundedMs = Mathf.RoundToInt(1000f / fps);
            _stringBuilder.Append(" (");
            _stringBuilder.Append(roundedMs);
            _stringBuilder.Append(" ms)");
        }

        if (showMinMax)
        {
            int roundedMin = Mathf.RoundToInt(minFps == float.MaxValue ? fps : minFps);
            int roundedMax = Mathf.RoundToInt(maxFps);

            _stringBuilder.Append('\n');
            _stringBuilder.Append("Min: ");
            _stringBuilder.Append(roundedMin);
            _stringBuilder.Append(" | Max: ");
            _stringBuilder.Append(roundedMax);
        }

        _cachedText = _stringBuilder.ToString();
    }

    void CreateGUIStyle()
    {
        // Безопасное создание стиля — не зависит от GUI.skin
        style = new GUIStyle
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };
        
        // Если доступен скин — используем его шрифт для лучшего вида
        if (GUI.skin != null && GUI.skin.label != null)
        {
            style.font = GUI.skin.label.font;
        }
    }

    /// <summary>
    /// Переключает видимость счётчика FPS.
    /// </summary>
    public void Toggle() => show = !show;

    /// <summary>
    /// Включает отображение счётчика FPS.
    /// </summary>
    public void Show() => show = true;

    /// <summary>
    /// Выключает отображение счётчика FPS.
    /// </summary>
    public void Hide() => show = false;

    /// <summary>
    /// Возвращает текущее усреднённое значение FPS.
    /// </summary>
    public float GetCurrentFps() => fps;

    // Горячая клавиша для включения/выключения (опционально)
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            show = !show;
        }
    }
}