using UnityEngine;

public class InventoryUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup inventoryGroup;
    [SerializeField] private ExternalLootSession externalSession;   // <-- thay externalDropper
    [SerializeField] private PlayerItemSlots playerItemSlots;
    [Header("Inventory Burn (Session)")]
    [SerializeField] private InventoryBurnSession burnSession;
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
        bool wasOpen = IsOpen;
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

        // Phase 1: start a fresh burn session when opening inventory
        if (open && !wasOpen)
        {
            burnSession?.BeginSession();
        }

        if (!open && wasOpen)
        {
            externalSession?.CommitExternal();

            // Phase 3: Commit pending burn (add oil + enqueue effects)
            if (burnSession != null && playerItemSlots != null)
            {
                var effects = new System.Collections.Generic.List<ItemEffect>(8);
                if (burnSession.ConsumeCommit(out int totalOilGain, effects))
                {
                    // Inventory Burn: no overcap. OilLamp.AddOil already clamps to capacity.
                    if (playerItemSlots.OilLamp != null)
                        playerItemSlots.OilLamp.AddOil(totalOilGain);

                    for (int i = 0; i < effects.Count; i++)
                        playerItemSlots.EnqueuePendingBurnEffect(effects[i]);
                }
            }

            if (playerItemSlots != null && playerItemSlots.HasPendingBurnEffects)
                StartCoroutine(playerItemSlots.ExecuteBurnQueue());
        }
    }

}
