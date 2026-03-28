using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    public Weapon weaponPrefab;  // Ссылка на префаб с компонентом Weapon
    public float pickupRange = 2f;
    public KeyCode pickupKey = KeyCode.E;
    
    private bool playerInRange = false;
    private PlayerWeaponHolder playerInventory;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerInventory = other.GetComponent<PlayerWeaponHolder>();
            // Показать UI "Нажмите E для подбора"
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerInventory = null;
            // Скрыть UI
        }
    }
    
    private void Update()
    {
        if (playerInRange && playerInventory != null && Input.GetKeyDown(pickupKey))
        {
            // Создаем экземпляр оружия из префаба
            Weapon newWeapon = Instantiate(weaponPrefab);
            
            // Передаем игроку
            playerInventory.PickupWeapon(newWeapon);
            
            // Уничтожаем объект на земле
            Destroy(gameObject);
        }
    }
}