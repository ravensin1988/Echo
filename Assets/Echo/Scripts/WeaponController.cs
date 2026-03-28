#if !UNITY_EDITOR
#define DISABLE_DEBUG
#endif

using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
    private static readonly Vector3 ViewportCenter = new(0.5f, 0.5f, 0f);
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int ReloadSpeedHash = Animator.StringToHash("ReloadSpeed");

    [Header("Настройки")]
    public WeaponSO weaponData;
    public Transform hipFirePoint;
    public Transform aimFirePoint;
    
    [HideInInspector]
    public Camera activeCamera;
    
    [HideInInspector]
    public CameraController cameraController;
    
    [HideInInspector]
    public CameraSwitcher cameraSwitcher;
    
    [HideInInspector]
    public Animator playerAnimator;
    
    public TracerRenderer tracerRenderer;
    
    [HideInInspector]
    public PlayerController_TPS playerController;

    [HideInInspector]
    [SerializeField] private Transform debugTarget;
    private string debugTargetTag = "DebugTarget";
    private static Transform cachedDebugTarget;
    private static bool isDebugTargetSearched = false;

#if UNITY_EDITOR
    private void Reset()
    {
        FindDebugTarget();
    }
#endif

    private void FindDebugTarget()
    {
        if (cachedDebugTarget != null)
        {
            debugTarget = cachedDebugTarget;
            return;
        }

        if (isDebugTargetSearched)
            return;

        GameObject target = GameObject.FindGameObjectWithTag(debugTargetTag);

        if (target != null)
        {
            cachedDebugTarget = target.transform;
            debugTarget = cachedDebugTarget;
            Debug.Log($"[WeaponController] Debug Target найден: {target.name}");
        }
        else
        {
            Debug.LogWarning($"[WeaponController] Debug Target с тегом '{debugTargetTag}' не найден в сцене!");
        }

        isDebugTargetSearched = true;
    }

    public static void ResetDebugTargetCache()
    {
        cachedDebugTarget = null;
        isDebugTargetSearched = false;
    }

    [Header("Декали")]
    [SerializeField] private bool enableDecals = true;

    [Header("Визуализация траектории")]
    public BulletTrajectoryVisualizer trajectoryVisualizer;

    [Header("Настройки движения")]
    [SerializeField] private float movementThreshold = 0.2f;
    [SerializeField] private float stateCheckInterval = 0.1f;

    private float nextStateCheckTime;

    // Входные данные
    private Echo_Imput controls;

    // Состояние оружия
    private float nextFireTime;
    private int currentAmmo;
    private bool isReloading;
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");

    // Оптимизация - кешированные компоненты
    private Transform cachedTransform;
    private CharacterController cachedCharacterController;
    private Transform cachedCameraTransform;
    private float reloadEndTime;
    private string cachedStateString = MovementState.Idle.ToString();

    // Оптимизация - кешированные синглтоны
    private BulletPool cachedBulletPool;
    private CameraShake cachedCameraShake;
    private VFXPool cachedVFXPool;
    private BulletDecal cachedBulletDecal;

    // Кешированное состояние для оптимизации
    private bool cachedIsAiming;
    private bool isAimingValid;

    // Система разброса
    private float currentSpread = 0f;
    private float baseSpreadForCurrentState = 0f;

    // Текущее состояние
    public enum MovementState { Idle, Walking, Running, Crouching, Jumping }
    private MovementState currentState = MovementState.Idle;

    // Публичные свойства для доступа
    public WeaponSO WeaponData => weaponData;
    public Transform HipFirePoint => hipFirePoint;
    public Transform AimFirePoint => aimFirePoint;
    public Transform DebugTarget => debugTarget;
    public Camera ActiveCamera => activeCamera;
    public bool IsAiming => GetAimingState();
    public MovementState CurrentMovementState => currentState;
    public float CurrentSpreadValue => currentSpread;

    // Pre-allocated arrays for raycasting
    private static readonly RaycastHit[] raycastHits = new RaycastHit[1];

    // Constants for optimization
    private const float MinSpreadThreshold = 0.001f;
    private const float SpreadNearBaseThreshold = 0.01f;
    private const float MovementSqrThreshold = 0.0001f; // 0.01f squared

    void Awake()
    {
        cachedTransform = transform;
        controls = new Echo_Imput();

        if (playerController != null)
        {
            cachedCharacterController = playerController.GetComponent<CharacterController>();
        }
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Shoot.performed += OnShoot;
    }

    void OnDisable()
    {
        controls.Player.Shoot.performed -= OnShoot;
        controls.Player.Disable();
    }

    void Start()
    {
        GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        // Автоматический поиск камеры по тегу MainCamera
        if (activeCamera == null)
        {
            if (mainCameraObj != null)
            {
                activeCamera = mainCameraObj.GetComponent<Camera>();
            }
        }

        // Fallback на Camera.main если тег не найден
        if (activeCamera == null) activeCamera = Camera.main;
        if (activeCamera != null) cachedCameraTransform = activeCamera.transform;

        // Автоматический поиск CameraController по тегу MainCamera
        if (cameraController == null)
        {
            if (mainCameraObj != null)
            {
                cameraController = mainCameraObj.GetComponent<CameraController>();
            }
        }

        // Автоматический поиск CameraSwitcher по тегу MainCamera
        if (cameraSwitcher == null)
        {
            if (mainCameraObj != null)
            {
                cameraSwitcher = mainCameraObj.GetComponent<CameraSwitcher>();
            }
        }

        // Автоматический поиск PlayerAnimator по тегу Player
        if (playerAnimator == null)
        {
            if (playerObj != null)
            {
                playerAnimator = playerObj.GetComponent<Animator>();
            }
        }

        // Автоматический поиск PlayerController по тегу Player
        if (playerController == null)
        {
            if (playerObj != null)
            {
                playerController = playerObj.GetComponent<PlayerController_TPS>();
            }

            // Кешируем CharacterController после поиска PlayerController
            if (playerController != null)
            {
                cachedCharacterController = playerController.GetComponent<CharacterController>();
            }
        }

        // Кешируем синглтоны
        cachedBulletPool = BulletPool.Instance;
        cachedCameraShake = CameraShake.Instance;
        cachedVFXPool = VFXPool.Instance;
        cachedBulletDecal = BulletDecal.Instance;

        // Автоматический поиск DebugTarget по тегу
        FindDebugTarget();

        currentAmmo = weaponData.magazineSize;
        currentSpread = weaponData.idleSpread;
        baseSpreadForCurrentState = weaponData.idleSpread;
    }

    void Update()
    {
        if (isReloading && Time.time >= reloadEndTime)
        {
            CompleteReload();
        }

        // Кешируем состояние прицеливания для этого кадра
        cachedIsAiming = GetAimingState();
        isAimingValid = true;

        if (Time.time > nextStateCheckTime)
        {
            DetermineMovementState();
            nextStateCheckTime = Time.time + stateCheckInterval;
        }

        HandleInput();
        UpdateSpread();
    }

    void DetermineMovementState()
    {
        if (playerController == null)
        {
            currentState = MovementState.Idle;
            return;
        }

        MovementState newState;

        // Приоритеты: Прыжок > Присед > Бег > Ходьба > Стояние
        if (IsPlayerJumping())
        {
            newState = MovementState.Jumping;
        }
        else if (IsPlayerCrouching())
        {
            newState = MovementState.Crouching;
        }
        else if (IsPlayerRunning())
        {
            newState = MovementState.Running;
        }
        else if (IsPlayerMoving())
        {
            newState = MovementState.Walking;
        }
        else
        {
            newState = MovementState.Idle;
        }

        if (newState != currentState)
        {
            currentState = newState;
            cachedStateString = currentState.ToString();
            UpdateBaseSpreadForState();
        }
    }

    void UpdateBaseSpreadForState()
    {
        baseSpreadForCurrentState = currentState switch
        {
            MovementState.Jumping => weaponData.jumpingSpread,
            MovementState.Running => weaponData.runningSpread,
            MovementState.Crouching => weaponData.crouchingSpread,
            MovementState.Walking => weaponData.walkingSpread,
            _ => weaponData.idleSpread
        };
    }

    void OnShoot(InputAction.CallbackContext context)
    {
        if (weaponData.isAutomatic) return;

        if (CanFire())
        {
            Fire(GetAimingState());
        }
    }

    void HandleInput()
    {
        bool isAiming = GetAimingState();

        if (weaponData.isAutomatic && controls.Player.Shoot.IsPressed())
        {
            if (CanFire())
            {
                Fire(isAiming);
            }
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame &&
            !isReloading && currentAmmo < weaponData.magazineSize)
        {
            Reload();
        }
    }

    bool GetAimingState()
    {
        // Проверяем инвентарь - если открыт, не позволяем прицеливаться
        if (InventorySystem.Instance != null && InventorySystem.Instance.IsOpen)
            return false;
            
        if (cameraController != null) return cameraController.IsAiming();
        if (cameraSwitcher != null) return cameraSwitcher.IsAiming();
        return false; // Не проверяем мышь напрямую, полагаемся на контроллеры
    }

    bool CanFire()
    {
        if (isReloading) return false;
        if (Time.time < nextFireTime) return false;
        if (currentAmmo <= 0) return false;

        return CheckPlayerStateForShooting();
    }

    bool CheckPlayerStateForShooting()
    {
        if (playerController == null) return true;

        if (currentState == MovementState.Running && !weaponData.allowShootingWhileRunning)
        {
            return false;
        }

        if (currentState == MovementState.Jumping && !weaponData.allowShootingWhileJumping)
        {
            return false;
        }

        return true;
    }

    bool IsPlayerRunning()
    {
        if (playerController == null) return false;

        if (playerAnimator != null && playerAnimator.GetBool(IsRunningHash))
        {
            return true;
        }

        bool isSprinting = controls.Player.Sprint.IsPressed();
        bool isMoving = IsPlayerMoving();

        return isSprinting && isMoving;
    }

    bool IsPlayerJumping()
    {
        if (playerController == null || playerAnimator == null) return false;

        return playerAnimator.GetBool(IsJumpingHash) || playerAnimator.GetBool(IsFallingHash);
    }

    bool IsPlayerCrouching()
    {
        if (playerController == null) return false;

        if (playerAnimator != null)
        {
            return playerAnimator.GetBool(IsCrouchingHash);
        }

        return controls.Player.Crouch.IsPressed();
    }

    bool IsPlayerMoving()
    {
        if (playerController == null) return false;

        if (cachedCharacterController != null)
        {
            Vector3 vel = cachedCharacterController.velocity;
            vel.y = 0f;
            return vel.sqrMagnitude > movementThreshold * movementThreshold;
        }

        Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();
        return moveInput.sqrMagnitude > 0.01f;
    }

    Transform GetCurrentFirePoint(bool isAiming)
    {
        if (isAiming && aimFirePoint != null)
            return aimFirePoint;

        return hipFirePoint != null ? hipFirePoint : cachedTransform;
    }

    void Fire(bool isAiming)
    {
        if (!CanFire())
        {
            // Для продакшена используем проверку по центру экрана, если нет debugTarget
            if (debugTarget == null && activeCamera != null)
            {
                // Стрельба по центру экрана будет обработана ниже
            }
            else if (debugTarget == null)
            {
                return;
            }
        }

        // Динамическая корректировка скорострельности при низком FPS
        float adjustedFireRate = weaponData.fireRate;
        if (Time.deltaTime > 0.025f) // FPS < 40
        {
            adjustedFireRate *= 0.7f; // Уменьшаем RoF на 30%
        }

        nextFireTime = Time.time + 1f / adjustedFireRate;
        currentAmmo--;

        Transform firePoint = GetCurrentFirePoint(isAiming);
        Vector3 origin = firePoint.position;

        // Спавн эффекта выстрела (muzzle flash)
        SpawnFireEffect(firePoint);

        // Определяем направление выстрела
        Vector3 direction;
        if (debugTarget != null)
        {
            direction = (debugTarget.position - origin).normalized;
        }
        else if (activeCamera != null)
        {
            // Стрельба по центру экрана
            Ray ray = activeCamera.ViewportPointToRay(ViewportCenter);
            direction = ray.direction;
        }
        else
        {
            direction = firePoint.forward;
        }

        Vector3 spreadDirection = GetSpreadDirection(direction, currentSpread);

        if (weaponData.bulletMode == Bullet.BulletMode.Projectile)
        {
            SpawnProjectile(origin, spreadDirection);
        }
        else
        {
            PerformHitscanShot(origin, spreadDirection);
        }

        ApplyRecoil();
        ApplyCameraShake();

        currentSpread += weaponData.spreadPerShot;

        if (trajectoryVisualizer != null)
        {
            float maxDistance = Mathf.Max(weaponData.damageFalloffEnd, 1000f);

            if (Physics.Raycast(origin, spreadDirection, out RaycastHit hit, maxDistance, weaponData.targetLayers))
            {
                trajectoryVisualizer.ShowShotTrajectory(origin, hit.point, currentSpread);
            }
            else
            {
                trajectoryVisualizer.ShowShotTrajectory(origin, origin + spreadDirection * maxDistance, currentSpread);
            }
        }
    }

    void UpdateSpread()
    {
        currentSpread = Mathf.Lerp(currentSpread, baseSpreadForCurrentState,
                                 weaponData.spreadRecovery * Time.deltaTime);

        if (Mathf.Abs(currentSpread - baseSpreadForCurrentState) < 0.01f)
        {
            currentSpread = baseSpreadForCurrentState;
        }
    }

    private Vector3 GetSpreadDirection(Vector3 baseDirection, float spreadAmount)
    {
        if (spreadAmount <= MinSpreadThreshold) return baseDirection;

        if (cachedCameraTransform == null && activeCamera != null)
        {
            cachedCameraTransform = activeCamera.transform;
        }

        Vector2 randomCircle = Random.insideUnitCircle * spreadAmount;

        Vector3 right = cachedCameraTransform != null ? cachedCameraTransform.right : Vector3.right;
        Vector3 up = cachedCameraTransform != null ? cachedCameraTransform.up : Vector3.up;

        Vector3 result = baseDirection;
        result.x += right.x * randomCircle.x + up.x * randomCircle.y;
        result.y += right.y * randomCircle.x + up.y * randomCircle.y;
        result.z += right.z * randomCircle.x + up.z * randomCircle.y;

        return result.normalized;
    }

    void SpawnProjectile(Vector3 origin, Vector3 direction)
    {
        if (cachedBulletPool == null) return;

        Bullet bullet = cachedBulletPool.GetBullet();
        if (bullet == null) return;

        bullet.transform.position = origin;
        bullet.Initialize(
            dir: direction,
            bulletSpeed: weaponData.bulletSpeed,
            layers: weaponData.targetLayers,
            damage: weaponData.damage,
            startFalloff: weaponData.damageFalloffStart,
            endFalloff: weaponData.damageFalloffEnd,
            minDmgPercent: weaponData.minDamagePercent,
            bulletMode: weaponData.bulletMode,
            customMaxDistance: weaponData.maxDistance > 0 ? weaponData.maxDistance : -1f
        );
    }

    void PerformHitscanShot(Vector3 origin, Vector3 direction)
    {
        float maxDistance = Mathf.Max(weaponData.damageFalloffEnd, 1000f);

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastHits,
            maxDistance,
            weaponData.targetLayers,
            QueryTriggerInteraction.Ignore
        );

        Vector3 endPoint;

        if (hitCount > 0)
        {
            RaycastHit hit = raycastHits[0];
            endPoint = hit.point;

            float distance = Vector3.Distance(origin, hit.point);
            float finalDamage = CalculateDamage(
                weaponData.damage, distance,
                weaponData.damageFalloffStart,
                weaponData.damageFalloffEnd,
                weaponData.minDamagePercent
            );

            // Физическая реакция
            if (hit.collider.TryGetComponent(out PhysicsReactObject physicsReact))
            {
                float force = Mathf.Lerp(weaponData.damage * 5f, weaponData.damage,
                                       Mathf.InverseLerp(0, 100f, hit.distance));
                physicsReact.ReactToBulletHit(hit.point, direction, force);
            }

            ProcessDamage(hit.collider, finalDamage);

            // Декали
            if (enableDecals && cachedBulletDecal != null)
            {
                Transform parentSurface = hit.collider.transform;
                if (hit.collider.attachedRigidbody != null)
                {
                    parentSurface = hit.collider.attachedRigidbody.transform;
                }

                cachedBulletDecal.SpawnDecal(
                    hit.point + hit.normal * 0.001f,
                    hit.normal,
                    parentSurface,
                    GetSurfaceMaterial(hit.collider)
                );
            }

            SpawnSurfaceEffect(hit.point, hit.normal, hit.collider);
        }
        else
        {
            endPoint = origin + direction * maxDistance;
        }

        if (tracerRenderer != null)
        {
            tracerRenderer.ShowTracer(origin, endPoint);
        }
    }

    [ContextMenu("Debug Layer Settings")]
    public void DebugLayerSettings()
    {
        if (weaponData == null)
        {
            Debug.LogError("WeaponData не назначен!");
            return;
        }

        Debug.Log($"=== Weapon Layer Debug ===");
        Debug.Log($"Weapon Name: {weaponData.weaponName}");
        Debug.Log($"Bullet Mode: {weaponData.bulletMode}");
        Debug.Log($"Target Layers Mask Value: {weaponData.targetLayers.value}");

        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                int layerMask = 1 << i;
                bool isInMask = (weaponData.targetLayers.value & layerMask) != 0;
                if (isInMask)
                {
                    Debug.Log($"Layer {i} ({layerName}): INCLUDED");
                }
            }
        }

        Debug.Log($"=== End Debug ===");
    }

    [ContextMenu("Test Hitscan Manually")]
    public void TestHitscanManually()
    {
        if (weaponData == null || activeCamera == null)
        {
            Debug.LogError("Не хватает ссылок!");
            return;
        }

        Debug.Log("=== Manual Hitscan Test ===");

        Transform firePoint = GetCurrentFirePoint(IsAiming);
        Vector3 origin = firePoint.position;
        Vector3 direction = activeCamera.transform.forward;

        PerformHitscanShot(origin, direction);

        Debug.Log("=== Test Complete ===");
    }

    private void ProcessDamage(Collider collider, float damage)
    {
        if (collider.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damage);
            return;
        }

        Transform parent = collider.transform.parent;
        while (parent != null)
        {
            if (parent.TryGetComponent(out damageable))
            {
                damageable.TakeDamage(damage);
                return;
            }
            parent = parent.parent;
        }
    }

    private void SpawnSurfaceEffect(Vector3 position, Vector3 normal, Collider collider)
    {
        SurfaceMaterial material = GetSurfaceMaterial(collider);
        string effectKey = GetEffectKeyByMaterial(material);

        if (!string.IsNullOrEmpty(effectKey) && cachedVFXPool != null)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            cachedVFXPool.Get(effectKey, position, rotation);
        }
    }

    private SurfaceMaterial GetSurfaceMaterial(Collider collider)
    {
        // Пытаемся получить компонент непосредственно на коллайдере
        if (collider.TryGetComponent(out SurfaceTypeTag surfaceComp))
        {
            return surfaceComp.surfaceMaterial;
        }

        // Если не нашли, пробуем на родителе (закешировать результаты сложнее, 
        // но это все еще лучше, чем множественные вызовы GetComponent)
        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out surfaceComp))
        {
            return surfaceComp.surfaceMaterial;
        }

        return SurfaceMaterial.Default;
    }

    private string GetEffectKeyByMaterial(SurfaceMaterial material)
    {
        return material switch
        {
            SurfaceMaterial.Wood => "wood",
            SurfaceMaterial.Concrete => "concrete",
            SurfaceMaterial.Metal => "metal",
            SurfaceMaterial.Water => "water",
            _ => "default"
        };
    }

    private float CalculateDamage(float baseDamage, float distance, float falloffStart, float falloffEnd, float minPercent)
    {
        if (distance <= falloffStart) return baseDamage;
        if (distance >= falloffEnd) return baseDamage * minPercent;

        float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
        return Mathf.Lerp(baseDamage, baseDamage * minPercent, t);
    }

    void ApplyRecoil()
    {
        if (cameraController != null)
        {
            float vertical = weaponData.recoilVertical + Random.Range(0f, weaponData.recoilVerticalRandom);
            float horizontal = weaponData.recoilHorizontal + Random.Range(-weaponData.recoilHorizontalRandom, weaponData.recoilHorizontalRandom);
            cameraController.ApplyRecoil(vertical, horizontal);
        }
    }

    void ApplyCameraShake()
    {
        if (cachedCameraShake != null)
        {
            cachedCameraShake.Shake(weaponData.cameraShakeMagnitude, weaponData.cameraShakeDuration);
        }
    }

    private void SpawnFireEffect(Transform firePoint)
    {
        if (cachedVFXPool == null) return;
        if (string.IsNullOrEmpty(weaponData.fireEffectKey)) return;

        // Получаем направление выстрела для правильного поворота эффекта
        Vector3 forward = firePoint.forward;
        Quaternion rotation = Quaternion.LookRotation(forward);
        
        cachedVFXPool.Get(weaponData.fireEffectKey, firePoint.position, rotation);
    }

    void Reload()
    {
        if (isReloading || currentAmmo >= weaponData.magazineSize) return;

        isReloading = true;
        SetupReloadAnimation();
        reloadEndTime = Time.time + weaponData.reloadTime;
    }

    private void SetupReloadAnimation()
    {
        if (playerAnimator != null)
        {
            const float baseAnimDuration = 2f;
            if (weaponData.reloadTime > 0f)
            {
                float speed = baseAnimDuration / weaponData.reloadTime;
                playerAnimator.SetFloat(ReloadSpeedHash, speed);
            }
            playerAnimator.SetBool(IsReloadingHash, true);
        }
    }

    private void CompleteReload()
    {
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        currentSpread = baseSpreadForCurrentState;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool(IsReloadingHash, false);
        }
    }

    // Публичные методы для UI
    /// <summary>
    /// Возвращает <see langword="true"/>, если в магазине есть патроны.
    /// </summary>
    public bool HasAmmo() => currentAmmo > 0;

    /// <summary>
    /// Возвращает <see langword="true"/>, если сейчас выполняется перезарядка.
    /// </summary>
    public bool IsReloading() => isReloading;

    /// <summary>
    /// Возвращает текущее количество патронов в магазине.
    /// </summary>
    public int GetCurrentAmmo() => currentAmmo;

    /// <summary>
    /// Возвращает ёмкость магазина текущего оружия.
    /// </summary>
    public int GetMagazineSize() => weaponData.magazineSize;

    /// <summary>
    /// Возвращает текущий разброс выстрела.
    /// </summary>
    public float GetCurrentSpread() => currentSpread;

    /// <summary>
    /// Возвращает кешированное строковое представление состояния движения.
    /// </summary>
    public string GetCurrentStateString() => cachedStateString;

    /// <summary>
    /// Возвращает нормализованный процент текущего разброса относительно ожидаемого максимума.
    /// </summary>
    public float GetSpreadPercentage()
    {
        float maxSpread = Mathf.Max(
            weaponData.idleSpread,
            weaponData.walkingSpread,
            weaponData.runningSpread,
            weaponData.jumpingSpread,
            weaponData.crouchingSpread
        );

        maxSpread += weaponData.spreadPerShot * 5f;

        return Mathf.Clamp01(currentSpread / maxSpread);
    }
}
