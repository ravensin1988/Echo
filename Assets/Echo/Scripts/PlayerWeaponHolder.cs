using UnityEngine;
using System.Collections;

/// <summary>
/// Связка: тип оружия → точка экипировки на теле персонажа
/// </summary>
[System.Serializable]
public class WeaponEquipPoint
{
    [Tooltip("Тип оружия для этой точки")]
    public WeaponType weaponType;

    [Tooltip("Transform точки на теле персонажа (например, кость правой руки)")]
    public Transform equipPoint;
}

public class PlayerWeaponHolder : MonoBehaviour
{
    /// <summary>
    /// Вызывается после успешной экипировки оружия.
    /// </summary>
    public event System.Action<Weapon> WeaponEquipped;

    /// <summary>
    /// Вызывается после успешного снятия оружия.
    /// </summary>
    public event System.Action<Weapon> WeaponHolstered;

    [Header("Точки экипировки по типу оружия")]
    [Tooltip("Для каждого типа оружия укажите соответствующую точку экипировки")]
    public WeaponEquipPoint[] equipPoints;

    [Tooltip("Резервная точка экипировки, если для данного типа оружия не задана точка")]
    public Transform defaultEquipPoint;

    [Header("Анимация")]
    [Tooltip("Animator персонажа для переключения слоёв оружия")]
    public Animator animator;

    [Header("Текущее состояние")]
    [SerializeField] private Weapon currentWeapon;

    [Header("Настройки поиска")]
    [Tooltip("Автоматически искать оружие в сцене при старте")]
    public bool autoFindWeaponOnStart = true;

    [Tooltip("Радиус (в метрах) в котором работает автоматическая экипировка оружия при старте")]
    public float autoEquipRadius = 1f;

    public Weapon CurrentWeapon => currentWeapon;

    // Кэш индексов слоёв: [weaponType] = layerIndex (кешируется при первом запросе)
    private readonly int[] _layerIndexCache = new int[5];
    private bool _cacheInitialized = false;
    private int _lastAnimatorInstanceId = 0;

    private void Awake()
    {
        // Если Animator не назначен вручную — ищем на этом же объекте
        if (animator == null)
            animator = GetComponent<Animator>();

        InitializeLayerCache();
    }

    private void Start()
    {
        if (autoFindWeaponOnStart)
        {
            FindAndEquipWeapon();
        }
    }

    /// <summary>
    /// Возвращает точку экипировки для указанного типа оружия.
    /// Если точка не задана — возвращает defaultEquipPoint, либо transform самого персонажа.
    /// </summary>
    public Transform GetEquipPointForWeapon(WeaponType weaponType)
    {
        if (equipPoints != null)
        {
            foreach (var ep in equipPoints)
            {
                if (ep.weaponType == weaponType && ep.equipPoint != null)
                    return ep.equipPoint;
            }
        }

        if (defaultEquipPoint != null)
            return defaultEquipPoint;

        return transform;
    }

    /// <summary>
    /// Инициализирует кэш индексов слоёв по именам (оптимизация - поиск выполняется один раз)
    /// </summary>
    private void InitializeLayerCache()
    {
        if (_cacheInitialized && animator != null && animator.GetInstanceID() == _lastAnimatorInstanceId)
            return;

        if (animator == null)
        {
            _cacheInitialized = false;
            return;
        }

        _lastAnimatorInstanceId = animator.GetInstanceID();

        // Инициализируем кэш значениями -1 (не найден)
        for (int i = 0; i < _layerIndexCache.Length; i++)
        {
            _layerIndexCache[i] = -1;
        }

        // Ищем слои по именам enum'а WeaponType
        string[] weaponTypeNames = { "None", "Pistol", "Rifle", "Grenade", "Melee" };

        for (int i = 0; i < weaponTypeNames.Length; i++)
        {
            int layerIndex = animator.GetLayerIndex(weaponTypeNames[i]);
            if (layerIndex > 0) // 0 - это базовый слой, он не используется для оружия
            {
                _layerIndexCache[i] = layerIndex;
            }
        }

        _cacheInitialized = true;
    }

    /// <summary>
    /// Получает индекс слоя по типу оружия (из кэша)
    /// </summary>
    private int GetWeaponLayerIndex(WeaponType type)
    {
        int typeIndex = (int)type;
        if (typeIndex < 0 || typeIndex >= _layerIndexCache.Length)
            return -1;

        return _layerIndexCache[typeIndex];
    }

