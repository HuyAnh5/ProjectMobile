using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Strategy base: m?i Effect là m?t ScriptableObject ??c l?p.
/// Không ch?a state c?a Item (ItemData s? hold list các effect này).
/// </summary>
public abstract class ItemEffect : ScriptableObject
{
    [TextArea]
    [Tooltip("Short description of what this effect does.")]
    public string tooltip;

    [Tooltip("Design flag only: mark this effect as cursed.")]
    public bool isCursed;

    /// <summary>Called when the item is equipped in any slot.</summary>
    public virtual void OnEquip(PlayerItemSlots target) { }

    /// <summary>Called when the item is unequipped from a slot.</summary>
    public virtual void OnUnequip(PlayerItemSlots target) { }

    /// <summary>Called when the item is burned/consumed.</summary>
    public virtual void OnBurn(PlayerItemSlots target) { }
}

/// <summary>
/// Pure data: tên, icon, mô t? + danh sách các Effect chi?n l??c.
/// Không có logic ? ?ây.
/// </summary>
