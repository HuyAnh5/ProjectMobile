using System.Collections;
using UnityEngine;

/// <summary>
/// Script chuyên quản lý việc mở / đóng Inventory,
/// tách riêng khỏi hệ thống Pause menu.
/// 
/// - Nhấn phím (ví dụ KeyCode.I) để mở/đóng túi (Dropping mode).
/// - Có hàm public để mở Inventory ở Looting mode từ rương/xác quái.
/// - Tự pause / unpause game bằng Time.timeScale.
/// - Khi đóng, nếu có pending Burn effects thì kích coroutine ExecuteBurnQueue.
/// </summary>
public class InventoryController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private PlayerItemSlots playerItemSlots;

    [Header("Input")]
    [SerializeField] private KeyCode inventoryKey = KeyCode.I;

    /// <summary>
    /// Inventory có đang mở không?
    /// </summary>
    public bool IsInventoryOpen { get; private set; }

    /// <summary>
    /// Có đang ở Looting mode không (đang tương tác rương/xác quái)?
    /// </summary>
    public bool IsLooting => IsInventoryOpen && inventoryManager != null && inventoryManager.IsLootMode;

    private void Awake()
    {
        if (inventoryManager == null)
            inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (playerItemSlots == null)
            playerItemSlots = FindAnyObjectByType<PlayerItemSlots>();
    }

    private void Update()
    {
        // Mở/đóng inventory bằng phím riêng (không chung pause)
        if (Input.GetKeyDown(inventoryKey))
        {
            // Nếu đang loot từ container, thường bạn sẽ đóng qua UI / script container,
            // ở đây ta cho phép phím I cũng đóng luôn.
            if (!IsInventoryOpen)
            {
                OpenInventoryManual();
            }
            else
            {
                CloseInventory();
            }
        }
    }

    // ======================================================================
    // PUBLIC API
    // ======================================================================

    /// <summary>
    /// Mở Inventory ở chế độ bình thường (Dropping mode): Overflow rỗng.
    /// </summary>
    public void OpenInventoryManual()
    {
        if (IsInventoryOpen) return;
        if (inventoryManager == null) return;

        // Mở với source = null => Dropping mode
        inventoryManager.OpenInventory(null);
        PauseGame();
        IsInventoryOpen = true;
    }

    /// <summary>
    /// Mở Inventory ở chế độ Looting (rương, xác quái).
    /// Gọi từ LootContainer (implement ILootSource).
    /// </summary>
    /// <param name="source">Nguồn loot (rương/xác quái) implement ILootSource.</param>
    public void OpenInventoryFromLoot(ILootSource source)
    {
        if (IsInventoryOpen) return;
        if (inventoryManager == null || source == null) return;

        inventoryManager.OpenInventory(source);
        PauseGame();
        IsInventoryOpen = true;
    }

    /// <summary>
    /// Đóng Inventory (dù là Dropping hay Looting).
    /// </summary>
    public void CloseInventory()
    {
        if (!IsInventoryOpen) return;
        if (inventoryManager == null) return;

        inventoryManager.CloseInventory();
        ResumeGame();
        IsInventoryOpen = false;

        // Sau khi Inventory đóng và game chạy lại,
        // nếu có hiệu ứng Burn pending thì kích hoạt chúng.
        if (playerItemSlots != null && playerItemSlots.HasPendingBurnEffects)
        {
            StartCoroutine(RunBurnQueue());
        }
    }

    // ======================================================================
    // INTERNAL HELPERS
    // ======================================================================

    private void PauseGame()
    {
        // Nếu game của bạn có hệ thống Pause phức tạp hơn,
        // có thể chuyển sang dùng PauseManager thay vì chỉnh timeScale trực tiếp.
        Time.timeScale = 0f;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    private IEnumerator RunBurnQueue()
    {
        // Đảm bảo PlayerItemSlots tồn tại
        if (playerItemSlots == null)
            yield break;

        yield return playerItemSlots.ExecuteBurnQueue();
    }
}
