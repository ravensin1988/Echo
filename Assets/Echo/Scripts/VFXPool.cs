using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VFXPool : MonoBehaviour
{
    [System.Serializable]
    public class PooledEffect
    {
        public string key;
        public GameObject prefab;
        public int initialCount = 30;
        [Tooltip("Максимальное количество активных эффектов одновременно")]
        public int maxActive = 50;
        [Tooltip("Автоматическая очистка старых эффектов при достижении лимита")]
        public bool autoRecycleOldest = true;
    }

    public static VFXPool Instance { get; private set; }

    [SerializeField] private List<PooledEffect> effectTypes = new();

    private readonly Dictionary<string, Queue<GameObject>> pools = new();
    private readonly Dictionary<string, GameObject> prefabCache = new();
    private readonly Dictionary<string, int> activeEffectCount = new();
    private readonly Dictionary<string, List<GameObject>> activeEffectsList = new();
    private readonly Dictionary<string, PooledEffect> effectSettings = new();

    // Кэш для WaitForSeconds
    private static readonly WaitForSeconds cleanupInterval = new(2f);

    // Кэш для компонентов
    private readonly Dictionary<GameObject, VFXLifetime> lifetimeCache = new();
    private readonly Dictionary<GameObject, ParticleSystem> particleCache = new();
    private readonly Dictionary<GameObject, AudioSource> audioCache = new();
    private readonly Dictionary<VFXLifetime, string> lifetimeKeyCache = new();

    private readonly List<string> poolKeysBuffer = new(16);

    // Кэшированные значения для оптимизации
    private static readonly Quaternion IdentityRotation = Quaternion.identity;
    private static readonly Vector3 ZeroVector = Vector3.zero;
    private static readonly Vector3 OneVector = Vector3.one;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePool();
    }

    void InitializePool()
    {
        foreach (var effect in effectTypes)
        {
            if (effect.prefab == null) continue;

            var queue = new Queue<GameObject>();
            prefabCache[effect.key] = effect.prefab;
            activeEffectCount[effect.key] = 0;
            activeEffectsList[effect.key] = new List<GameObject>();
            effectSettings[effect.key] = effect;

            for (int i = 0; i < effect.initialCount; i++)
            {
                GameObject obj = Instantiate(effect.prefab, transform);
                obj.name = $"{effect.key}_pooled_{i}";
                SetupVFXObject(obj, effect.key);
                queue.Enqueue(obj);
            }
            pools[effect.key] = queue;
        }
    }

    void Start()
    {
        StartCoroutine(PeriodicCleanup());
    }

    IEnumerator PeriodicCleanup()
    {
        while (true)
        {
            yield return cleanupInterval;
            ValidateActiveEffects();
        }
    }

    void SetupVFXObject(GameObject obj, string key)
    {
        obj.SetActive(false);

        // Оптимизированная проверка компонента - используем TryGetComponent
        if (!obj.TryGetComponent<VFXLifetime>(out var lifetime))
        {
            lifetime = obj.AddComponent<VFXLifetime>();
        }

        lifetimeCache[obj] = lifetime;
        lifetimeKeyCache[lifetime] = key;

        // Оптимизированное кэширование компонентов
        obj.TryGetComponent<ParticleSystem>(out var ps);
        particleCache[obj] = ps;

        obj.TryGetComponent<AudioSource>(out var audio);
        audioCache[obj] = audio;

        lifetime.OnCompleted += HandleLifetimeCompleted;
    }

    private void HandleLifetimeCompleted(VFXLifetime lifetime)
    {
        if (lifetime == null)
            return;

        GameObject owner = lifetime.gameObject;
        if (owner == null)
            return;

        if (!lifetimeKeyCache.TryGetValue(lifetime, out string key))
            return;

        ReturnToPool(key, owner);
    }

    /// <summary>
    /// Возвращает объект эффекта из пула и активирует его в заданной позиции.
    /// </summary>
    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (!pools.ContainsKey(key))
        {
            return null;
        }

        var settings = effectSettings[key];
        int maxActive = settings.maxActive;

        if (activeEffectCount[key] >= maxActive)
        {
            if (settings.autoRecycleOldest && activeEffectsList[key].Count > 0)
            {
                GameObject oldestEffect = activeEffectsList[key][0];
                if (oldestEffect != null)
                {
                    ReturnToPoolImmediate(key, oldestEffect);
                }
            }
            else
            {
                return null;
            }
        }

        GameObject obj = null;
        var pool = pools[key];

        int attempts = 0;
        while (pool.Count > 0 && obj == null && attempts < 10)
        {
            obj = pool.Dequeue();
            attempts++;

            if (obj == null)
            {
                obj = null;
                continue;
            }
        }

        if (obj == null)
        {
            if (prefabCache.TryGetValue(key, out GameObject prefabToCreate))
            {
                obj = Instantiate(prefabToCreate, transform);
                obj.name = $"{key}_pooled_new";
                SetupVFXObject(obj, key);
            }
        }

        if (obj != null)
        {
            // Оптимизированная установка трансформа
            Transform objTransform = obj.transform;
            objTransform.SetPositionAndRotation(position, rotation);
            objTransform.localScale = OneVector;

            activeEffectsList[key].Add(obj);

            activeEffectCount[key]++;

            obj.SetActive(true);

            // Используем кэшированные компоненты
            if (particleCache.TryGetValue(obj, out ParticleSystem ps) && ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }

            if (audioCache.TryGetValue(obj, out AudioSource audio) && audio != null && audio.clip != null)
            {
                audio.Stop();
                audio.Play();
            }
        }

        return obj;
    }

    void ReturnToPool(string key, GameObject obj)
    {
        if (obj == null || !pools.ContainsKey(key)) return;

        ReturnToPoolImmediate(key, obj);
    }

    void ReturnToPoolImmediate(string key, GameObject obj)
    {
        if (obj == null || !pools.ContainsKey(key)) return;

        if (activeEffectsList.ContainsKey(key))
        {
            activeEffectsList[key].Remove(obj);
        }

        if (activeEffectCount.ContainsKey(key))
        {
            activeEffectCount[key] = Mathf.Max(0, activeEffectCount[key] - 1);
        }

        obj.SetActive(false);

        // ОПТИМИЗАЦИЯ: Используем SetParent с параметрами для минимизации операций
        Transform objTransform = obj.transform;

        // Вариант 1: SetParent с последующим SetLocalPositionAndRotation (Unity 2019.3+)
        objTransform.SetParent(transform, false); // false - сохраняем локальные координаты

        // Используем SetLocalPositionAndRotation для одновременной установки позиции и поворота
        objTransform.SetLocalPositionAndRotation(ZeroVector, IdentityRotation);

        // Устанавливаем масштаб отдельно, так как нет комбинированного метода для всех трех
        objTransform.localScale = OneVector;

        // Используем кэшированные компоненты
        if (particleCache.TryGetValue(obj, out ParticleSystem ps) && ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ps.Clear(true);
        }

        if (audioCache.TryGetValue(obj, out AudioSource audio) && audio != null)
        {
            audio.Stop();
        }

        pools[key].Enqueue(obj);
    }

    private int GetTotalObjectCount(string key)
    {
        if (!pools.ContainsKey(key)) return 0;
        return pools[key].Count + (activeEffectCount.ContainsKey(key) ? activeEffectCount[key] : 0);
    }

    /// <summary>
    /// Возвращает в пул все активные эффекты указанного типа.
    /// </summary>
    public void ClearAllEffects(string key)
    {
        if (!activeEffectsList.ContainsKey(key)) return;

        List<GameObject> list = activeEffectsList[key];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var obj = list[i];
            if (obj != null)
            {
                ReturnToPoolImmediate(key, obj);
            }
        }

        activeEffectsList[key].Clear();
        activeEffectCount[key] = 0;
    }

    /// <summary>
    /// Удаляет уничтоженные ссылки из пулов и списков активных эффектов.
    /// </summary>
    public void CleanupUnusedEffects()
    {
        poolKeysBuffer.Clear();
        foreach (var kvp in pools)
            poolKeysBuffer.Add(kvp.Key);

        for (int i = 0; i < poolKeysBuffer.Count; i++)
        {
            string key = poolKeysBuffer[i];
            var pool = pools[key];
            var validObjects = new Queue<GameObject>();

            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                {
                    validObjects.Enqueue(obj);
                }
            }

            pools[key] = validObjects;
        }

        ValidateActiveEffects();
        Resources.UnloadUnusedAssets();
    }

    void ValidateActiveEffects()
    {
        foreach (var kvp in activeEffectsList)
        {
            string key = kvp.Key;
            var list = kvp.Value;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                    if (activeEffectCount.ContainsKey(key))
                    {
                        activeEffectCount[key] = Mathf.Max(0, activeEffectCount[key] - 1);
                    }
                }
            }
        }
    }

    void OnDestroy()
    {
        StopAllCoroutines();

        foreach (var kvp in pools)
        {
            while (kvp.Value.Count > 0)
            {
                var obj = kvp.Value.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }

        pools.Clear();
        activeEffectsList.Clear();
        activeEffectCount.Clear();
        prefabCache.Clear();
        effectSettings.Clear();

        // Очищаем кэши компонентов
        foreach (var kvp in lifetimeCache)
        {
            if (kvp.Value != null)
                kvp.Value.OnCompleted -= HandleLifetimeCompleted;
        }

        lifetimeCache.Clear();
        lifetimeKeyCache.Clear();
        particleCache.Clear();
        audioCache.Clear();
    }

    /// <summary>
    /// Выводит статистику пулов эффектов в консоль.
    /// </summary>
    public void LogPoolStatus()
    {
        foreach (var kvp in pools)
        {
            string key = kvp.Key;
            int poolSize = kvp.Value.Count;
            int activeCount = activeEffectCount.ContainsKey(key) ? activeEffectCount[key] : 0;
            int maxActive = effectSettings.ContainsKey(key) ? effectSettings[key].maxActive : 20;

            Debug.Log($"VFXPool '{key}': {activeCount}/{maxActive} active, {poolSize} in pool, {activeCount + poolSize} total");
        }
    }
}