using UnityEngine;
using UnityEngine.InputSystem;

public class LadderClimbToPoint : MonoBehaviour
{
    [Header("Настройки перемещения")]
    public Transform startPoint; // Точка начала подъема
    public Transform endPoint;   // Точка окончания подъема
    public float moveSpeed = 2f; // Скорость перемещения
    public string climbAnimationParameter = "IsClimbing";

    [Header("Фильтрация персонажа")]
    public string playerTag = "Player"; // Тег персонажа

    [Header("Объект для выравнивания")]
    public Transform targetToAlign; // Объект, который нужно выровнять (если null - используется сам персонаж)

    [Header("Выравнивание к лестнице")]
    public bool alignToLadder = true; // Выравнивать объект к лестнице
    [Range(0f, 1f)] public float alignSmoothness = 1f; // Плавность выравнивания (0 = мгновенно, 1 = плавно)

    [Header("Блокировка вращения")]
    public bool lockRotationX = true; // Блокировать вращение по X
    public bool lockRotationY = true; // Блокировать вращение по Y
    public bool lockRotationZ = true; // Блокировать вращение по Z

    private bool isClimbing = false;
    private bool isMovingUp = true;
    private float moveTimer = 0f;
    private float totalMoveTime;
    private GameObject currentPlayer;
    private Transform targetTransform; // Объект для выравнивания/вращения
    private Animator playerAnimator;
    private CharacterController playerController;
    private int playerLayer;
    private readonly int[] ignoreLayers = new int[0]; // Массив слоёв для игнорирования
    private Quaternion initialRotation; // Сохранённое вращение

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag) && !isClimbing)
        {
            StartClimbing(other.gameObject);
        }
    }

    void StartClimbing(GameObject player)
    {
        currentPlayer = player;
        playerAnimator = currentPlayer.GetComponent<Animator>();
        playerController = currentPlayer.GetComponent<CharacterController>();

        if (playerAnimator == null || playerController == null)
        {
            Debug.LogError("Персонаж не имеет Animator или CharacterController");
            return;
        }

        // === ОПРЕДЕЛЯЕМ ОБЪЕКТ ДЛЯ ВЫРАВНИВАНИЯ ===
        targetTransform = targetToAlign != null ? targetToAlign : currentPlayer.transform;

        // === ВЫРАВНИВАНИЕ К ЛОКАЛЬНОЙ ОСИ Z+ ОБЪЕКТА СО СКРИПТОМ ===
        if (alignToLadder)
        {
            AlignTargetToLadderObject();
        }

        // === СОХРАНЯЕМ НАЧАЛЬНОЕ ВРАЩЕНИЕ (ПОСЛЕ ВЫРАВНИВАНИЯ) ===
        initialRotation = targetTransform.rotation;

        // Сохраняем текущий слой персонажа
        playerLayer = currentPlayer.layer;

        isClimbing = true;
        isMovingUp = true; // Всегда подъем вверх

        // Рассчитываем время перемещения
        float distance = Vector3.Distance(startPoint.position, endPoint.position);
        totalMoveTime = distance / moveSpeed;
        moveTimer = 0f;

        // Включаем анимацию
        if (!string.IsNullOrEmpty(climbAnimationParameter))
        {
            playerAnimator.SetBool(climbAnimationParameter, true);
        }

        // Отключаем коллизию с указанными слоями
        SetCollisionIgnore(true);
    }

    void AlignTargetToLadderObject()
    {
        if (targetTransform == null) return;

        // === Направление локальной оси Z+ объекта со скриптом ===
        Vector3 targetForward = transform.forward; // Локальная ось Z+ объекта
        Vector3 targetUp = transform.up;           // Локальная ось Y+ объекта

        // === Вычисляем целевой поворот ===
        Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);

        // === Плавный или мгновенный поворот ===
        if (alignSmoothness < 1f && alignSmoothness > 0f)
        {
            // Плавный поворот
            targetTransform.rotation = Quaternion.Slerp(
                targetTransform.rotation,
                targetRotation,
                alignSmoothness * 10f * Time.deltaTime
            );
        }
        else if (alignSmoothness == 1f)
        {
            // Мгновенный поворот
            targetTransform.rotation = targetRotation;
        }
        else
        {
            // Плавный поворот с настраиваемой скоростью
            targetTransform.rotation = Quaternion.Slerp(
                targetTransform.rotation,
                targetRotation,
                alignSmoothness * 10f * Time.deltaTime
            );
        }
    }

    void SetCollisionIgnore(bool ignore)
    {
        if (currentPlayer == null) return;

        foreach (int layer in ignoreLayers)
        {
            Physics.IgnoreLayerCollision(playerLayer, layer, ignore);
        }
    }

    void Update()
    {
        if (isClimbing && currentPlayer != null)
        {
            HandleClimbing();
            HandleRotationLock(); // === БЛОКИРОВКА ВРАЩЕНИЯ (ПОСЛЕ ВЫРАВНИВАНИЯ) ===
        }
    }

    void HandleRotationLock()
    {
        if (targetTransform == null) return;

        Vector3 currentEulerAngles = targetTransform.eulerAngles;

        // === БЛОКИРУЕМ ВРАЩЕНИЕ ПО ОСЯМ ===
        if (lockRotationX) currentEulerAngles.x = initialRotation.eulerAngles.x;
        if (lockRotationY) currentEulerAngles.y = initialRotation.eulerAngles.y;
        if (lockRotationZ) currentEulerAngles.z = initialRotation.eulerAngles.z;

        // === ПРИМЕНЯЕМ ОГРАНИЧЕНИЕ ВРАЩЕНИЯ ===
        targetTransform.eulerAngles = currentEulerAngles;
    }

    void HandleClimbing()
    {
        moveTimer += Time.deltaTime;
        float progress = moveTimer / totalMoveTime;
        progress = Mathf.Clamp01(progress); // от 0 до 1

        // === Выбираем начальную и конечную точки в зависимости от направления ===
        Vector3 startPosition = isMovingUp ? startPoint.position : endPoint.position;
        Vector3 endPosition = isMovingUp ? endPoint.position : startPoint.position;

        Vector3 newPosition = Vector3.Lerp(startPosition, endPosition, progress);

        // === Временно отключаем CharacterController для плавного перемещения ===
        if (playerController != null)
        {
            playerController.enabled = false;
            currentPlayer.transform.position = newPosition;
            playerController.enabled = true;
        }
        
        // === Проверяем, достигли ли конца ===
        if (progress >= 1f)
        {
            FinishClimbing();
        }
    }

    void FinishClimbing()
    {
        if (currentPlayer != null && playerAnimator != null)
        {
            // === Выключаем анимацию ===
            if (!string.IsNullOrEmpty(climbAnimationParameter))
            {
                playerAnimator.SetBool(climbAnimationParameter, false);
            }
        }

        // === Восстанавливаем коллизию ===
        SetCollisionIgnore(false);

        isClimbing = false;
        currentPlayer = null;
        targetTransform = null;
        playerAnimator = null;
        playerController = null;
    }
}