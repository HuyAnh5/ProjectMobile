using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure data: tên, icon, mô tả + danh sách các Effect chiến lược.
/// Không có logic ở đây.
/// </summary>
[CreateAssetMenu(fileName = "ItemData", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Display")]
    public string id;
    public string displayName;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Design")]
    [Tooltip("Normal items = false. Cursed items = true.")]
    public bool isCursed;

    [Header("Behaviour")]
    [Tooltip("Pluggable effects executed on equip / unequip / burn.")]
    public List<ItemEffect> effects = new List<ItemEffect>();

    [Header("Burn settings")]
    [Tooltip("Lượng dầu cơ bản khi đốt 1 món.")]
    public float baseOilOnBurn = 10f;

    [Tooltip("Đánh dấu item là Material để tính combo burn (5 mats = ~1 equipment).")]
    public bool isMaterial = false;
}
