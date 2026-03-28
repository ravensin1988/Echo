using System;
using UnityEngine;

/// <summary>
/// MonoBehaviour-менеджер инвентаря игрока.
/// Добавляется на объект Player вместе с PlayerWeaponHolder.
/// </summary>
public class InventorySystem : MonoBehaviour
{
    [Header("Размер сетки инвентаря")]
    [Tooltip("Количество колонок")]
    [Min(1)] public int gridWidth  = 10;
    [Tooltip("Количество строк")]
    [Min(1)] public int gridHeight = 6;

    [Header("Ссылки на управление (для блокировки при открытом инвентаре)")]
    [Tooltip("CameraController на камере — будет ставиться на паузу при открытом инвентаре")]
    [SerializeField] private CameraController cameraController;
    [Tooltip("PlayerCamera (альтернативный контроллер камеры) — опционально")]
    [SerializeField] private PlayerCamera playerCamera;

    // Компоненты движения игрока — отключаются при открытом инвентаре
    // Заполняются автоматически при старте (ищем на том же объекте и детях)
    private MonoBehaviour[] _movementComponents;

    // ─── Внутреннее состояние ───────────────────────────────────────────────
    private InventoryGrid _grid;

    // ─── События ────────────────────────────────────────────────────────────
    /// <summary>Вызывается при любом изменении содержимого инвентаря</summary>
    public event Action OnInventoryChanged;

    /// <summary>Вызывается при открытии/закрытии инвентаря (bool = isOpen)</summary>
    public event Action<bool> OnInventoryToggled;

    private bool _isOpen = false;
    public bool IsOpen => _isOpen;

    public InventoryGrid Grid => _grid;

    // ─── Синглтон для лёгкого доступа ───────────────────────────────────────
    public static InventorySystem Instance { get; private set; }

    private void Awake()
    {
        // Если инвентарь один на сцену — используем мягкий синглтон
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        _grid = new InventoryGrid(gridWidth, gridHeight);
    }

    private void Start()
    {
        // Собираем все компоненты движения/ввода на корне игрока и его детях
        // Ищем только компоненты которые явно управляют вводом
        CollectMovementComponents();
    }

