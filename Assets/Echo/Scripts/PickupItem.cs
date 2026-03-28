using UnityEngine;
using TMPro;

/// <summary>
/// Подбираемый предмет в мире.
/// Содержит данные ItemSO: тип, размер, стакаемость, иконка.
/// При подборе кладёт предмет в InventorySystem игрока.
/// </summary>
public class PickupItem : MonoBehaviour
{
    [Header("Данные предмета")]
    [Tooltip("ScriptableObject с описанием предмета (тип, размер ячеек, иконка, стакание)")]
    [SerializeField] private ItemSO itemData;

    [Tooltip("Количество подбираемого предмета")]
    [SerializeField] private int amount = 1;

    [Header("Настройки подбора")]
    [SerializeField] private float pickupRange  = 2f;
    [SerializeField] private bool  autoPickup   = false;

    [Header("Анимация")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bobSpeed      = 1f;
    [SerializeField] private float bobHeight     = 0.2f;

    [Header("UI подсказка")]
    [SerializeField] private GameObject pickupPromptPrefab;
    [SerializeField] private string     pickupMessage = "Нажмите E чтобы подобрать {item}";

    // ─── Внутренние переменные ──────────────────────────────────────────────
    private Transform         _playerTransform;
    private InventorySystem   _playerInventory;
    private GameObject        _activePrompt;
    private Vector3           _startPosition;
    private float             _bobOffset;

    // ─── Unity Events ───────────────────────────────────────────────────────

    private void Start()
    {
        _startPosition = transform.position;
        _bobOffset     = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        // Анимация: боб + вращение
        float bobY = Mathf.Sin(Time.time * bobSpeed + _bobOffset) * bobHeight;
        transform.position = _startPosition + Vector3.up * bobY;
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Авто-подбор
        if (autoPickup && _playerTransform != null)
        {
            if (Vector3.Distance(transform.position, _playerTransform.position) < pickupRange)
                TryPickup();
            Debug.Log($"[PickupItem] itemData = {itemData?.itemName ?? "NULL"}");
            Debug.Log($"[PickupItem] icon = {itemData?.icon?.name ?? "NULL"}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerTransform = other.transform;
        // Ищем InventorySystem — на игроке
        _playerInventory = other.GetComponentInParent<InventorySystem>();
        if (_playerInventory == null)
            _playerInventory = InventorySystem.Instance;

        if (!autoPickup)
            ShowPickupPrompt();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!autoPickup && other.CompareTag("Player"))
        {
            if (Input.GetKeyDown(KeyCode.E))
                TryPickup();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        HidePickupPrompt();
        _playerTransform = null;
        _playerInventory = null;
    }

    // ─── Логика подбора ─────────────────────────────────────────────────────

    private void TryPickup()
    {
        if (_playerInventory == null)
        {
            Debug.LogWarning($"[PickupItem] Не найден InventorySystem у игрока!");
            return;
        }
        if (itemData == null)
        {
            Debug.LogWarning($"[PickupItem] itemData не назначен на объекте {gameObject.name}!");
            return;
        }

        bool success = _playerInventory.AddItem(itemData, amount);

        if (success)
        {
            HidePickupPrompt();
            Destroy(gameObject);
        }
        else
        {
            ShowMessage("Инвентарь полон!");
        }
    }

    // ─── UI ─────────────────────────────────────────────────────────────────

    private void ShowPickupPrompt()
    {
        if (pickupPromptPrefab == null || _activePrompt != null) return;

        _activePrompt = Instantiate(pickupPromptPrefab,
                                    transform.position + Vector3.up * 1.5f,
                                    Quaternion.identity,
                                    transform);

        var txt = _activePrompt.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
            txt.text = pickupMessage.Replace("{item}", itemData != null ? itemData.itemName : "предмет");
    }

    private void HidePickupPrompt()
    {
        if (_activePrompt != null)
        {
            Destroy(_activePrompt);
            _activePrompt = null;
        }
    }

    private void ShowMessage(string message)
    {
        Debug.Log($"[PickupItem] {message}");
    }

    // ─── Гизмо ──────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }

    // ─── Публичный геттер для внешних систем ────────────────────────────────
    public ItemSO ItemData => itemData;
    public int    Amount   => amount;
}
