using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovement : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;

    [SerializeField] private float stepLength = 0.8f;
    [SerializeField] private float animationStepTime = 0.6f;

    // Кешированные хеши параметров аниматора
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // Кешированная скорость для избежания лишних вызовов SetFloat
    private float targetSpeed;
    private float lastSpeed = -1f;

    // Input Actions
    private Echo_Imput controls;
    private Vector2 moveInput;

    void Awake()
    {
        controls = new Echo_Imput();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => moveInput = Vector2.zero;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        targetSpeed = moveDirection.magnitude * (stepLength / animationStepTime);

        controller.Move(moveDirection * targetSpeed * Time.deltaTime);

        // Обновляем аниматор только при изменении скорости
        if (!Mathf.Approximately(targetSpeed, lastSpeed))
        {
            animator.SetFloat(SpeedHash, targetSpeed);
            lastSpeed = targetSpeed;
        }
    }
}
