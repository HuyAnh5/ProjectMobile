using UnityEngine;

public class InventoryUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup inventoryGroup;
    [SerializeField] private ExternalLootSession externalSession;   // <-- thay externalDropper
    [SerializeField] private PlayerItemSlots playerItemSlots;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    public bool IsOpen { get; private set; }

    private void Start() => SetOpen(false);

    private void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;

        if (!IsOpen)
        {
            // mở inventory bình thường (TAB) => External là “đồ trên đất”, không gắn container
            externalSession?.OpenEmpty();
            SetOpen(true);
        }
        else
        {
            SetOpen(false);
        }
    }

    // để LootContainerInteractable gọi
    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);

    public void SetOpen(bool open)
    {
        IsOpen = open;

        if (inventoryGroup)
        {
            inventoryGroup.alpha = open ? 1f : 0f;
            inventoryGroup.interactable = open;
            inventoryGroup.blocksRaycasts = open;
        }

        // FIX: luôn hiển thị cursor (cả khi gameplay)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = open ? 0f : 1f;

        if (!open)
        {
            externalSession?.CommitExternal();

            if (playerItemSlots != null && playerItemSlots.HasPendingBurnEffects)
                StartCoroutine(playerItemSlots.ExecuteBurnQueue());
        }
    }

}
