using UnityEngine;

public class LoadoutToPlayerItemSlotsBridge : MonoBehaviour
{
    [SerializeField] private ActiveLoadoutState loadout;
    [SerializeField] private PlayerItemSlots playerItemSlots;

    private void OnEnable()
    {
        if (loadout != null) loadout.OnLoadoutChanged += Sync;
        Sync();
    }

    private void OnDisable()
    {
        if (loadout != null) loadout.OnLoadoutChanged -= Sync;
    }

    private void Sync()
    {
        if (playerItemSlots == null || loadout == null) return;

        for (int i = 0; i < 2; i++)
        {
            var itemSO = loadout.GetItem(i);
            var data = itemSO != null ? itemSO.itemData : null;
            playerItemSlots.EquipRuntime(i, data);
        }
    }
}
