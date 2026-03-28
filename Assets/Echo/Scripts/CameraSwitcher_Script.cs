using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Основная камера")]
    public Camera mainCamera;

    [Header("Прицельная камера")]
    public Camera aimCamera;

    [Header("Настройки прицеливания")]
    public float aimFOV = 45f;
    public float normalFOV = 60f; // можно задать в инспекторе
    public float aimTransitionSpeed = 10f;
    public bool smoothTransition = true;

    private bool isAiming = false;
    private float currentFOV;

    private Echo_Imput controls;

    void Awake()
    {
        controls = new Echo_Imput();
    }

    void Start()
    {
        if (mainCamera == null) mainCamera = GetComponent<Camera>();
        
        // Пытаемся найти AimCamera: сначала по ссылке, потом по имени, потом по тегу
        if (aimCamera == null)
        {
            Transform child = transform.Find("AimCamera");
            if (child != null) aimCamera = child.GetComponent<Camera>();
        }
        if (aimCamera == null)
        {
            GameObject aimCamByTag = GameObject.FindWithTag("AimCamera");
            if (aimCamByTag != null)
            {
                aimCamera = aimCamByTag.GetComponent<Camera>();
            }
        }
        
        if (aimCamera == null) Debug.LogError("AimCamera not found!");

        normalFOV = mainCamera ? mainCamera.fieldOfView : normalFOV;
        currentFOV = normalFOV;

        // Изначально активна только основная камера
        if (mainCamera != null) mainCamera.enabled = true;
        if (aimCamera != null) aimCamera.enabled = false;
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Update()
    {
        // Проверяем состояние инвентаря - если открыт, не реагируем на ввод
        bool inventoryOpen = InventorySystem.Instance != null && InventorySystem.Instance.IsOpen;
        
        // Проверка нажатия ПКМ через Input System или Mouse API
        if (!inventoryOpen && Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                StartAiming();
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                StopAiming();
            }
        }

        // Обновление FOV
        float targetFOV = isAiming ? aimFOV : normalFOV;
        if (smoothTransition)
        {
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, aimTransitionSpeed * Time.deltaTime);
        }
        else
        {
            currentFOV = targetFOV;
        }

        if (mainCamera != null) mainCamera.fieldOfView = currentFOV;
        if (aimCamera != null) aimCamera.fieldOfView = currentFOV;

        // Управление активностью камер
        if (mainCamera != null) mainCamera.enabled = !isAiming;
        if (aimCamera != null) aimCamera.enabled = isAiming;
    }

    void StartAiming()
    {
        isAiming = true;
    }

    void StopAiming()
    {
        isAiming = false;
    }

    // 🔑 Ключевой метод: состояние определяется по активной камере
    public bool IsAiming()
    {
        return aimCamera != null && aimCamera.enabled;
    }

    /// <summary>
    /// Принудительно устанавливает состояние прицеливания (для сброса при закрытии инвентаря)
    /// </summary>
    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
    }

    void OnDestroy()
    {
        controls?.Dispose();
    }
}