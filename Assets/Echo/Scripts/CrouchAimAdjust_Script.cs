using UnityEngine;
using UnityEngine.InputSystem;

public class CrouchAimAdjust : MonoBehaviour
{
    [Header("Настройки приседания")]
    public float crouchOffset = 0.5f; // На сколько опускать вниз
    public float crouchSpeed = 5f; // Скорость опускания
    public bool useSmoothTransition = true; // Плавное движение

    [Header("Ссылки")]
    public Transform targetToMove; // Объект, который нужно двигать (если null - используется этот объект)

    private Echo_Imput controls;
    private bool isCrouching = false;
    private Vector3 initialLocalPosition;
    private Vector3 targetLocalPosition;

    void Start()
    {
        // Определяем объект для движения
        if (targetToMove == null) targetToMove = transform;

        // Сохраняем начальную позицию
        initialLocalPosition = targetToMove.localPosition;
        targetLocalPosition = initialLocalPosition;
    }

    void OnEnable()
    {
        if (controls == null) controls = new Echo_Imput();
        controls.Enable();

        // Подписываемся на событие приседания
        controls.Player.Crouch.performed += OnCrouchStarted;
        controls.Player.Crouch.canceled += OnCrouchEnded;
    }

    void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.Crouch.performed -= OnCrouchStarted;
            controls.Player.Crouch.canceled -= OnCrouchEnded;
            controls.Disable();
        }
    }

    void OnCrouchStarted(InputAction.CallbackContext context)
    {
        isCrouching = true;
        UpdateTargetPosition();
    }

    void OnCrouchEnded(InputAction.CallbackContext context)
    {
        isCrouching = false;
        UpdateTargetPosition();
    }

    void UpdateTargetPosition()
    {
        if (useSmoothTransition)
        {
            // Плавное движение к целевой позиции
            targetLocalPosition = initialLocalPosition + Vector3.down * (isCrouching ? crouchOffset : 0f);
        }
        else
        {
            // Мгновенное движение
            Vector3 offset = Vector3.down * (isCrouching ? crouchOffset : 0f);
            targetToMove.localPosition = initialLocalPosition + offset;
        }
    }

    void Update()
    {
        if (useSmoothTransition)
        {
            // Плавное движение к целевой позиции
            targetToMove.localPosition = Vector3.Lerp(
                targetToMove.localPosition,
                targetLocalPosition,
                crouchSpeed * Time.deltaTime
            );
        }
    }

    void OnDestroy()
    {
        if (controls != null)
        {
            controls.Dispose();
        }
    }
}