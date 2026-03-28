using UnityEngine;
using System;

public enum WeaponType
{
    None,
    Pistol,
    Rifle,
    Grenade,
    Melee
}

public enum WeaponSlot
{
    HandRight,      // Одноручное в правой руке
    HandLeft,       // Одноручное в левой руке
    HandBoth,       // Двуручное (занимает обе руки)
    Back,           // За спиной
    Hip             // На поясе
}

public class Weapon : MonoBehaviour
{
    [Header("Основные настройки")]
    public WeaponType weaponType = WeaponType.Pistol;
    public WeaponSlot slotType = WeaponSlot.HandRight;
    
    [Header("Состояние")]
    public bool isEquipped = false;        // В руках ли сейчас оружие
    
    // События для оповещения персонажа
    public event Action<Weapon> OnEquipped;
    public event Action<Weapon> OnHolstered;
    
    /// <summary>
    /// Метод для "надевания" оружия на персонажа
    /// </summary>
    public void Equip(Transform newParent, Vector3 localPos, Quaternion localRot)
    {
        transform.SetParent(newParent);
        transform.localPosition = localPos;
        transform.localRotation = localRot;
        isEquipped = true;
        OnEquipped?.Invoke(this);
    }
    
    /// <summary>
    /// Метод для "снятия" оружия в слот
    /// </summary>
    public void Holster(Transform newParent, Vector3 localPos, Quaternion localRot)
    {
        transform.SetParent(newParent);
        transform.localPosition = localPos;
        transform.localRotation = localRot;
        isEquipped = false;
        OnHolstered?.Invoke(this);
    }
    
    /// <summary>
    /// Включение/выключение видимости и коллайдеров
    /// </summary>
    public void SetVisible(bool visible)
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = visible;
        }
        
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = visible;
        }
    }
}
