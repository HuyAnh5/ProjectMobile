using System;
using UnityEngine;

public class ActiveLoadoutState : MonoBehaviour
{
    [Header("Weapon slots (3)")]
    [SerializeField] private LoadoutSlot weaponSlot1;
    [SerializeField] private LoadoutSlot weaponSlot2;
    [SerializeField] private LoadoutSlot weaponSlot3;

    [Header("Item slots (2)")]
    [SerializeField] private LoadoutSlot itemSlot1;
    [SerializeField] private LoadoutSlot itemSlot2;

    public event Action OnLoadoutChanged;

    private void Awake()
    {
        Subscribe(weaponSlot1);
        Subscribe(weaponSlot2);
        Subscribe(weaponSlot3);
        Subscribe(itemSlot1);
        Subscribe(itemSlot2);
    }

    private void Subscribe(LoadoutSlot slot)
    {
        if (slot == null) return;
        slot.OnContentChanged += HandleSlotChanged;
    }

    private void HandleSlotChanged(LoadoutSlot slot)
    {
        OnLoadoutChanged?.Invoke();
    }

    // --- API ??c loadout ---

    public ItemTetrisSO GetWeapon(int index)
    {
        return index switch
        {
            0 => weaponSlot1 != null ? weaponSlot1.CurrentItem : null,
            1 => weaponSlot2 != null ? weaponSlot2.CurrentItem : null,
            2 => weaponSlot3 != null ? weaponSlot3.CurrentItem : null,
            _ => null
        };
    }

    public ItemTetrisSO GetItem(int index)
    {
        return index switch
        {
            0 => itemSlot1 != null ? itemSlot1.CurrentItem : null,
            1 => itemSlot2 != null ? itemSlot2.CurrentItem : null,
            _ => null
        };
    }

    public ItemTetrisSO[] GetAllWeapons()
    {
        return new[]
        {
            GetWeapon(0),
            GetWeapon(1),
            GetWeapon(2),
        };
    }

    public ItemTetrisSO[] GetAllItems()
    {
        return new[]
        {
            GetItem(0),
            GetItem(1),
        };
    }
}
