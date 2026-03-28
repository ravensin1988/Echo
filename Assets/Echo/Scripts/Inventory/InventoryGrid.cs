using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Логика сетки инвентаря. Хранит предметы, проверяет размещение, добавляет/удаляет.
/// Не привязан к Unity MonoBehaviour — чистая логика.
/// </summary>
public class InventoryGrid
{
    public readonly int Width;
    public readonly int Height;

    // Сетка: каждая ячейка хранит ссылку на InventoryItem или null
    private readonly InventoryItem[,] _grid;

    // Список всех уникальных предметов
    private readonly List<InventoryItem> _items = new List<InventoryItem>();

    public IReadOnlyList<InventoryItem> Items => _items;

    public InventoryGrid(int width, int height)
    {
        Width  = width;
        Height = height;
        _grid  = new InventoryItem[width, height];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Добавление предмета
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Попытаться добавить предмет (amount штук). 
    /// Сначала пытается доложить в существующие стаки, затем найти свободное место.
    /// Возвращает true, если весь amount поместился.
    /// </summary>
    public bool TryAddItem(ItemSO itemData, int amount, out int leftover)
    {
        leftover = amount;

        // 1) Докладываем в существующие стаки
        if (itemData.isStackable)
        {
            foreach (var existing in _items)
            {
                if (existing.itemData == itemData && existing.amount < itemData.maxStackSize)
                {
                    leftover = existing.AddToStack(leftover);
                    if (leftover == 0) return true;
                }
            }
        }

        // 2) Ищем свободное место для новых стаков / нестакаемых предметов
        while (leftover > 0)
        {
            Vector2Int? freePos = FindFreePosition(itemData, false);
            bool rotated = false;

            // Попробуем повёрнутый вариант, если прямой не подошёл
            if (freePos == null && itemData.sizeX != itemData.sizeY)
            {
                freePos  = FindFreePosition(itemData, true);
                rotated  = true;
            }

            if (freePos == null)
                return false; // Места нет

            int stackAmount = itemData.isStackable
                ? Mathf.Min(leftover, itemData.maxStackSize)
                : 1;

            var newItem = new InventoryItem(itemData, stackAmount, freePos.Value, rotated);
            PlaceItemAt(newItem, freePos.Value);
            _items.Add(newItem);
            leftover -= stackAmount;
        }

        return true;
    }

    /// <summary>
    /// Попытаться разместить предмет по конкретным координатам.
    /// </summary>
    public bool TryPlaceItem(InventoryItem item, Vector2Int position)
    {
        if (!CanPlace(item, position)) return false;
        item.gridPosition = position;
        PlaceItemAt(item, position);
        if (!_items.Contains(item))
            _items.Add(item);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Удаление предмета
    // ─────────────────────────────────────────────────────────────────────────

    public void RemoveItem(InventoryItem item)
    {
        if (!_items.Contains(item)) return;
        ClearCells(item);
        _items.Remove(item);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Запрос данных
    // ─────────────────────────────────────────────────────────────────────────

    public InventoryItem GetItemAt(int x, int y)
    {
        if (!IsInBounds(x, y)) return null;
        return _grid[x, y];
    }

    public InventoryItem GetItemAt(Vector2Int cell) => GetItemAt(cell.x, cell.y);

    public bool CanPlace(InventoryItem item, Vector2Int pos)
    {
        for (int x = pos.x; x < pos.x + item.Width; x++)
        {
            for (int y = pos.y; y < pos.y + item.Height; y++)
            {
                if (!IsInBounds(x, y)) return false;
                if (_grid[x, y] != null && _grid[x, y] != item) return false;
            }
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Вспомогательные методы
    // ─────────────────────────────────────────────────────────────────────────

    private Vector2Int? FindFreePosition(ItemSO itemData, bool rotated)
    {
        int w = rotated ? itemData.sizeY : itemData.sizeX;
        int h = rotated ? itemData.sizeX : itemData.sizeY;

        for (int y = 0; y <= Height - h; y++)
        {
            for (int x = 0; x <= Width - w; x++)
            {
                if (IsFreeAt(x, y, w, h))
                    return new Vector2Int(x, y);
            }
        }
        return null;
    }

    private bool IsFreeAt(int startX, int startY, int w, int h)
    {
        for (int x = startX; x < startX + w; x++)
            for (int y = startY; y < startY + h; y++)
                if (!IsInBounds(x, y) || _grid[x, y] != null)
                    return false;
        return true;
    }

    private void PlaceItemAt(InventoryItem item, Vector2Int pos)
    {
        // Сначала очищаем старые ячейки (если предмет перемещается)
        ClearCells(item);

        for (int x = pos.x; x < pos.x + item.Width; x++)
            for (int y = pos.y; y < pos.y + item.Height; y++)
                _grid[x, y] = item;

        item.gridPosition = pos;
    }

    private void ClearCells(InventoryItem item)
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_grid[x, y] == item)
                    _grid[x, y] = null;
    }

    private bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;
}
