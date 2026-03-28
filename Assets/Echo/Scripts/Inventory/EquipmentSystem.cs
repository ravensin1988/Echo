using System;
using UnityEngine;

/// <summary>
/// Система экипировки. Управляет надетыми предметами.
/// Для оружия — спавнит GameObject из ItemSO.worldPrefab и передаёт PlayerWeaponHolder.
///
/// ВАЖНО: Добавьте этот компонент на тот же объект Player, где есть InventorySystem.
/// PlayerWeaponHolder может быть на том же объекте или будет найден автоматически.
/// </summary>
[RequireComponent(typeof(InventorySystem))]
public class EquipmentSystem : MonoBehaviour
{
    // ─── Слоты экипировки ────────────────────────────────────────────────────
    // НЕ SerializeField — InventoryItem не MonoBehaviour
    private InventoryItem _equippedWeaponItem;

    // Ссылка на PlayerWeaponHolder
    private PlayerWeaponHolder _weaponHolder;
    private InventorySystem    _inventory;

    // Ссылка на заспавненный GameObject оружия (чтобы уничтожить при снятии)
    private GameObject _spawnedWeaponObject;

    // ─── События ────────────────────────────────────────────────────────────
    public event Action<EquipSlot, InventoryItem> OnEquipmentChanged;

