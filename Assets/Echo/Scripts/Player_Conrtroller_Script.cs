/// скрипт управления персонажем

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController_TPS : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");

    // Плавность ввода для анимаций
    [Header("Animation Smoothing")]
    public float animationSmoothTime = 0.1f;
    private Vector2 smoothedInput;

    [Header("Movement Speeds - Walk")]
    public float walkForward = 6f;
    public float walkBackward = 4f;
    public float strafeLeft = 4f;
    public float strafeRight = 4f;
    public float diagForwardLeft = 5f;
    public float diagForwardRight = 5f;
    public float diagBackwardLeft = 3f;
    public float diagBackwardRight = 3f;

    [Header("Movement Speeds - Run")]
    public float runForward = 10f;
    public float runBackward = 7f;
    public float runStrafeLeft = 6f;
    public float runStrafeRight = 6f;
    public float runDiagForwardLeft = 8f;
    public float runDiagForwardRight = 8f;
    public float runDiagBackwardLeft = 5f;
    public float runDiagBackwardRight = 5f;

    [Header("Movement Speeds - Crouch")]
    public float crouchForward = 3f;
    public float crouchBackward = 2f;
    public float crouchLeft = 2f;
    public float crouchRight = 2f;
    public float crouchDiagForwardLeft = 2.5f;
    public float crouchDiagForwardRight = 2.5f;
    public float crouchDiagBackwardLeft = 1.5f;
    public float crouchDiagBackwardRight = 1.5f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float groundCheckOffset = 0.1f;
    public float fallingThreshold = -1f;

    [Header("Crouch Settings")]
    public float standingHeight = 2f;
    public float crouchingHeight = 1f;
    public float crouchSmoothSpeed = 5f;

    [Header("Camera")]
    public CameraController cameraController;

    [Header("Animation")]
    public Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private bool jumpPressed;
    private bool isGrounded;
    private bool isCrouching;

    // === Прыжок: флаги для синхронизации с анимацией ===
    private bool jumpQueued = false;
    private bool isInJumpPhase = false;

    // === Присед: логика для триггер-зон ===
    private bool isCrouchForced = false;
    private bool wasUserCrouching = false;

    private Echo_Imput controls;
    private float groundCheckTimer = 0f;
    private readonly float groundCheckDelay = 0.1f;
    private bool isEffectivelyGrounded => groundCheckTimer > 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new Echo_Imput();
        controls.Player.Jump.performed += _ => jumpPressed = true;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        HandleGroundCheck();
        HandleJump();
        ApplyGravity();

        // === УПРАВЛЕНИЕ ПРИСЕДОМ ===
        bool userWantsToCrouch = controls.Player.Crouch.IsPressed();
        ManageCrouchState(userWantsToCrouch);

        Vector2 rawInput = controls.Player.Move.ReadValue<Vector2>();
        bool sprintPressed = controls.Player.Sprint.IsPressed();

        Vector2 movementInput = rawInput;
        if (movementInput.magnitude > 1f) movementInput.Normalize();

        float smoothSpeed = 1f / animationSmoothTime;
        smoothedInput.x = Mathf.MoveTowards(smoothedInput.x, rawInput.x, smoothSpeed * Time.deltaTime);
        smoothedInput.y = Mathf.MoveTowards(smoothedInput.y, rawInput.y, smoothSpeed * Time.deltaTime);

        if (cameraController != null)
        {
            float cameraYaw = cameraController.GetYawAngle();
            Quaternion targetRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);

            float speed = CalculateSpeed(movementInput.x, movementInput.y, sprintPressed, isCrouching);
            Vector3 forward = targetRotation * Vector3.forward;
            Vector3 right = targetRotation * Vector3.right;
            Vector3 moveDirection = forward * movementInput.y + right * movementInput.x;
            moveDirection.y = 0f;

            if (moveDirection.magnitude > 1f) moveDirection.Normalize();
            float currentMoveSpeed = moveDirection.magnitude * speed;
            controller.Move(moveDirection * speed * Time.deltaTime + velocity * Time.deltaTime);

            if (animator != null)
            {
                bool isFalling = !isGrounded && velocity.y < fallingThreshold;
                
                // ИСПРАВЛЕНИЕ: Правильная установка параметров анимации
                animator.SetFloat(MoveXHash, smoothedInput.x);
                animator.SetFloat(MoveYHash, smoothedInput.y);
                animator.SetBool(IsCrouchingHash, isCrouching); // Убедитесь, что это правильное значение
                animator.SetBool(IsRunningHash, sprintPressed && !isCrouching && isGrounded);
                animator.SetBool(IsJumpingHash, isInJumpPhase);
                animator.SetBool(IsFallingHash, isFalling);
                animator.SetBool(IsGroundedHash, isGrounded);
                animator.SetFloat(MoveSpeedHash, currentMoveSpeed);
            }
        }
        else
        {
            Vector3 moveDir = new ();
            if (moveDir.magnitude > 1f) moveDir.Normalize();

            float speed = CalculateSpeed(movementInput.x, movementInput.y, sprintPressed, isCrouching);
            controller.Move(moveDir * speed * Time.deltaTime + velocity * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat(MoveXHash, smoothedInput.x);
                animator.SetFloat(MoveYHash, smoothedInput.y);
                animator.SetBool(IsCrouchingHash, isCrouching);
                animator.SetBool(IsRunningHash, sprintPressed && !isCrouching);
            }
        }
    }

    // === ЦЕНТРАЛИЗОВАННОЕ УПРАВЛЕНИЕ ПРИСЕДОМ ===
    void ManageCrouchState(bool userWantsToCrouch)
    {
        if (isCrouchForced)
        {
            // В зоне принудительного приседа — всегда приседаем
            isCrouching = true;
        }
        else
        {
            // Вне зоны — работаем от ввода пользователя
            isCrouching = userWantsToCrouch;
        }

        // Обновляем физику и анимацию
        UpdateCrouchState();
        
        // Убедимся, что аниматор получил правильное значение
        if (animator != null)
        {
            animator.SetBool(IsCrouchingHash, isCrouching);
        }
    }

    void HandleGroundCheck()
    {
        float offset = groundCheckOffset;
        float skinWidth = 0.1f;

        Vector3 centerBottom = transform.position + controller.center;
        centerBottom.y -= controller.height * 0.5f;

        float radius = controller.radius;
        Vector3 leftRay = centerBottom + transform.right * (-radius + skinWidth);
        Vector3 rightRay = centerBottom + transform.right * (radius - skinWidth);

        bool groundedLeft = Physics.Raycast(leftRay, Vector3.down, offset, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        bool groundedCenter = Physics.Raycast(centerBottom, Vector3.down, offset, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        bool groundedRight = Physics.Raycast(rightRay, Vector3.down, offset, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        bool isActuallyGrounded = groundedLeft || groundedCenter || groundedRight;

        if (isActuallyGrounded)
        {
            groundCheckTimer = groundCheckDelay;
            if (velocity.y < 0)
                velocity.y = -2f;

            if (isInJumpPhase)
            {
                isInJumpPhase = false;
            }
        }
        else
        {
            groundCheckTimer -= Time.deltaTime;
        }

        isGrounded = isEffectivelyGrounded;
    }

    void HandleJump()
    {
        if (isGrounded && jumpPressed && !isCrouching)
        {
            jumpQueued = true;
            isInJumpPhase = true;
            jumpPressed = false;
        }
    }

    public void ApplyJumpImpulse()
    {
        if (jumpQueued)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpQueued = false;
        }
    }

    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
    }

    void UpdateCrouchState()
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCenterY = isCrouching ? crouchingHeight * 0.5f : standingHeight * 0.5f;

        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchSmoothSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, new Vector3(0, targetCenterY, 0), crouchSmoothSpeed * Time.deltaTime);
    }

    float CalculateSpeed(float moveX, float moveY, bool isRunning, bool isCrouching)
    {
        float threshold = 0.5f;

        if (isCrouching)
        {
            if (IsDirection(moveX, moveY, 0, 1, threshold)) return crouchForward;
            if (IsDirection(moveX, moveY, 0, -1, threshold)) return crouchBackward;
            if (IsDirection(moveX, moveY, -1, 0, threshold)) return crouchLeft;
            if (IsDirection(moveX, moveY, 1, 0, threshold)) return crouchRight;
            if (IsDirection(moveX, moveY, -1, 1, threshold)) return crouchDiagForwardLeft;
            if (IsDirection(moveX, moveY, 1, 1, threshold)) return crouchDiagForwardRight;
            if (IsDirection(moveX, moveY, -1, -1, threshold)) return crouchDiagBackwardLeft;
            if (IsDirection(moveX, moveY, 1, -1, threshold)) return crouchDiagBackwardRight;
            return (crouchForward + crouchLeft) * 0.5f;
        }
        else if (isRunning)
        {
            if (IsDirection(moveX, moveY, 0, 1, threshold)) return runForward;
            if (IsDirection(moveX, moveY, 0, -1, threshold)) return runBackward;
            if (IsDirection(moveX, moveY, -1, 0, threshold)) return runStrafeLeft;
            if (IsDirection(moveX, moveY, 1, 0, threshold)) return runStrafeRight;
            if (IsDirection(moveX, moveY, -1, 1, threshold)) return runDiagForwardLeft;
            if (IsDirection(moveX, moveY, 1, 1, threshold)) return runDiagForwardRight;
            if (IsDirection(moveX, moveY, -1, -1, threshold)) return runDiagBackwardLeft;
            if (IsDirection(moveX, moveY, 1, -1, threshold)) return runDiagBackwardRight;
            return (runForward + runStrafeLeft) * 0.5f;
        }
        else
        {
            if (IsDirection(moveX, moveY, 0, 1, threshold)) return walkForward;
            if (IsDirection(moveX, moveY, 0, -1, threshold)) return walkBackward;
            if (IsDirection(moveX, moveY, -1, 0, threshold)) return strafeLeft;
            if (IsDirection(moveX, moveY, 1, 0, threshold)) return strafeRight;
            if (IsDirection(moveX, moveY, -1, 1, threshold)) return diagForwardLeft;
            if (IsDirection(moveX, moveY, 1, 1, threshold)) return diagForwardRight;
            if (IsDirection(moveX, moveY, -1, -1, threshold)) return diagBackwardLeft;
            if (IsDirection(moveX, moveY, 1, -1, threshold)) return diagBackwardRight;
            return (walkForward + strafeLeft) * 0.5f;
        }
    }

    bool IsDirection(float x, float y, float targetX, float targetY, float threshold)
    {
        return Mathf.Abs(x - targetX) < threshold && Mathf.Abs(y - targetY) < threshold;
    }

    // === МЕТОД ДЛЯ ВЫЗОВА ИЗ ТРИГГЕРА ===
    public void SetForcedCrouch(bool force)
    {
        if (force)
        {
            wasUserCrouching = isCrouching;
            isCrouchForced = true;
            isCrouching = true;
        }
        else if (isCrouchForced)
        {
            isCrouchForced = false;
            isCrouching = wasUserCrouching;
        }
        ManageCrouchState(isCrouching); // Обновит физику и анимацию
    }
    
    // Дополнительный метод для отладки
    [ContextMenu("Force Crouch")]
    public void DebugForceCrouch()
    {
        isCrouching = !isCrouching;
        ManageCrouchState(isCrouching);
        Debug.Log($"Debug: Crouching state set to {isCrouching}");
    }
}