    /// <summary>
    /// Принудительно переинициализирует кэш слоёв
    /// </summary>
    public void RefreshLayerCache()
    {
        _cacheInitialized = false;
        InitializeLayerCache();
    }

    /// <summary>
    /// Автоматически находит объект с компонентом Weapon в сцене и экипирует его.
    /// Проверяется расстояние: оружие должно быть в радиусе autoEquipRadius от игрока.
    /// Проверка выполняется один раз при старте.
    /// </summary>
    public void FindAndEquipWeapon()
    {
        if (currentWeapon != null)
        {
            if (!IsWeaponInScene(currentWeapon))
            {
                Debug.LogWarning("Текущее оружие было уничтожено, сбрасываем.");
                currentWeapon = null;
            }
            else
            {
                Debug.Log("Оружие уже экипировано.");
                return;
            }
        }

        Weapon[] weaponsInScene = FindObjectsByType<Weapon>(FindObjectsSortMode.None);

        if (weaponsInScene.Length == 0)
        {
            Debug.Log("В сцене не найдено оружие с компонентом Weapon.");
            return;
        }

        Weapon availableWeapon = FindNearestAvailableWeapon(weaponsInScene);

        if (availableWeapon == null)
        {
            Debug.Log($"Нет доступного оружия в радиусе {autoEquipRadius} м.");
            return;
        }

        EquipWeapon(availableWeapon);
    }

