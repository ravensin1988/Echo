// File: BulletDecal.cs
using UnityEngine;
using System.Collections.Generic;

public class BulletDecal : MonoBehaviour
{
    [Header("Настройки декалей")]
    [SerializeField] private Material defaultDecalMaterial;
    [SerializeField] private Material woodDecalMaterial;
    [SerializeField] private Material concreteDecalMaterial;
    [SerializeField] private Material metalDecalMaterial;
    [SerializeField] private Material waterDecalMaterial;
    [SerializeField] private Material dirtDecalMaterial;
    [SerializeField] private Material glassDecalMaterial;

    [Header("Размер и позиционирование")]
    [SerializeField] private float decalSizeMin = 0.05f;
    [SerializeField] private float decalSizeMax = 0.1f;
    [SerializeField] private bool randomRotation = true;
    [Tooltip("Локальная ось префаба, которая смотрит «лицом» наружу.\n" +
             "Для стандартного Unity Quad = (0,0,-1).\n" +
             "Для квада в плоскости XZ с нормалью по +Y = (0,1,0).")]
    [SerializeField] private Vector3 decalFaceAxis = new Vector3(0f, 0f, -1f);

    [Header("Время жизни")]
    [SerializeField] private float fadeDuration = 30f;
    [SerializeField] private bool fadeEnabled = true;

    [Header("Ограничения")]
    [SerializeField] private int maxDecalsTotal = 100;
    [SerializeField] private float minDistanceBetweenDecals = 0.05f;

    [Header("Пул объектов")]
    [SerializeField] private GameObject decalPrefab;
    [SerializeField] private int initialPoolSize = 50;

    // IDE0044 + IDE0090: readonly + target-typed new
    private readonly Queue<GameObject> decalPool = new();
    private readonly List<GameObject> activeDecals = new();
    private readonly List<GameObject> clearBuffer = new(128);

    private readonly Dictionary<GameObject, Renderer> rendererCache = new();
    private readonly Dictionary<GameObject, Color> decalBaseColor = new();
    private readonly Dictionary<GameObject, FadeState> activeFades = new();
    private readonly List<GameObject> fadeKeysBuffer = new(128);

    private MaterialPropertyBlock _materialPropertyBlock;
    private Vector3 _normalizedFaceAxis;
    private int _decalInstanceCounter;

    private struct FadeState
    {
        public float Elapsed;
        public float Duration;
        public Color InitialColor;
    }

    // IDE0017: Упрощенная инициализация коллекции с инициализатором
    private readonly Dictionary<SurfaceMaterial, Material> materialLookup = new()
    {
        [SurfaceMaterial.Default] = null,
        [SurfaceMaterial.Wood] = null,
        [SurfaceMaterial.Concrete] = null,
        [SurfaceMaterial.Metal] = null,
        [SurfaceMaterial.Water] = null,
        [SurfaceMaterial.Dirt] = null,
        [SurfaceMaterial.Glass] = null
    };

    public static BulletDecal Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _materialPropertyBlock = new MaterialPropertyBlock();
            _normalizedFaceAxis = decalFaceAxis.sqrMagnitude > 0f ? decalFaceAxis.normalized : -Vector3.forward;
            InitializeMaterialLookup();
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (!fadeEnabled || activeFades.Count == 0)
            return;

        float deltaTime = Time.deltaTime;
        clearBuffer.Clear();
        fadeKeysBuffer.Clear();

        foreach (var kv in activeFades)
            fadeKeysBuffer.Add(kv.Key);

        for (int i = 0; i < fadeKeysBuffer.Count; i++)
        {
            GameObject decal = fadeKeysBuffer[i];
            if (!activeFades.TryGetValue(decal, out FadeState fade))
                continue;

            if (decal == null || !decal.activeSelf)
            {
                clearBuffer.Add(decal);
                continue;
            }

            fade.Elapsed += deltaTime;
            float t = Mathf.Clamp01(fade.Elapsed / fade.Duration);

            if (rendererCache.TryGetValue(decal, out var renderer) && renderer != null)
            {
                Color fadeColor = fade.InitialColor;
                fadeColor.a = Mathf.Lerp(fade.InitialColor.a, 0f, t);
                ApplyColor(renderer, fadeColor);
            }

            if (fade.Elapsed >= fade.Duration)
            {
                clearBuffer.Add(decal);
                ReturnDecalToPool(decal);
            }
            else
            {
                activeFades[decal] = fade;
            }
        }

