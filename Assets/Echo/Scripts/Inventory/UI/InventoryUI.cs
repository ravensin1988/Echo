using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Главный UI менеджер инвентаря.
/// Управляет: сеткой, контекстным меню, тултипом, слотом экипировки.
///
/// Настройка в Unity:
///   1. Создайте Canvas (Screen Space - Overlay).
///   2. Создайте дочерний панель InventoryPanel (этот компонент).
///   3. Внутри: GridContainer (горизонтальный parent для ячеек), ContextMenu панель, Tooltip панель.
///   4. Назначьте префаб SlotPrefab (1×1 ячейка) и ItemPrefab (иконка предмета).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ─── Ссылки на UI ───────────────────────────────────────────────────────
    [Header("Панели")]
    [Tooltip("Корневой объект всего окна инвентаря")]
    [SerializeField] private GameObject inventoryPanel;

    [Tooltip("RectTransform внутри которого генерируются ячейки")]
    [SerializeField] private RectTransform gridContainer;

    [Header("Размер ячейки (пиксели)")]
    [SerializeField] private float cellSize = 64f;
    [SerializeField] private float cellGap  = 2f;

    [Header("Префабы")]
    [Tooltip("Префаб одной ячейки фона (Image)")]
    [SerializeField] private GameObject slotPrefab;

    [Tooltip("Префаб визуала предмета (содержит InventoryItemUI)")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Контекстное меню")]
    [SerializeField] private GameObject contextMenuPanel;
    [SerializeField] private Button     btnEquip;
    [SerializeField] private Button     btnUnequip;
    [SerializeField] private Button     btnDrop;

    [Header("Тултип")]
    [SerializeField] private GameObject      tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipName;
    [SerializeField] private TextMeshProUGUI tooltipDescription;
    [SerializeField] private TextMeshProUGUI tooltipSize;

    [Header("Слот экипировки (опционально)")]
    [Tooltip("Image, показывающий иконку надетого оружия")]
    [SerializeField] private Image equippedWeaponIcon;
    [SerializeField] private TextMeshProUGUI equippedWeaponName;

    // ─── Внутренние данные ──────────────────────────────────────────────────
    private InventorySystem  _inventory;
    private EquipmentSystem  _equipment;

    // Список UI-объектов ячеек фона (фиксированные)
    private readonly List<GameObject> _slotObjects = new List<GameObject>();
    // Словарь: InventoryItem → соответствующий InventoryItemUI
    private readonly Dictionary<InventoryItem, InventoryItemUI> _itemUIs =
        new Dictionary<InventoryItem, InventoryItemUI>();

    // Предмет для которого открыто контекстное меню
    private InventoryItem _contextItem;

    // Фрейм в котором было открыто контекстное меню — чтобы не закрывать сразу
    private int _contextMenuOpenFrame = -1;
    private int _hideContextMenuAtFrame = -1;

    private readonly List<RaycastResult> _raycastResultsBuffer = new(16);
    private PointerEventData _pointerEventData;
    private readonly List<InventoryItem> _removeItemsBuffer = new(16);

    private Transform _cachedPlayerTransform;
    private Camera _cachedMainCamera;

    private readonly Dictionary<Texture, Material> _iconMaterialCache = new(8);

    private static FieldInfo _pickupItemDataField;
    private static FieldInfo _pickupAmountField;

    // ─── Unity Events ────────────────────────────────────────────────────────

    private void Awake()
    {
        // Кнопки настраиваем сразу — они могут понадобиться до Start
        SetupContextMenuButtons();

        CachePickupReflectionFields();
    }

    private void Start()
    {
        // Находим системы
        _inventory = InventorySystem.Instance;
        _equipment = EquipmentSystem.Instance;
        _cachedMainCamera = Camera.main;

        if (_inventory == null)
        {
            Debug.LogError("[InventoryUI] InventorySystem.Instance не найден! Добавьте InventorySystem на Player.");
            return;
        }

        if (_equipment == null)
            Debug.LogWarning("[InventoryUI] EquipmentSystem.Instance не найден! " +
                             "Добавьте компонент EquipmentSystem на тот же объект что и InventorySystem. " +
                             "Экипировка предметов работать не будет.");
        else
            Debug.Log($"[InventoryUI] EquipmentSystem найден на: {_equipment.gameObject.name}");

        // Подписываемся на события
        _inventory.OnInventoryToggled += OnInventoryToggled;
        _inventory.OnInventoryChanged += RefreshAll;

        if (_equipment != null)
            _equipment.OnEquipmentChanged += OnEquipmentChanged;

        // Скрываем панели ПОСЛЕ подписки, синхронизируя с реальным состоянием инвентаря
        // (это гарантирует что SetActive(false) не убьёт сам объект с InventoryUI)
        SyncPanelVisibility(_inventory.IsOpen);

        // Строим сетку фоновых ячеек
        BuildGrid();
    }

    private void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.OnInventoryToggled -= OnInventoryToggled;
            _inventory.OnInventoryChanged -= RefreshAll;
        }
        if (_equipment != null)
            _equipment.OnEquipmentChanged -= OnEquipmentChanged;

        foreach (var kv in _iconMaterialCache)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        _iconMaterialCache.Clear();
    }

    private void Update()
    {
        if (_hideContextMenuAtFrame >= 0 && Time.frameCount >= _hideContextMenuAtFrame)
        {
            _hideContextMenuAtFrame = -1;
            HideContextMenu();
        }

        // Закрытие контекстного меню по клику МИМО него.
        // Не реагируем в тот же фрейм когда меню было открыто (_contextMenuOpenFrame).
        if (contextMenuPanel != null && contextMenuPanel.activeSelf &&
            Time.frameCount > _contextMenuOpenFrame)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                // Проверяем попал ли клик в контекстное меню через EventSystem
                bool clickedOnContextMenu = IsPointerOverObject(contextMenuPanel);

                if (!clickedOnContextMenu)
                {
                    // Откладываем скрытие на 1 кадр чтобы Button.onClick успел сработать.
                    _hideContextMenuAtFrame = Time.frameCount + 1;
                }
                // Если кликнули В меню — ничего не делаем, Button.onClick сам вызовет HideContextMenu
            }
        }
    }

    /// <summary>
    /// Проверяет, находится ли курсор над указанным GameObject или его потомками
    /// </summary>
    private bool IsPointerOverObject(GameObject target)
    {
        if (EventSystem.current == null) return false;

        if (_pointerEventData == null)
            _pointerEventData = new PointerEventData(EventSystem.current);

        _pointerEventData.position = Input.mousePosition;
        _raycastResultsBuffer.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResultsBuffer);

        for (int i = 0; i < _raycastResultsBuffer.Count; i++)
        {
            RaycastResult r = _raycastResultsBuffer[i];
            if (r.gameObject == target ||
                r.gameObject.transform.IsChildOf(target.transform))
                return true;
        }
        return false;
    }

    // ─── Построение сетки ───────────────────────────────────────────────────

    private void BuildGrid()
    {
        if (gridContainer == null || slotPrefab == null || _inventory == null) return;

        // Очищаем старые слоты
        foreach (var s in _slotObjects)
            if (s != null) Destroy(s);
        _slotObjects.Clear();

        int w = _inventory.Grid.Width;
        int h = _inventory.Grid.Height;

        // Устанавливаем размер контейнера
        float totalW = w * (cellSize + cellGap) - cellGap;
        float totalH = h * (cellSize + cellGap) - cellGap;
        gridContainer.sizeDelta = new Vector2(totalW, totalH);

        // Создаём ячейки фона
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                GameObject slot = Instantiate(slotPrefab, gridContainer);
                RectTransform rt = slot.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.up;
                rt.anchorMax = Vector2.up;
                rt.pivot     = Vector2.up;
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = CellToLocalPos(x, y);
                _slotObjects.Add(slot);
            }
        }
    }

    // ─── Обновление UI предметов ────────────────────────────────────────────

    /// <summary>Полное перестроение UI предметов (вызывается при изменении инвентаря)</summary>
    public void RefreshAll()
    {
        if (_inventory == null) return;

        // Удаляем UI предметов которых больше нет
        _removeItemsBuffer.Clear();
        foreach (var kv in _itemUIs)
        {
            if (!ContainsItem(_inventory.Grid.Items, kv.Key))
                _removeItemsBuffer.Add(kv.Key);
        }

        for (int i = 0; i < _removeItemsBuffer.Count; i++)
        {
            InventoryItem item = _removeItemsBuffer[i];
            if (_itemUIs[item] != null)
                Destroy(_itemUIs[item].gameObject);
            _itemUIs.Remove(item);
        }

        // Добавляем/обновляем UI для каждого предмета
        foreach (var item in _inventory.Grid.Items)
        {
            if (!_itemUIs.TryGetValue(item, out InventoryItemUI itemUI) || itemUI == null)
            {
                // Создаём новый UI элемент
                itemUI = CreateItemUI(item);
                _itemUIs[item] = itemUI;
            }
            else
            {
                // Обновляем позицию и визуал
                PositionItemUI(itemUI, item);
                itemUI.Refresh();
            }
        }

        RefreshEquipmentSlotUI();
    }

    private InventoryItemUI CreateItemUI(InventoryItem item)
    {
        if (itemPrefab == null) return null;

        GameObject go = Instantiate(itemPrefab, gridContainer);
        InventoryItemUI ui = go.GetComponent<InventoryItemUI>();
        if (ui == null)
        {
            Debug.LogError("[InventoryUI] itemPrefab не содержит компонент InventoryItemUI!");
            Destroy(go);
            return null;
        }

        ui.Initialize(item, this);
        PositionItemUI(ui, item);
        return ui;
    }

    private void PositionItemUI(InventoryItemUI ui, InventoryItem item)
    {
        RectTransform rt = ui.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.up;
        rt.anchorMax = Vector2.up;
        rt.pivot     = Vector2.up;

        float w = item.Width  * cellSize + (item.Width  - 1) * cellGap;
        float h = item.Height * cellSize + (item.Height - 1) * cellGap;
        rt.sizeDelta       = new Vector2(w, h);
        rt.anchoredPosition = CellToLocalPos(item.gridPosition.x, item.gridPosition.y);
    }

    /// <summary>Конвертирует координаты ячейки в локальную позицию внутри gridContainer</summary>
    private Vector2 CellToLocalPos(int x, int y)
    {
        return new Vector2(
             x * (cellSize + cellGap),
            -y * (cellSize + cellGap)
        );
    }

    // ─── Контекстное меню ───────────────────────────────────────────────────

    public void ShowContextMenu(InventoryItem item, Vector3 worldPos)
    {
        if (contextMenuPanel == null || item == null) return;

        _contextItem = item;

        // Запоминаем фрейм открытия — Update не закроет меню в этот же фрейм
        _contextMenuOpenFrame = Time.frameCount;

        // Определяем доступные действия
        bool canEquip   = CanEquip(item) && (_equipment == null || !_equipment.IsEquipped(item));
        bool canUnequip = _equipment != null && _equipment.IsEquipped(item);

        if (btnEquip   != null) btnEquip  .gameObject.SetActive(canEquip);
        if (btnUnequip != null) btnUnequip.gameObject.SetActive(canUnequip);
        if (btnDrop    != null) btnDrop   .gameObject.SetActive(true);

        // Позиционируем меню у курсора (используем Input.mousePosition для точности)
        Vector3 menuPos = Input.mousePosition;
        // Сдвигаем немного чтобы курсор не был прямо на первой кнопке
        menuPos.x += 5f;
        menuPos.y -= 5f;
        contextMenuPanel.transform.position = menuPos;
        contextMenuPanel.SetActive(true);

        // Поднимаем на верх иерархии (чтобы отрисовывалось поверх всего)
        contextMenuPanel.transform.SetAsLastSibling();

        Debug.Log($"[InventoryUI] Контекстное меню открыто для: {item.itemData.itemName} " +
                  $"(Экипировать:{canEquip}, Снять:{canUnequip})");
    }

    public void HideContextMenu()
    {
        if (contextMenuPanel != null)
            contextMenuPanel.SetActive(false);
        _contextItem = null;
    }

    private void SetupContextMenuButtons()
    {
        if (btnEquip   != null) btnEquip  .onClick.AddListener(OnClickEquip);
        if (btnUnequip != null) btnUnequip.onClick.AddListener(OnClickUnequip);
        if (btnDrop    != null) btnDrop   .onClick.AddListener(OnClickDrop);
    }

    private void OnClickEquip()
    {
        Debug.Log($"[InventoryUI] OnClickEquip нажата. _contextItem={_contextItem?.itemData?.itemName ?? "null"}, _equipment={(_equipment != null ? _equipment.gameObject.name : "null")}");

        if (_contextItem == null)
        {
            Debug.LogError("[InventoryUI] OnClickEquip: _contextItem == null!");
            HideContextMenu();
            return;
        }

        // Попробуем найти EquipmentSystem если он не был найден при Start
        if (_equipment == null)
            _equipment = EquipmentSystem.Instance;

        if (_equipment == null)
        {
            Debug.LogError("[InventoryUI] EquipmentSystem не найден! Добавьте компонент EquipmentSystem на объект Player (рядом с InventorySystem).");
            HideContextMenu();
            return;
        }

        bool success = _equipment.EquipItem(_contextItem);
        Debug.Log($"[InventoryUI] EquipItem вернул: {success}");
        HideContextMenu();
    }

    private void OnClickUnequip()
    {
        Debug.Log($"[InventoryUI] OnClickUnequip нажата. _contextItem={_contextItem?.itemData?.itemName ?? "null"}");

        if (_contextItem == null)
        {
            HideContextMenu();
            return;
        }

        if (_equipment == null)
            _equipment = EquipmentSystem.Instance;

        if (_equipment == null)
        {
            Debug.LogError("[InventoryUI] EquipmentSystem не найден!");
            HideContextMenu();
            return;
        }

        _equipment.UnequipSlot(EquipSlot.Weapon);
        HideContextMenu();
    }

    private void OnClickDrop()
    {
        if (_contextItem == null || _inventory == null) { HideContextMenu(); return; }

        // Позиция — перед игроком, на уровне земли (у ног)
        Vector3 dropPos;
        Transform playerTransform = null;
        
        // Пытаемся найти игрока через тег
        if (_cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _cachedPlayerTransform = player.transform;
        }

        playerTransform = _cachedPlayerTransform;
        
        // Fallback на InventorySystem
        if (playerTransform == null && _inventory != null)
        {
            playerTransform = _inventory.transform;
        }
        
        if (_cachedMainCamera == null)
            _cachedMainCamera = Camera.main;

        if (playerTransform != null && _cachedMainCamera != null)
        {
            // Позиция перед игроком, чуть выше уровня земли
            dropPos = playerTransform.position + _cachedMainCamera.transform.forward * 1.5f;
            dropPos.y = playerTransform.position.y + 0.3f; // На 30 см выше ног
        }
        else
        {
            dropPos = Vector3.zero;
        }

        GameObject droppedObj = null;

        // Проверяем специальный pickupPrefab в WeaponSO
        if (_contextItem.itemData.itemType == ItemType.Weapon && 
            _contextItem.itemData.worldPrefab != null)
        {
            // Получаем WeaponSO из worldPrefab
            WeaponController weaponCtrl = _contextItem.itemData.worldPrefab.GetComponent<WeaponController>();
            if (weaponCtrl != null && weaponCtrl.weaponData != null && 
                weaponCtrl.weaponData.pickupPrefab != null)
            {
                // Используем pickupPrefab из WeaponSO
                droppedObj = Instantiate(weaponCtrl.weaponData.pickupPrefab, dropPos, Quaternion.identity);
                
                // Настраиваем PickupItem
                PickupItem pickup = droppedObj.GetComponent<PickupItem>();
                if (pickup != null)
                {
                    SetPickupItemData(pickup, _contextItem);
                }
            }
            else
            {
                // Нет pickupPrefab — создаём новый объект
                droppedObj = CreatePickupObject(dropPos, _contextItem);
            }
        }
        else if (_contextItem.itemData.worldPrefab != null)
        {
            // Проверяем есть ли PickupItem на worldPrefab
            PickupItem prefabPickup = _contextItem.itemData.worldPrefab.GetComponent<PickupItem>();
            
            if (prefabPickup != null)
            {
                // Префаб содержит PickupItem — используем его напрямую
                droppedObj = Instantiate(_contextItem.itemData.worldPrefab, dropPos, Quaternion.identity);
            }
            else
            {
                // Префаб не содержит PickupItem — создаём новый объект
                droppedObj = CreatePickupObject(dropPos, _contextItem);
            }
        }
        else
        {
            // Префаба нет — создаём новый объект
            droppedObj = CreatePickupObject(dropPos, _contextItem);
        }

        _inventory.RemoveItem(_contextItem);
        HideContextMenu();
    }

    /// <summary>
    /// Создаёт новый объект с PickupItem для выбрасывания
    /// </summary>
    private GameObject CreatePickupObject(Vector3 position, InventoryItem inventoryItem)
    {
        GameObject droppedObj = new GameObject($"Dropped_{inventoryItem.itemData.itemName}");
        droppedObj.transform.position = position;
        
        // Добавляем PickupItem с данными
        PickupItem pickup = droppedObj.AddComponent<PickupItem>();
        SetPickupItemData(pickup, inventoryItem);
        
        // Добавляем визуальную сферу для видимости
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.parent = droppedObj.transform;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.3f;
        // Убираем коллайдер с визуала (уже есть на родителе)
        Destroy(visual.GetComponent<Collider>());
        
        // Добавляем материал если есть иконка
        if (inventoryItem.itemData.icon != null)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Используем кеш материалов по texture, чтобы не создавать новый Material на каждый дроп.
                Texture iconTexture = inventoryItem.itemData.icon.texture;
                if (iconTexture != null)
                    renderer.sharedMaterial = GetOrCreateIconMaterial(iconTexture);
            }
        }
        
        return droppedObj;
    }

    /// <summary>
    /// Устанавливает данные предмета на компонент PickupItem через рефлексию
    /// </summary>
    private void SetPickupItemData(PickupItem pickup, InventoryItem inventoryItem)
    {
        if (pickup == null || inventoryItem == null) return;

        if (_pickupItemDataField != null)
            _pickupItemDataField.SetValue(pickup, inventoryItem.itemData);
        if (_pickupAmountField != null)
            _pickupAmountField.SetValue(pickup, inventoryItem.amount);
    }

    // ─── Тултип ─────────────────────────────────────────────────────────────

    public void ShowTooltip(InventoryItem item, Vector3 pos)
    {
        if (tooltipPanel == null || item == null) return;

        if (tooltipName != null)
            tooltipName.text = item.itemData.itemName;

        if (tooltipDescription != null)
            tooltipDescription.text = item.itemData.description;

        if (tooltipSize != null)
        {
            if (item.itemData.isStackable)
                tooltipSize.SetText("Размер: {0}×{1}  Стак: {2}/{3}", item.itemData.sizeX, item.itemData.sizeY, item.amount, item.itemData.maxStackSize);
            else
                tooltipSize.SetText("Размер: {0}×{1}", item.itemData.sizeX, item.itemData.sizeY);
        }

        tooltipPanel.transform.position = pos + new Vector3(10f, -10f, 0f);
        tooltipPanel.SetActive(true);
        tooltipPanel.transform.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    // ─── Слот экипировки ────────────────────────────────────────────────────

    private void RefreshEquipmentSlotUI()
    {
        if (_equipment == null) return;
        InventoryItem weapon = _equipment.GetEquippedWeapon();

        if (equippedWeaponIcon != null)
        {
            equippedWeaponIcon.sprite  = weapon?.itemData?.icon;
            equippedWeaponIcon.enabled = weapon != null && weapon.itemData.icon != null;
        }

        if (equippedWeaponName != null)
            equippedWeaponName.text = weapon != null ? weapon.itemData.itemName : "—";
    }

    // ─── Callbacks событий ──────────────────────────────────────────────────

    private void OnInventoryToggled(bool isOpen)
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(isOpen);

        if (isOpen)
            RefreshAll();
        else
        {
            HideContextMenu();
            HideTooltip();
        }
    }

    private void OnEquipmentChanged(EquipSlot slot, InventoryItem item)
    {
        // Обновляем подсветку всех предметов
        foreach (var kv in _itemUIs)
            kv.Value?.Refresh();

        RefreshEquipmentSlotUI();
    }

    // ─── Вспомогательные ────────────────────────────────────────────────────

    /// <summary>
    /// Синхронизирует видимость всех панелей с флагом isOpen.
    /// Вызывается один раз при Start() чтобы спрятать панели без риска
    /// деактивировать объект содержащий сам компонент InventoryUI.
    /// </summary>
    private void SyncPanelVisibility(bool isOpen)
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(isOpen);

        // Контекстное меню и тултип всегда скрыты при старте
        if (contextMenuPanel != null)
            contextMenuPanel.SetActive(false);
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private bool CanEquip(InventoryItem item)
    {
        if (item == null || item.itemData == null) return false;
        return item.itemData.itemType == ItemType.Weapon;
        // В будущем: добавить Armor, Helmet и т.д.
    }

    private static bool ContainsItem(IReadOnlyList<InventoryItem> items, InventoryItem target)
    {
        if (items == null || target == null)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target))
                return true;
        }

        return false;
    }

    private static void CachePickupReflectionFields()
    {
        if (_pickupItemDataField != null && _pickupAmountField != null)
            return;

        _pickupItemDataField = typeof(PickupItem).GetField("itemData", BindingFlags.NonPublic | BindingFlags.Instance);
        _pickupAmountField = typeof(PickupItem).GetField("amount", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private Material GetOrCreateIconMaterial(Texture iconTexture)
    {
        if (iconTexture == null)
            return null;

        if (_iconMaterialCache.TryGetValue(iconTexture, out Material cached) && cached != null)
            return cached;

        Material createdMaterial = new Material(Shader.Find("Standard"));
        createdMaterial.mainTexture = iconTexture;
        _iconMaterialCache[iconTexture] = createdMaterial;
        return createdMaterial;
    }

}
