using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCameraController : MonoBehaviour
{
    [Header("References")]
    public Transform pivot;              // Точка вращения
    public Camera mainCamera;            // Основная камера
    public Camera freeCamera;            // Свободная камера

    [Header("Orbit Settings")]
    public float rotationSpeed = 2.0f;
    public float zoomSpeed = 5.0f;
    public float minDistance = 1.5f;
    public float maxDistance = 30.0f;

    private bool isFreeModeActive = false;
    private float currentDistance;
    private float yaw;
    private float pitch;

    private Echo_Imput controls; // ← Используем ваш класс

    private void Awake()
    {
        controls = new Echo_Imput(); // ← Создаём ваш экземпляр

        // Подписка на нажатие Tab
        controls.Camera.ToggleFreeCam.performed += _ => ToggleFreeCamera();
    }

    private void OnEnable()
    {
        if (pivot == null || mainCamera == null || freeCamera == null)
        {
            Debug.LogError("FreeCameraController: Missing references!", this);
            enabled = false;
            return;
        }

        mainCamera.enabled = true;
        freeCamera.enabled = false;
    }

    private void Update()
    {
        if (isFreeModeActive)
        {
            HandleFreeCamera();
        }
    }

    private void ToggleFreeCamera()
    {
        isFreeModeActive = !isFreeModeActive;

        if (isFreeModeActive)
        {
            controls.Camera.Enable();

            // Инициализация позиции и углов
            Vector3 dir = (freeCamera.transform.position - pivot.position).normalized;
            currentDistance = Vector3.Distance(freeCamera.transform.position, pivot.position);
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
        else
        {
            controls.Camera.Disable();
        }

        mainCamera.enabled = !isFreeModeActive;
        freeCamera.enabled = isFreeModeActive;
    }

    private void HandleFreeCamera()
    {
        // Вращение
        Vector2 rot = controls.Camera.Rotate.ReadValue<Vector2>();
        yaw += rot.x * rotationSpeed;
        pitch -= rot.y * rotationSpeed;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        // Зум
        Vector2 scroll = controls.Camera.Zoom.ReadValue<Vector2>();
        currentDistance -= scroll.y * zoomSpeed;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        // Позиционирование
        Quaternion rotQuat = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotQuat * Vector3.forward * -currentDistance;
        freeCamera.transform.position = pivot.position + offset;
        freeCamera.transform.LookAt(pivot);
    }

    private void OnDisable()
    {
        controls?.Camera.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}