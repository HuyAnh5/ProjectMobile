using UnityEngine;

public class LootContainer : MonoBehaviour
{
    [TextArea(2, 10)]
    [SerializeField] private string lootJson;

    [SerializeField] private bool destroyWhenEmpty = true;
    public bool DestroyWhenEmpty => destroyWhenEmpty;

    public string GetLootJson() => lootJson;   // <-- ADD
    public void SetLootJson(string json) => lootJson = json;

    public void LoadToExternal(InventoryTetris external)
    {
        if (external == null) return;
        if (string.IsNullOrEmpty(lootJson)) return;
        external.Load(lootJson);
    }

    public void SaveFromExternal(InventoryTetris external)
    {
        if (external == null) return;
        lootJson = external.Save();
    }

    public bool IsEmpty()
    {
        if (string.IsNullOrEmpty(lootJson)) return true;
        var list = JsonUtility.FromJson<InventoryTetris.ListAddItemTetris>(lootJson);
        return list.addItemTetrisList == null || list.addItemTetrisList.Count == 0;
    }
}
