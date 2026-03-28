using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Цель")]
    public Transform target;

    [Header("Позиция камеры")]
    public float distance = 5f;
    public float height = 1f;

    [Range(-180f, 180f)] public float minVerticalAngle = -30f;
    [Range(-180f, 180f)] public float maxVerticalAngle = 90f;

    [Range(0.01f, 2f)] public float normalSensitivity = 2f;
    [Range(0.01f, 2f)] public float aimSensitivity = 0.2f;

    public LayerMask collisionLayers = -1;
    public float minDistance = 1.5f;
    public float sphereCastRadius = 0.3f;

    public float recoilRecoverySpeed = 8f;

    private float yaw;
    private float pitch;
    private bool isAiming = false;
    private bool isPaused = false;
    private float mouseSensitivity;

    private Echo_Imput controls;
    private Transform aimCameraTransform;
    private Vector2 currentRecoil = Vector2.zero;

    void Awake()
    {
        controls = new Echo_Imput();
        controls.Player.Look.performed += ctx => OnLook(ctx.ReadValue<Vector2>());
        aimCameraTransform = transform.Find("AimCamera");
        mouseSensitivity = normalSensitivity;
    }

    void OnEnable()
    {
        controls.Enable();
        SetGameInputActive(true);
    }

    void OnDisable()
    {
        SetGameInputActive(false);
    }

    // ⬇️ КЛЮЧЕВОЙ МЕТОД: ВКЛ/ВЫКЛ ВВОД
    public void SetGameInputActive(bool active)
    {
        isPaused = !active;
        if (active)
        {
            controls.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            controls.Disable(); // 🔥 ПОЛНОСТЬЮ ОТКЛЮЧАЕМ INPUT SYSTEM
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Вызывается извне (например, из PauseMenu)
    public void TogglePause()
    {
        SetGameInputActive(isPaused); // переключаем
    }

    void Update()
    {
        // Проверяем состояние инвентаря
        bool inventoryOpen = InventorySystem.Instance != null && InventorySystem.Instance.IsOpen;
        
        // Обработка ESC напрямую через Keyboard (независимо от Input Actions)
        // Не реагируем на ESC если инвентарь открыт
        if (!isPaused && !inventoryOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetGameInputActive(false);
        }

        if (isPaused)
        {
            // Выходим из паузы только если инвентарь закрыт
            // (иначе клик по UI инвентаря снимал бы паузу камеры)
            if (!inventoryOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetGameInputActive(true);
            }
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isAiming = true;
            mouseSensitivity = aimSensitivity;
        }
        else if (!Mouse.current.rightButton.isPressed && isAiming)
        {
            isAiming = false;
            mouseSensitivity = normalSensitivity;
        }

        UpdateRecoil();
    }

    void OnLook(Vector2 lookInput)
    {
        // Даже если вызовется — игнорируем в паузе
        if (isPaused) return;

        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }

    void UpdateRecoil()
    {
        if (currentRecoil.sqrMagnitude > 0.0001f)
        {
            currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, recoilRecoverySpeed * Time.deltaTime);
            if (currentRecoil.sqrMagnitude < 0.0001f)
                currentRecoil = Vector2.zero;
        }
    }

    public void ApplyRecoil(float vertical, float horizontal)
    {
        if (isPaused) return;
        currentRecoil += new Vector2(horizontal, vertical);
    }

    void LateUpdate()
    {
        if (isPaused || target == null) return;

        float finalYaw = yaw + currentRecoil.x;
        float finalPitch = pitch - currentRecoil.y;

        Quaternion rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
        Vector3 desiredPosition = target.position - rotation * Vector3.forward * distance + Vector3.up * height;

        Vector3 dir = (desiredPosition - target.position).normalized;
        float dist = Vector3.Distance(target.position, desiredPosition);

        if (Physics.SphereCast(target.position, sphereCastRadius, dir, out RaycastHit hit, dist, collisionLayers))
        {
            float finalDist = Mathf.Max(hit.distance - sphereCastRadius, minDistance);
            transform.position = target.position + dir * finalDist + Vector3.up * height;
        }
        else
        {
            transform.position = target.position + dir * Mathf.Max(distance, minDistance) + Vector3.up * height;
        }

        transform.LookAt(target);

        if (aimCameraTransform != null)
            aimCameraTransform.rotation = transform.rotation;
    }

    public bool IsAiming() => isAiming;
    public bool IsPaused() => isPaused;

    // ⬇️ ОБЯЗАТЕЛЬНО: добавьте этот метод!
    public float GetYawAngle() => yaw;  // ← ЭТО УСТРАНЯЕТ ПЕРВУЮ ОШИБКУ
    public float GetPitchAngle() => pitch;
}