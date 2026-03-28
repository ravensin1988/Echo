using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Визуальный элемент предмета в сетке инвентаря.
/// Создаётся динамически через InventoryUI.
/// Поддерживает:
///   - отображение иконки
///   - счётчик стака
///   - подсветку экипированного предмета
///   - ПКМ → контекстное меню (Экипировать / Выбросить / Использовать)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InventoryItemUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Ссылки на дочерние объекты (задаются в префабе)")]
    [SerializeField] private Image       iconImage;
    [SerializeField] private TextMeshProUGUI stackCountText;
    [SerializeField] private Image       equippedHighlight;   // Рамка/подсветка для экипированного
    [SerializeField] private Image       hoverHighlight;      // Подсветка при наведении

    // ─── Данные ─────────────────────────────────────────────────────────────
    private InventoryItem _item;
    private InventoryUI   _inventoryUI;

    // ─── Инициализация ──────────────────────────────────────────────────────

    public void Initialize(InventoryItem item, InventoryUI ui)
    {
        _item        = item;
        _inventoryUI = ui;
        Refresh();
    }

    /// <summary>Обновить визуал (иконка, стак, подсветка экипировки)</summary>
    public void Refresh()
    {
        if (_item == null || _item.itemData == null) return;

        // Иконка
        if (iconImage != null)
        {
            iconImage.sprite  = _item.itemData.icon;
            iconImage.enabled = _item.itemData.icon != null;
        }

        // Счётчик стака
        if (stackCountText != null)
        {
            bool showCount = _item.itemData.isStackable && _item.amount > 1;
            stackCountText.gameObject.SetActive(showCount);
            if (showCount) stackCountText.text = _item.amount.ToString();
        }

        Debug.Log($"[ItemUI] Установка иконки: {iconImage}, sprite: {_item.itemData.icon?.name ?? "NULL"}");

        // Подсветка экипированного
        RefreshEquippedHighlight();

        // Ховер по умолчанию скрыт
        if (hoverHighlight != null)
            hoverHighlight.enabled = false;
    }

    private void RefreshEquippedHighlight()
    {
        if (equippedHighlight == null) return;
        bool equipped = EquipmentSystem.Instance != null &&
                        EquipmentSystem.Instance.IsEquipped(_item);
        equippedHighlight.enabled = equipped;
    }

    // ─── Клики ──────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            _inventoryUI.ShowContextMenu(_item, transform.position);
        }
    }

    // ─── Ховер ──────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverHighlight != null) hoverHighlight.enabled = true;
        _inventoryUI.ShowTooltip(_item, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverHighlight != null) hoverHighlight.enabled = false;
        _inventoryUI.HideTooltip();
    }

    // ─── Публичный геттер ────────────────────────────────────────────────────
    public InventoryItem Item => _item;
}
