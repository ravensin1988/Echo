using UnityEngine;

public class HitscanDebugger : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private KeyCode testKey = KeyCode.F9;
    [SerializeField] private LayerMask testLayers = -1;
    
    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            TestHitscan();
        }
    }
    
    void TestHitscan()
    {
        if (weaponController == null)
        {
            weaponController = FindAnyObjectByType<WeaponController>();
        }
        
        if (weaponController != null && weaponController.WeaponData != null)
        {
            // Тестовый выстрел
            Transform firePoint = weaponController.HipFirePoint ?? weaponController.transform;
            Vector3 origin = firePoint.position;
            Vector3 direction = weaponController.transform.forward;
            
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, 1000f, testLayers, QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"Test Hitscan HIT: {hit.collider.name} at {hit.distance:F1}m");
                Debug.DrawLine(origin, hit.point, Color.red, 2f);
                Debug.DrawRay(hit.point, hit.normal, Color.green, 2f);
                
                // Проверка IDamageable
                var damageable = hit.collider.GetComponent<IDamageable>();
                Debug.Log($"Has IDamageable: {damageable != null}");
            }
            else
            {
                Debug.Log("Test Hitscan MISS");
                Debug.DrawRay(origin, direction * 1000f, Color.yellow, 2f);
            }
            
            // Лог информации о оружии
            Debug.Log($"Weapon Mode: {weaponController.WeaponData.bulletMode}");
            Debug.Log($"Target Layers: {weaponController.WeaponData.targetLayers.value}");
        }
        else
        {
            Debug.LogWarning("WeaponController не найден!");
        }
    }
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Визуализация лучей в реальном времени
        if (weaponController != null && weaponController.WeaponData != null)
        {
            if (weaponController.WeaponData.bulletMode == Bullet.BulletMode.Hitscan)
            {
                Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
                Transform firePoint = weaponController.HipFirePoint ?? weaponController.transform;
                
                if (weaponController.DebugTarget != null)
                {
                    Vector3 direction = (weaponController.DebugTarget.position - firePoint.position).normalized;
                    Gizmos.DrawRay(firePoint.position, direction * 100f);
                }
                else
                {
                    Gizmos.DrawRay(firePoint.position, weaponController.transform.forward * 100f);
                }
            }
        }
    }
    
    void OnGUI()
    {
        if (weaponController == null) return;
        
        GUI.color = Color.white;
        GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
        
        string debugText = "HITSCAN DEBUGGER\n";
        debugText += "=================\n";
        
        if (weaponController.WeaponData != null)
        {
            debugText += $"Mode: {weaponController.WeaponData.bulletMode}\n";
            debugText += $"Layers: {weaponController.WeaponData.targetLayers.value}\n";
        }
        
        debugText += $"Fire Point: {(weaponController.HipFirePoint != null ? "Set" : "Null")}\n";
        debugText += $"Debug Target: {(weaponController.DebugTarget != null ? "Set" : "Null")}\n";
        debugText += $"Current State: {weaponController.CurrentMovementState}\n";
        debugText += $"Current Spread: {weaponController.CurrentSpreadValue:F3}\n";
        
        GUI.Box(new Rect(10, 100, 250, 150), debugText);
    }
}