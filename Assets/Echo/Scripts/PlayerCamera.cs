using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [Header("Цель (голова/тело игрока)")]
    public Transform target;

    [Header("Камеры")]
    public Camera mainCamera;   // от бедра
    public Camera aimCamera;    // прицельная

    [Header("Чувствительность")]
    [Range(0.01f, 2f)] public float normalSensitivity = 2f;
    [Range(0.01f, 2f)] public float aimSensitivity = 0.5f;

    [Header("Позиция (только для mainCamera)")]
    public float distance = 5f;
    public float height = 1f;
    public LayerMask collisionLayers = -1;
    public float minDistance = 1.5f;
    public float sphereCastRadius = 0.3f;

    [Header("Ограничения угла")]
    [Range(-90f, 90f)] public float minVerticalAngle = -30f;
    [Range(-90f, 90f)] public float maxVerticalAngle = 70f;

    [Header("Прицеливание")]
    public float aimFOV = 45f;
    public float normalFOV = 60f;
    public float aimTransitionSpeed = 10f;
    public bool smoothFOV = true;

    [Header("Отдача")]
    public float recoilRecoverySpeed = 8f;

    // Внутреннее состояние
    private float yaw;
    private float pitch;
    private bool isAiming = false;
    private bool isPaused = false;
    private float currentFOV;
    private Vector2 currentRecoil = Vector2.zero;

    private Echo_Imput controls;
    private Transform aimCameraTransform;

    void Awake()
    {
        controls = new Echo_Imput();
        controls.Player.Look.performed += ctx => OnLook(ctx.ReadValue<Vector2>());
        controls.Player.Aim.performed += _ => StartAiming();
        controls.Player.Aim.canceled += _ => StopAiming();

        aimCameraTransform = aimCamera ? aimCamera.transform : null;

        // Инициализация камер
        if (mainCamera != null) normalFOV = mainCamera.fieldOfView;
        currentFOV = normalFOV;

        SetCamerasActive(isAiming: false);
        ApplyFOV(normalFOV);
    }

    void OnEnable()
    {
        controls.Enable();
        SetCursorState(true);
    }

    void OnDisable()
    {
        controls.Disable();
        SetCursorState(false);
    }

    void Update()
    {
        // Проверяем состояние инвентаря
        bool inventoryOpen = InventorySystem.Instance != null && InventorySystem.Instance.IsOpen;
        
        // Обработка паузы (ESC) - не реагируем если инвентарь открыт
        if (!isPaused && !inventoryOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetPaused(true);
            return;
        }

        if (isPaused)
        {
            // Выходим из паузы только если инвентарь закрыт
            if (!inventoryOpen && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetPaused(false);
            }
            return;
        }

        UpdateFOV();
        UpdateRecoil();
    }

    void LateUpdate()
    {
        if (isPaused || target == null) return;

        // Обновление поворота
        float finalYaw = yaw + currentRecoil.x;
        float finalPitch = pitch - currentRecoil.y;
        Quaternion rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);

        if (!isAiming)
        {
            // Позиционирование третьего лица
            Vector3 desiredPos = target.position - rotation * Vector3.forward * distance + Vector3.up * height;
            Vector3 dir = (desiredPos - target.position).normalized;
            float dist = Vector3.Distance(target.position, desiredPos);

            if (Physics.SphereCast(target.position, sphereCastRadius, dir, out RaycastHit hit, dist, collisionLayers))
            {
                float finalDist = Mathf.Max(hit.distance - sphereCastRadius, minDistance);
                transform.position = target.position + dir * finalDist + Vector3.up * height;
            }
            else
            {
                transform.position = target.position + dir * Mathf.Max(distance, minDistance) + Vector3.up * height;
            }

            transform.rotation = rotation;
        }
        else
        {
            // Прицеливание: камера следует за поворотом, но остаётся на месте игрока
            transform.position = target.position + Vector3.up * height;
            transform.rotation = rotation;
        }

        // Синхронизация дочерней прицельной камеры (если есть)
        if (aimCameraTransform != null)
            aimCameraTransform.rotation = transform.rotation;
    }

    void OnLook(Vector2 input)
    {
        if (isPaused) return;

        float sens = isAiming ? aimSensitivity : normalSensitivity;
        yaw += input.x * sens;
        pitch -= input.y * sens;
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }

    void StartAiming()
    {
        if (isPaused) return;
        isAiming = true;
    }

    void StopAiming()
    {
        if (isPaused) return;
        isAiming = false;
    }

    void UpdateFOV()
    {
        float targetFOV = isAiming ? aimFOV : normalFOV;
        if (smoothFOV)
        {
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, aimTransitionSpeed * Time.deltaTime);
        }
        else
        {
            currentFOV = targetFOV;
        }
        ApplyFOV(currentFOV);
    }

    void ApplyFOV(float fov)
    {
        if (mainCamera != null) mainCamera.fieldOfView = fov;
        if (aimCamera != null) aimCamera.fieldOfView = fov;
    }

    void SetCamerasActive(bool isAiming)
    {
        if (mainCamera != null) mainCamera.enabled = !isAiming;
        if (aimCamera != null) aimCamera.enabled = isAiming;
    }

    void UpdateRecoil()
    {
        if (currentRecoil.magnitude > 0.01f)
        {
            currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, recoilRecoverySpeed * Time.deltaTime);
        }
        else
        {
            currentRecoil = Vector2.zero;
        }
    }

    public void ApplyRecoil(float vertical, float horizontal)
    {
        if (isPaused) return;
        currentRecoil += new Vector2(horizontal, vertical);
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        SetCursorState(!paused);
        if (paused) StopAiming(); // выходим из прицела при паузе
    }

    void SetCursorState(bool gameMode)
    {
        Cursor.lockState = gameMode ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameMode;
    }

    // === Публичные методы для других скриптов ===
    public bool IsAiming() => isAiming && !isPaused;
    public bool IsPaused() => isPaused;
    public Camera GetActiveCamera() => isAiming ? aimCamera : mainCamera;
    
    /// <summary>
    /// Принудительно устанавливает состояние прицеливания (для сброса при закрытии инвентаря)
    /// </summary>
    public void SetAiming(bool aiming)
    {
        if (aiming)
            StartAiming();
        else
            StopAiming();
    }
}