    /// <summary>
    /// Собирает компоненты движения/камеры которые нужно отключать при открытом инвентаре.
    /// Ищет на корне игрока И в сцене глобально (для WeaponController на оружии).
    /// </summary>
    private void CollectMovementComponents()
    {
        var root = transform.root;
        var list = new System.Collections.Generic.List<MonoBehaviour>();

        // ── Компоненты на объекте игрока ────────────────────────────────────

        var mc = root.GetComponentInChildren<CharacterMovement>(true);
        if (mc != null) list.Add(mc);

        var pc = root.GetComponentInChildren<PlayerController_TPS>(true);
        if (pc != null) list.Add(pc);

        // CameraController — на камере (не обязательно ребёнок игрока)
        var cc = root.GetComponentInChildren<CameraController>(true);
        if (cc == null) cc = FindAnyObjectByType<CameraController>();
        if (cc != null)
        {
            if (!list.Contains(cc)) list.Add(cc);
            if (cameraController == null) cameraController = cc;
        }

        // PlayerCamera — альтернативный контроллер камеры
        var pca = root.GetComponentInChildren<PlayerCamera>(true);
        if (pca == null) pca = FindAnyObjectByType<PlayerCamera>();
        if (pca != null)
        {
            if (!list.Contains(pca)) list.Add(pca);
            if (playerCamera == null) playerCamera = pca;
        }

        // CameraSwitcher — переключатель камер (прицеливание)
        var cs = root.GetComponentInChildren<CameraSwitcher>(true);
        if (cs == null) cs = FindAnyObjectByType<CameraSwitcher>();
        if (cs != null && !list.Contains(cs)) list.Add(cs);

        // ── WeaponController — на объекте оружия в сцене ────────────────────
        // Найти все WeaponController в сцене (может быть несколько оружий)
        var allWC = FindObjectsByType<WeaponController>(FindObjectsSortMode.None);
        foreach (var wc in allWC)
            if (!list.Contains(wc)) list.Add(wc);

        _movementComponents = list.ToArray();
        Debug.Log($"[InventorySystem] Найдено {_movementComponents.Length} компонентов для блокировки: " +
                  string.Join(", ", System.Array.ConvertAll(_movementComponents, m =>
                      $"{m.GetType().Name}({m.gameObject.name})")));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    // ─── Открытие/закрытие ──────────────────────────────────────────────────

    public void ToggleInventory()
    {
        _isOpen = !_isOpen;
        OnInventoryToggled?.Invoke(_isOpen);
        ApplyInputBlocking(_isOpen);
    }

    public void OpenInventory()
    {
        if (_isOpen) return;
        _isOpen = true;
        OnInventoryToggled?.Invoke(true);
        ApplyInputBlocking(true);
    }

    public void CloseInventory()
    {
        if (!_isOpen) return;
        _isOpen = false;
        OnInventoryToggled?.Invoke(false);
        ApplyInputBlocking(false);
    }

    /// <summary>
    /// Блокирует/разблокирует управление и курсор.
    /// — CameraController и PlayerCamera: используют свои методы паузы (не отключаются через enabled)
    /// — CameraSwitcher: отключаем и сбрасываем состояние прицеливания
    /// — Остальные компоненты движения: отключаются через enabled = false
    /// </summary>
    private void ApplyInputBlocking(bool inventoryOpen)
    {
        // ── Специальные обработчики камеры ──────────────────────────────────
        if (cameraController != null)
        {
            // Не отключаем enabled — CameraController нужен для LateUpdate позиции
            // Только блокируем ввод через встроенный метод
            cameraController.SetGameInputActive(!inventoryOpen);
        }

        if (playerCamera != null)
        {
            playerCamera.SetPaused(inventoryOpen);
            // Сбрасываем состояние прицеливания при закрытии инвентаря
            if (!inventoryOpen)
            {
                playerCamera.SetAiming(false);
            }
        }

        // ── Отключаем остальные компоненты движения через enabled ────────────
        if (_movementComponents != null)
        {
            foreach (var comp in _movementComponents)
            {
                if (comp == null) continue;
                // CameraController и PlayerCamera уже обработаны выше — пропускаем
                if (comp is CameraController || comp is PlayerCamera) continue;

                // CameraSwitcher — сбрасываем состояние прицеливания
                if (comp is CameraSwitcher cs)
                {
                    cs.SetAiming(false);
                }

                comp.enabled = !inventoryOpen;
            }
        }

        // ── Курсор ───────────────────────────────────────────────────────────
        Cursor.visible   = inventoryOpen;
        Cursor.lockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;

        Debug.Log($"[InventorySystem] Инвентарь {(inventoryOpen ? "ОТКРЫТ" : "ЗАКРЫТ")} — " +
                  $"управление {(inventoryOpen ? "заблокировано" : "разблокировано")}");
    }

    // ─── Добавление предмета ────────────────────────────────────────────────

    /// <summary>
    /// Добавить предмет в инвентарь.
    /// Возвращает true, если хотя бы часть поместилась.
    /// </summary>
    public bool AddItem(ItemSO itemData, int amount = 1)
    {
        if (itemData == null) return false;

        bool placed = _grid.TryAddItem(itemData, amount, out int leftover);

        if (placed || leftover < amount)
            OnInventoryChanged?.Invoke();

        if (!placed)
            Debug.Log($"[Inventory] Нет места для: {itemData.itemName}");

        return placed;
    }

    // ─── Удаление предмета ──────────────────────────────────────────────────

    public void RemoveItem(InventoryItem item)
    {
        _grid.RemoveItem(item);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Уменьшить количество предмета на delta. Если количество ≤ 0 — удаляет.
    /// </summary>
    public void ConsumeItem(InventoryItem item, int delta = 1)
    {
        if (item == null) return;
        item.amount -= delta;
        if (item.amount <= 0)
            RemoveItem(item);
        else
            OnInventoryChanged?.Invoke();
    }

    // ─── Поиск ──────────────────────────────────────────────────────────────

    public InventoryItem FindItem(ItemSO itemData)
    {
        foreach (var item in _grid.Items)
            if (item.itemData == itemData)
                return item;
        return null;
    }

    public bool HasItem(ItemSO itemData) => FindItem(itemData) != null;
}