    private Weapon FindNearestAvailableWeapon(Weapon[] weaponsInScene)
    {
        Vector3 playerPosition = transform.position;
        float maxSqrDistance = autoEquipRadius * autoEquipRadius;
        float bestSqrDistance = float.MaxValue;
        Weapon bestWeapon = null;

        for (int i = 0; i < weaponsInScene.Length; i++)
        {
            Weapon weapon = weaponsInScene[i];
            if (weapon == null || weapon.isEquipped || !IsWeaponInScene(weapon))
                continue;

            float sqrDistance = (weapon.transform.position - playerPosition).sqrMagnitude;
            if (sqrDistance > maxSqrDistance || sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestWeapon = weapon;
        }

        return bestWeapon;
    }

    /// <summary>
    /// Проверяет, существует ли оружие в сцене
    /// </summary>
    public bool IsWeaponInScene(Weapon weapon)
    {
        if (weapon == null)
            return false;

        return weapon.gameObject != null && weapon.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// Проверяет, экипировано ли указанное оружие
    /// </summary>
    public bool IsWeaponEquipped(Weapon weapon)
    {
        if (weapon == null)
            return false;

        if (!IsWeaponInScene(weapon))
            return false;

        return weapon.isEquipped && currentWeapon == weapon;
    }

    /// <summary>
    /// Проверяет, есть ли экипированное оружие
    /// </summary>
    public bool HasEquippedWeapon()
    {
        if (currentWeapon == null)
            return false;

        return IsWeaponInScene(currentWeapon) && currentWeapon.isEquipped;
    }

    /// <summary>
    /// Экипирует оружие
    /// </summary>
    public void EquipWeapon(Weapon weapon)
    {
        if (!IsWeaponInScene(weapon))
        {
            Debug.LogWarning("Невозможно экипировать: оружие не существует в сцене.");
            return;
        }

        if (IsWeaponEquipped(weapon))
        {
            Debug.Log($"Оружие {weapon.name} уже экипировано.");
            return;
        }

        WeaponType previousWeaponType = currentWeapon != null ? currentWeapon.weaponType : WeaponType.None;

        if (currentWeapon != null && IsWeaponInScene(currentWeapon) && currentWeapon.isEquipped)
        {
            HolsterWeapon();
        }

        StartCoroutine(EquipWeaponCoroutine(weapon, previousWeaponType));
    }

    private IEnumerator EquipWeaponCoroutine(Weapon weapon, WeaponType previousWeaponType)
    {
        yield return null;

        // Выбираем точку экипировки в зависимости от типа оружия
        Transform targetPoint = GetEquipPointForWeapon(weapon.weaponType);

        weapon.Equip(targetPoint, Vector3.zero, Quaternion.identity);
        currentWeapon = weapon;

        // Обновляем анимационные слои
        UpdateWeaponAnimationLayers(previousWeaponType, weapon.weaponType);
        WeaponEquipped?.Invoke(weapon);

        Debug.Log($"Оружие {weapon.name} (тип: {weapon.weaponType}) экипировано в точку '{targetPoint.name}'.");
    }

    /// <summary>
    /// Убирает текущее оружие
    /// </summary>
    public void HolsterWeapon()
    {
        if (currentWeapon == null || !IsWeaponInScene(currentWeapon))
        {
            currentWeapon = null;
            return;
        }

        if (!currentWeapon.isEquipped)
        {
            Debug.Log("Оружие уже убрано.");
            currentWeapon = null;
            return;
        }

        Weapon weaponToHolster = currentWeapon;
        WeaponType weaponType = weaponToHolster.weaponType;

        weaponToHolster.SetVisible(false);
        weaponToHolster.Holster(transform, Vector3.zero, Quaternion.identity);

        // Сбрасываем анимационный слой
        ResetWeaponLayer(weaponType);

        WeaponHolstered?.Invoke(weaponToHolster);
        Debug.Log($"Оружие {weaponToHolster.name} убрано.");
        currentWeapon = null;
    }

    /// <summary>
    /// Получить оружие по типу из сцены
    /// </summary>
    public Weapon GetWeaponByType(WeaponType type)
    {
        Weapon[] weaponsInScene = FindObjectsByType<Weapon>(FindObjectsSortMode.None);
        for (int i = 0; i < weaponsInScene.Length; i++)
        {
            Weapon weapon = weaponsInScene[i];
            if (weapon != null && weapon.weaponType == type)
                return weapon;
        }

        return null;
    }

    /// <summary>
    /// Получить первое доступное (не экипированное) оружие
    /// </summary>
    public Weapon GetFirstAvailableWeapon()
    {
        Weapon[] weaponsInScene = FindObjectsByType<Weapon>(FindObjectsSortMode.None);
        for (int i = 0; i < weaponsInScene.Length; i++)
        {
            Weapon weapon = weaponsInScene[i];
            if (weapon != null && !weapon.isEquipped && IsWeaponInScene(weapon))
                return weapon;
        }

        return null;
    }

    /// <summary>
    /// Подобрать оружие и экипировать его
    /// </summary>
    public void PickupWeapon(Weapon weapon)
    {
        if (weapon == null)
        {
            Debug.LogWarning("Невозможно подобрать: оружие не назначено.");
            return;
        }

        EquipWeapon(weapon);
    }

    /// <summary>
    /// Добавить предмет в инвентарь — делегирует InventorySystem
    /// </summary>
    public bool AddItem(ItemSO item, int amount)
    {
        if (item == null)
        {
            Debug.LogWarning("Невозможно добавить: предмет не назначен.");
            return false;
        }

        var inventory = InventorySystem.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[PlayerWeaponHolder] InventorySystem не найден на сцене!");
            return false;
        }

        return inventory.AddItem(item, amount);
    }

    #region Анимационные слои

    /// <summary>
    /// Обновляет веса анимационных слоёв
    /// </summary>
    private void UpdateWeaponAnimationLayers(WeaponType previousWeaponType, WeaponType currentWeaponType)
    {
        if (animator == null)
            return;

        // Сбрасываем предыдущий слой
        ResetWeaponLayer(previousWeaponType);

        // Устанавливаем новый слой
        SetWeaponLayer(currentWeaponType, 1f);
    }

    /// <summary>
    /// Устанавливает вес слоя для указанного типа оружия
    /// </summary>
    private void SetWeaponLayer(WeaponType type, float weight)
    {
        int layerIndex = GetWeaponLayerIndex(type);
        if (layerIndex < 0)
            return;

        animator.SetLayerWeight(layerIndex, weight);
    }

    /// <summary>
    /// Сбрасывает вес слоя для указанного типа оружия
    /// </summary>
    private void ResetWeaponLayer(WeaponType type)
    {
        if (type == WeaponType.None)
            return;

        SetWeaponLayer(type, 0f);
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!autoFindWeaponOnStart)
            return;

        // Рисуем сферу радиуса автоматической экипировки в редакторе
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.DrawSphere(transform.position, autoEquipRadius);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, autoEquipRadius);
    }
#endif
}
