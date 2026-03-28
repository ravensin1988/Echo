using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemSO : ScriptableObject
{
    [Header("Основная информация")]
    public string itemName;
    public Sprite icon;
    public ItemType itemType;
    public GameObject worldPrefab;
    
    [Header("Инвентарь - Размер")]
    [Tooltip("Ширина предмета в ячейках")]
    [Min(1)] public int sizeX = 1;
    [Tooltip("Высота предмета в ячейках")]
    [Min(1)] public int sizeY = 1;
    
    [Header("Стакание")]
    public bool isStackable = false;
    [Tooltip("Максимальное количество в одном стаке (если isStackable = true)")]
    [Min(1)] public int maxStackSize = 1;
    
    [Header("Описание")]
    [TextArea(3, 5)]
    public string description;
    
    /// <summary>
    /// Возвращает Vector2Int размера предмета в ячейках
    /// </summary>
    public Vector2Int Size => new Vector2Int(sizeX, sizeY);
}

public enum ItemType
{
    Weapon,
    Ammo,
    Health,
    Key,
    Quest,
    Misc
}