        for (int i = 0; i < clearBuffer.Count; i++)
            activeFades.Remove(clearBuffer[i]);
    }

    void InitializeMaterialLookup()
    {
        // IDE0017: Заполнение словаря через индексатор
        materialLookup[SurfaceMaterial.Default] = defaultDecalMaterial;
        materialLookup[SurfaceMaterial.Wood] = woodDecalMaterial;
        materialLookup[SurfaceMaterial.Concrete] = concreteDecalMaterial;
        materialLookup[SurfaceMaterial.Metal] = metalDecalMaterial;
        materialLookup[SurfaceMaterial.Water] = waterDecalMaterial;
        materialLookup[SurfaceMaterial.Dirt] = dirtDecalMaterial;
        materialLookup[SurfaceMaterial.Glass] = glassDecalMaterial;
    }

    void InitializePool()
    {
        if (decalPrefab == null)
        {
            decalPrefab = CreateDefaultDecalPrefab();
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject decal = Instantiate(decalPrefab, transform);
            decal.SetActive(false);
            decal.name = $"Decal_{i}";
            CacheDecalComponents(decal);
            decalPool.Enqueue(decal);
        }

        _decalInstanceCounter = initialPoolSize;
    }

    GameObject CreateDefaultDecalPrefab()
    {
        GameObject go = new("DefaultDecal"); // IDE0090: упрощено
        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateQuadMesh();
        var renderer = go.AddComponent<MeshRenderer>();

        if (defaultDecalMaterial == null)
        {
            // IDE0017 + IDE0090: упрощено
            defaultDecalMaterial = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.3f, 0.3f, 0.3f, 0.8f)
            };
            defaultDecalMaterial.SetFloat("_Metallic", 0f);
            defaultDecalMaterial.SetFloat("_Glossiness", 0.1f);
            defaultDecalMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            defaultDecalMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            defaultDecalMaterial.SetInt("_ZWrite", 0);
            defaultDecalMaterial.DisableKeyword("_ALPHATEST_ON");
            defaultDecalMaterial.EnableKeyword("_ALPHABLEND_ON");
            defaultDecalMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            defaultDecalMaterial.renderQueue = 3000;
        }

        renderer.material = defaultDecalMaterial;
        return go;
    }

    Mesh CreateQuadMesh()
    {
        Mesh mesh = new(); // IDE0090: упрощено

        // IDE0090: target-typed new для массивов
        Vector3[] vertices =
        {
            new(-0.5f, 0, -0.5f),
            new(0.5f, 0, -0.5f),
            new(-0.5f, 0, 0.5f),
            new(0.5f, 0, 0.5f)
        };

        Vector2[] uv =
        {
            new(0, 0),
            new(1, 0),
            new(0, 1),
            new(1, 1)
        };

        int[] triangles =
        {
            0, 2, 1,
            2, 3, 1
        };

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Спавнит декаль попадания с учётом поверхности и ограничений по расстоянию/количеству.
    /// </summary>
    public void SpawnDecal(Vector3 hitPoint, Vector3 hitNormal, Transform surface, SurfaceMaterial surfaceType = SurfaceMaterial.Default)
    {
        if (!CanPlaceDecal(hitPoint, surfaceType))
            return;

        if (activeDecals.Count >= maxDecalsTotal)
        {
            RemoveOldestDecal();
        }

        GameObject decal = GetDecalFromPool();
        if (decal == null) return;

        SetupDecalTransform(decal, hitPoint, hitNormal, surface);
        SetupDecalMaterial(decal, surfaceType);

        // Квад лежит в плоскости XZ, поэтому все три оси масштабируем одинаково.
        // Y=size (толщина вдоль нормали минимальна, но должна быть ненулевой).
        float size = Random.Range(decalSizeMin, decalSizeMax);
        decal.transform.localScale = new Vector3(size, size, size);

        decal.SetActive(true);
        activeDecals.Add(decal);

        if (fadeEnabled && fadeDuration > 0)
        {
            StartFade(decal, fadeDuration);
        }
    }

    bool CanPlaceDecal(Vector3 position, SurfaceMaterial surfaceType)
    {
        if (surfaceType == SurfaceMaterial.Water || surfaceType == SurfaceMaterial.Glass)
            return false;

        // Используем sqrMagnitude вместо Distance для избежания sqrt на каждой итерации
        float minDistSqr = minDistanceBetweenDecals * minDistanceBetweenDecals;
        for (int i = 0; i < activeDecals.Count; i++)
        {
            var decal = activeDecals[i];
            if (decal != null && (decal.transform.position - position).sqrMagnitude < minDistSqr)
                return false;
        }

        return true;
    }

    void SetupDecalTransform(GameObject decal, Vector3 hitPoint, Vector3 hitNormal, Transform surface)
    {
        float offset = 0.001f;

        // Нормализуем ось лица префаба на случай, если в инспекторе задали ненормализованный вектор.
        Vector3 faceAxis = _normalizedFaceAxis;

        // Выравниваем «лицевую» ось префаба по нормали поверхности.
        Quaternion rotation = Quaternion.FromToRotation(faceAxis, hitNormal);

        // Случайное вращение вокруг нормали поверхности (в мировом пространстве).
        if (randomRotation)
        {
            rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), hitNormal) * rotation;
        }

        Vector3 position = hitPoint + hitNormal * offset;

        // UNT0022: Используем SetPositionAndRotation вместо раздельных присваиваний
        decal.transform.SetPositionAndRotation(position, rotation);

        if (surface != null)
        {
            decal.transform.parent = surface;

            // Пересчитываем в локальном пространстве поверхности,
            // чтобы декаль корректно двигалась вместе с объектом (Rigidbody и т.д.)
            Vector3 localHitPoint = surface.InverseTransformPoint(hitPoint);
            // InverseTransformDirection не учитывает масштаб — нормаль остаётся нормализованной.
            Vector3 localNormal   = surface.InverseTransformDirection(hitNormal).normalized;
            Vector3 localPosition = localHitPoint + localNormal * offset;

            Quaternion localRotation = Quaternion.FromToRotation(faceAxis, localNormal);

            if (randomRotation)
            {
                localRotation = Quaternion.AngleAxis(Random.Range(0f, 360f), localNormal) * localRotation;
            }

            // UNT0032: Используем SetLocalPositionAndRotation вместо раздельных присваиваний
            decal.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        }
    }

    void SetupDecalMaterial(GameObject decal, SurfaceMaterial surfaceType)
    {
        if (!rendererCache.TryGetValue(decal, out var renderer) || renderer == null)
            return;

        Material material = GetMaterialForSurface(surfaceType);
        if (material != null)
        {
            renderer.sharedMaterial = material;

            Color color = material.color;
            color.r *= Random.Range(0.9f, 1.1f);
            color.g *= Random.Range(0.9f, 1.1f);
            color.b *= Random.Range(0.9f, 1.1f);
            decalBaseColor[decal] = color;
            ApplyColor(renderer, color);
        }
    }

    Material GetMaterialForSurface(SurfaceMaterial surfaceType)
    {
        if (materialLookup.TryGetValue(surfaceType, out Material material))
        {
            return material;
        }
        return defaultDecalMaterial;
    }

    GameObject GetDecalFromPool()
    {
        if (decalPool.Count > 0)
        {
            return decalPool.Dequeue();
        }

        GameObject newDecal = Instantiate(decalPrefab, transform);
        newDecal.name = $"Decal_New_{_decalInstanceCounter++}";
        CacheDecalComponents(newDecal);
        return newDecal;
    }

    void RemoveOldestDecal()
    {
        if (activeDecals.Count == 0) return;

        GameObject oldestDecal = activeDecals[0];
        ReturnDecalToPool(oldestDecal);
    }

    void ReturnDecalToPool(GameObject decal)
    {
        if (decal == null) return;

        activeFades.Remove(decal);

        activeDecals.Remove(decal);

        if (rendererCache.TryGetValue(decal, out var renderer) && renderer != null)
        {
            if (decalBaseColor.TryGetValue(decal, out var baseColor))
            {
                baseColor.a = 1f;
                ApplyColor(renderer, baseColor);
            }
        }

        if (this != null && gameObject != null)
        {
            decal.transform.parent = transform;
            // ✅ UNT0032: Объединённый вызов вместо раздельных присваиваний
            decal.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            decal.transform.localScale = Vector3.one;
            decal.SetActive(false);
            decalPool.Enqueue(decal);
        }
        else
        {
            Destroy(decal);
        }
    }

    /// <summary>
    /// Возвращает в пул все активные декали.
    /// </summary>
    [ContextMenu("Очистить все декали")]
    public void ClearAllDecals()
    {
        clearBuffer.Clear();
        clearBuffer.AddRange(activeDecals);

        for (int i = 0; i < clearBuffer.Count; i++)
        {
            ReturnDecalToPool(clearBuffer[i]);
        }

        clearBuffer.Clear();
    }

    void OnDestroy()
    {
        activeFades.Clear();

        foreach (var decal in activeDecals)
        {
            if (decal != null)
            {
                Destroy(decal);
            }
        }
        activeDecals.Clear();

        while (decalPool.Count > 0)
        {
            GameObject decal = decalPool.Dequeue();
            if (decal != null)
            {
                Destroy(decal);
            }
        }

        rendererCache.Clear();
        decalBaseColor.Clear();
    }

    private void CacheDecalComponents(GameObject decal)
    {
        if (decal == null)
            return;

        if (!rendererCache.ContainsKey(decal))
            rendererCache[decal] = decal.GetComponent<Renderer>();
    }

    private void StartFade(GameObject decal, float duration)
    {
        if (decal == null || duration <= 0f)
            return;

        if (!rendererCache.TryGetValue(decal, out var renderer) || renderer == null)
            return;

        Color startColor = decalBaseColor.TryGetValue(decal, out var cachedColor)
            ? cachedColor
            : renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;

        activeFades[decal] = new FadeState
        {
            Elapsed = 0f,
            Duration = duration,
            InitialColor = startColor
        };
    }

    private void ApplyColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        _materialPropertyBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(_materialPropertyBlock);
    }
}