using System;
using UnityEngine;
using UnityEngine.UI;

public enum LoadoutSlotType
{
    Weapon,
    Item,
}

public class LoadoutSlot : MonoBehaviour
{
    [Header("Slot Type")]
    public LoadoutSlotType slotType = LoadoutSlotType.Weapon;

    [Header("UI")]
    [SerializeField] private Image iconImage;

    [Header("Runtime")]
    [SerializeField] private ItemTetrisSO currentItem;

    public Action<LoadoutSlot> OnContentChanged;
    public bool IsEmpty => currentItem == null;
    public ItemTetrisSO CurrentItem => currentItem;

    // Slot Weapon chỉ nhận item.isWeapon = true
    // Slot Item chỉ nhận item.isWeapon = false
    public bool Accepts(ItemTetrisSO item)
    {
        if (item == null) return false;

        switch (slotType)
        {
            case LoadoutSlotType.Weapon:
                return item.isWeapon;
            case LoadoutSlotType.Item:
                return !item.isWeapon;
            default:
                return false;
        }
    }

    public void Equip(ItemTetrisSO item)
    {
        currentItem = item;
        RefreshIcon();
        OnContentChanged?.Invoke(this);
    }

    public void Clear()
    {
        currentItem = null;
        RefreshIcon();
        OnContentChanged?.Invoke(this);
    }

    public void RefreshIcon()
    {
        if (!iconImage) return;

        if (currentItem == null || currentItem.loadoutSprite == null)
        {
            iconImage.enabled = false;
            iconImage.sprite = null;
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite = currentItem.loadoutSprite;
    }

    // Dùng khi đang drag để tạm ẩn/hiện icon 1x1
    public void SetIconVisible(bool visible)
    {
        if (!iconImage) return;

        if (visible && currentItem != null && currentItem.loadoutSprite != null)
        {
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }
    }

    private void Reset()
    {
        if (!iconImage)
            iconImage = GetComponentInChildren<Image>();
    }
}
