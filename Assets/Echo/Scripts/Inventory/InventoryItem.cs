using UnityEngine;

/// <summary>
/// Экземпляр предмета в инвентаре
/// </summary>
[System.Serializable]
public class InventoryItem
{
    public ItemSO itemData;
    public int amount;
    
    // Позиция в сетке инвентаря (верхний левый угол)
    public Vector2Int gridPosition;
    
    // Повёрнут ли предмет (W<->H поменяны местами)
    public bool isRotated;

    public InventoryItem(ItemSO data, int amount, Vector2Int position, bool rotated = false)
    {
        this.itemData = data;
        this.amount = amount;
        this.gridPosition = position;
        this.isRotated = rotated;
    }

    /// <summary>
    /// Ширина предмета с учётом поворота
    /// </summary>
    public int Width => isRotated ? itemData.sizeY : itemData.sizeX;

    /// <summary>
    /// Высота предмета с учётом поворота
    /// </summary>
    public int Height => isRotated ? itemData.sizeX : itemData.sizeY;
    
    /// <summary>
    /// Добавить к стаку. Возвращает остаток, который не поместился.
    /// </summary>
    public int AddToStack(int addAmount)
    {
        if (!itemData.isStackable) return addAmount;
        int canAdd = itemData.maxStackSize - amount;
        int added = Mathf.Min(canAdd, addAmount);
        amount += added;
        return addAmount - added;
    }

    /// <summary>
    /// Занимает ли предмет данную ячейку сетки?
    /// </summary>
    public bool OccupiesCell(int x, int y)
    {
        return x >= gridPosition.x && x < gridPosition.x + Width &&
               y >= gridPosition.y && y < gridPosition.y + Height;
    }
}
