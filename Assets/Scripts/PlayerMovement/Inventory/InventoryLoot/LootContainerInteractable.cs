using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(LootContainer))]

public class LootContainerInteractable : MonoBehaviour
{
    [SerializeField] private LootContainer container;
    [SerializeField] private ExternalLootSession externalSession;
    [SerializeField] private InventoryUIController inventoryUI; // bạn dùng controller nào thì gọi mở ở đây
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool inRange;

    private void Awake()
    {
        if (container == null) container = GetComponent<LootContainer>();
        if (externalSession == null) externalSession = FindAnyObjectByType<ExternalLootSession>();
        if (inventoryUI == null) inventoryUI = FindAnyObjectByType<InventoryUIController>();
    }

    private void Update()
    {
        if (!inRange) return;
        if (!Input.GetKeyDown(interactKey)) return;

        if (container != null && container.IsEmpty())
        {
            Destroy(container.gameObject);
            return;
        }

        if (externalSession != null) externalSession.OpenFromSource(container);
        if (inventoryUI != null) inventoryUI.Open(); // hoặc SetOpen(true)
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) inRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) inRange = false;
    }
}
