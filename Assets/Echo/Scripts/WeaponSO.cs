using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponSO : ScriptableObject
{
    [Header("Эффекты выстрела")]
    [Tooltip("Ключ эффекта выстрела из VFXPool (оставьте пустым если не нужен)")]
    public string fireEffectKey = "muzzle_flash";

    [Header("Эффекты попадания")]
    public GameObject defaultHitEffect;
    public GameObject woodHitEffect;
    public GameObject concreteHitEffect;
    public GameObject metalHitEffect;
    public GameObject waterHitEffect;

    public bool isAutomatic = true;
    public Bullet.BulletMode bulletMode = Bullet.BulletMode.Projectile;

    public string weaponName;
    public float fireRate = 10f;
    public int magazineSize = 30;
    public int totalAmmo = 90;
    public float reloadTime = 2f;
    public float maxDistance = 100f;

    public float damageFalloffStart = 20f;
    public float damageFalloffEnd = 50f;
    public float minDamagePercent = 0.1f;

    public GameObject bulletPrefab;
    public float bulletSpeed = 100f;
    public float damage = 10f;
    public LayerMask targetLayers = -1;

    public float cameraShakeMagnitude = 0.2f;
    public float cameraShakeDuration = 0.1f;

    public float recoilVertical = 1.5f;
    public float recoilVerticalRandom = 0.5f;
    public float recoilHorizontal = 0.3f;
    public float recoilHorizontalRandom = 0.2f;
    public float recoilRecoverySpeed = 8f;

    [Header("Разброс в зависимости от состояния")]
    [Tooltip("Разброс стоя на месте")]
    [Range(0f, 2f)] public float idleSpread = 0.2f;
    
    [Tooltip("Разброс при ходьбе")]
    [Range(0f, 3f)] public float walkingSpread = 0.3f;
    
    [Tooltip("Разброс при беге")]
    [Range(0f, 5f)] public float runningSpread = 0.8f;
    
    [Tooltip("Разброс в прыжке")]
    [Range(0f, 10f)] public float jumpingSpread = 1.5f;
    
    [Tooltip("Разброс в приседе")]
    [Range(0f, 1f)] public float crouchingSpread = 0.1f;
    
    [Tooltip("Увеличение разброса за каждый выстрел")]
    [Range(0f, 0.5f)] public float spreadPerShot = 0.05f;
    
    [Tooltip("Скорость восстановления разброса")]
    [Range(1f, 30f)] public float spreadRecovery = 15f;
    
    [Tooltip("Разрешена ли стрельба во время бега")]
    public bool allowShootingWhileRunning = false;
    
    [Tooltip("Разрешена ли стрельба в прыжке")]
    public bool allowShootingWhileJumping = true;

    [Header("Подбираемый префаб")]
    [Tooltip("Префаб с PickupItem для выбрасывания из инвентаря")]
    public GameObject pickupPrefab;
}