    // ─── Синглтон ────────────────────────────────────────────────────────────
    public static EquipmentSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        _inventory = GetComponent<InventorySystem>();
    }

    private void Start()
    {
        // Ищем PlayerWeaponHolder — сначала на том же объекте, затем глобально
        _weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (_weaponHolder == null)
            _weaponHolder = GetComponentInChildren<PlayerWeaponHolder>(true);
        if (_weaponHolder == null)
            _weaponHolder = FindAnyObjectByType<PlayerWeaponHolder>();

        if (_weaponHolder == null)
            Debug.LogWarning("[EquipmentSystem] PlayerWeaponHolder не найден! " +
                             "Физическая экипировка оружия работать не будет. " +
                             "Добавьте PlayerWeaponHolder на объект Player.");
        else
            Debug.Log($"[EquipmentSystem] PlayerWeaponHolder найден на: {_weaponHolder.gameObject.name}");
    }

    // ─── Экипировка предмета ─────────────────────────────────────────────────

    /// <summary>
    /// Экипировать предмет из инвентаря.
    /// </summary>
    public bool EquipItem(InventoryItem item)
    {
        if (item == null || item.itemData == null)
        {
            Debug.LogWarning("[EquipmentSystem] EquipItem: item или itemData == null");
            return false;
        }

        Debug.Log($"[EquipmentSystem] EquipItem вызван для: '{item.itemData.itemName}' (тип: {item.itemData.itemType})");

        EquipSlot slot = GetSlotForItem(item.itemData);
        if (slot == EquipSlot.None)
        {
            Debug.LogWarning($"[EquipmentSystem] Предмет '{item.itemData.itemName}' нельзя экипировать (тип: {item.itemData.itemType}). " +
                             "Убедитесь что ItemType = Weapon в ScriptableObject.");
            return false;
        }

        // Если слот занят — снимаем
        if (GetSlot(slot) != null)
        {
            Debug.Log($"[EquipmentSystem] Слот {slot} занят, снимаем предыдущее...");
            UnequipSlot(slot);
        }

        // Убираем из инвентаря
        _inventory.RemoveItem(item);

        // Помещаем в слот
        _equippedWeaponItem = item;

        // Спавним физический объект
        if (slot == EquipSlot.Weapon)
            SpawnAndEquipWeapon(item);

        OnEquipmentChanged?.Invoke(slot, item);
        Debug.Log($"[EquipmentSystem] ✓ Экипировано: '{item.itemData.itemName}' в слот {slot}");
        return true;
    }

    /// <summary>
    /// Снять предмет из слота и вернуть в инвентарь.
    /// </summary>
    public void UnequipSlot(EquipSlot slot)
    {
        InventoryItem current = GetSlot(slot);
        if (current == null)
        {
            Debug.Log($"[EquipmentSystem] UnequipSlot({slot}): слот пуст");
            return;
        }

        Debug.Log($"[EquipmentSystem] Снимаем из слота {slot}: '{current.itemData.itemName}'");

        // Убираем физический объект оружия
        if (slot == EquipSlot.Weapon)
        {
            if (_weaponHolder != null)
                _weaponHolder.HolsterWeapon();

            if (_spawnedWeaponObject != null)
            {
                Destroy(_spawnedWeaponObject);
                _spawnedWeaponObject = null;
                Debug.Log("[EquipmentSystem] Заспавненный объект оружия уничтожен");
            }
        }

        // Возвращаем в инвентарь
        bool returned = _inventory.AddItem(current.itemData, current.amount);
        if (!returned)
            Debug.LogWarning($"[EquipmentSystem] Нет места в инвентаре для '{current.itemData.itemName}'! Предмет потерян.");
        else
            Debug.Log($"[EquipmentSystem] '{current.itemData.itemName}' возвращён в инвентарь");

        // Очищаем слот
        if (slot == EquipSlot.Weapon)
            _equippedWeaponItem = null;

        OnEquipmentChanged?.Invoke(slot, null);
    }

    // ─── Геттеры ─────────────────────────────────────────────────────────────

    public InventoryItem GetEquippedWeapon() => _equippedWeaponItem;

    public InventoryItem GetSlot(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => _equippedWeaponItem,
        _ => null
    };

    public bool IsEquipped(InventoryItem item)
    {
        if (item == null) return false;
        return _equippedWeaponItem == item;
    }

    // ─── Спавн и экипировка физического оружия ───────────────────────────────

    private void SpawnAndEquipWeapon(InventoryItem item)
    {
        // Пробуем найти WeaponHolder ещё раз если не нашли раньше
        if (_weaponHolder == null)
        {
            _weaponHolder = FindAnyObjectByType<PlayerWeaponHolder>();
            if (_weaponHolder == null)
            {
                Debug.LogError("[EquipmentSystem] PlayerWeaponHolder не найден в сцене! " +
                               "Оружие будет в инвентаре как 'экипированное' но физически не появится.");
                return;
            }
        }

        if (item.itemData.worldPrefab == null)
        {
            Debug.LogWarning($"[EquipmentSystem] У предмета '{item.itemData.itemName}' не задан worldPrefab! " +
                             "Назначьте префаб оружия в поле 'World Prefab' ItemSO.");
            return;
        }

        // Спавним объект оружия без позиции — PlayerWeaponHolder сам выберет точку по типу
        GameObject weaponObj = Instantiate(item.itemData.worldPrefab);
        _spawnedWeaponObject = weaponObj;

        Weapon weaponComp = weaponObj.GetComponent<Weapon>();
        if (weaponComp != null)
        {
            Debug.Log($"[EquipmentSystem] Спавним оружие типа '{weaponComp.weaponType}' — PlayerWeaponHolder выберет точку крепления");
            _weaponHolder.EquipWeapon(weaponComp);
            Debug.Log($"[EquipmentSystem] ✓ Weapon компонент найден, передан в PlayerWeaponHolder");
        }
        else
        {
            Debug.LogError($"[EquipmentSystem] Префаб '{item.itemData.worldPrefab.name}' НЕ содержит компонент Weapon! " +
                           "Добавьте компонент Weapon на префаб оружия.");
            Destroy(weaponObj);
            _spawnedWeaponObject = null;
        }
    }

    // ─── Вспомогательные ─────────────────────────────────────────────────────

    private static EquipSlot GetSlotForItem(ItemSO itemData)
    {
        return itemData.itemType switch
        {
            ItemType.Weapon => EquipSlot.Weapon,
            _ => EquipSlot.None
        };
    }
}

/// <summary>
/// Перечисление слотов экипировки
/// </summary>
public enum EquipSlot
{
    None,
    Weapon
    // В будущем: Helmet, Armor, Boots, Gloves, ...
}
