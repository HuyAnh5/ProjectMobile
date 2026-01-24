using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks Inventory Burn within ONE inventory-open session.
///
/// Decisions locked in:
/// - Burn happens by dragging items into BurnZone.
/// - Burned items are removed immediately (no undo).
/// - Oil is NOT added while inventory is open (game paused).
/// - Oil is committed when inventory closes.
/// - Inventory Burn has NO overcap (caller should add oil via OilLamp.AddOil which clamps to capacity).
/// - Combo bonus:
///     burnCount 1: 0%
///     burnCount 2: +5% (of baseSum)
///     burnCount 3: +7%
///     burnCount 4: +9%
///     burnCount 5: +11%
///     burnCount >5: stays at combo level 5 (still +11% by default)
///   (default rule: (c<2?0:(2*c+1)) where c = min(burnCount, 5))
///   bonusOil is ALWAYS floored.
/// - UI preview:
///     1 item  => "{baseSum}"
///     >=2    => "{baseSum} (+{bonusOil})"
/// </summary>
public class InventoryBurnSession : MonoBehaviour
{
    public event Action OnSessionChanged;

    // Combo is capped to level 5 by design.
    private const int MaxComboLevel = 5;

    [Header("Config")]
    [Tooltip("Optional. If null, session uses the default rule: (c<2?0:(2*c+1)) where c=min(burnCount, 5)")]
    [SerializeField] private BurnComboConfigSO comboConfig;

    [Header("Debug (read-only)")]
    [SerializeField] private int baseSum;
    [SerializeField] private int bonusOil;
    [SerializeField] private int burnCount;

    private readonly List<ItemData> burnedItemData = new();

    public int BaseSum => baseSum;
    public int BonusOil => bonusOil;
    public int BurnCount => burnCount;

    public bool HasAnythingBurned => burnCount > 0;

    /// <summary>
    /// Call when opening inventory.
    /// </summary>
    public void BeginSession()
    {
        ClearInternal();
        RaiseChanged();
    }

    /// <summary>
    /// Phase 1: Add a burned item to this session.
    /// IMPORTANT: this does NOT add oil.
    /// Caller should remove the item from grid/loadout BEFORE/AFTER calling this (no undo).
    /// </summary>
    public void AddBurn(ItemData data)
    {
        burnCount++;

        if (data != null)
        {
            burnedItemData.Add(data);

            // Treat base oil as integer. If your ItemData uses int already, this is a no-op.
            baseSum += Mathf.Max(0, Mathf.FloorToInt(data.baseOilOnBurn));
        }

        RecomputeBonus();
        RaiseChanged();
    }

    /// <summary>
    /// Phase 2: Text for UI preview.
    /// </summary>
    public string GetPreviewText()
    {
        if (burnCount <= 0) return string.Empty;
        if (burnCount == 1) return baseSum.ToString();
        return $"{baseSum} (+{bonusOil})";
    }

    /// <summary>
    /// Phase 3: Consume all pending burn data.
    /// - Returns totalOilGain = baseSum + bonusOil.
    /// - Outputs OnBurn effects to enqueue (execution happens after closing inventory).
    /// - Clears the session.
    ///
    /// Inventory Burn does NOT overcap; clamp should be done by the caller (OilLamp.AddOil already clamps).
    /// </summary>
    public bool ConsumeCommit(out int totalOilGain, List<ItemEffect> outEffects)
    {
        if (!HasAnythingBurned)
        {
            totalOilGain = 0;
            return false;
        }

        totalOilGain = Mathf.Max(0, baseSum + bonusOil);

        if (outEffects != null)
        {
            for (int i = 0; i < burnedItemData.Count; i++)
            {
                ItemData d = burnedItemData[i];
                if (d == null || d.effects == null) continue;

                for (int e = 0; e < d.effects.Count; e++)
                {
                    ItemEffect eff = d.effects[e];
                    if (eff != null) outEffects.Add(eff);
                }
            }
        }

        ClearInternal();
        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Clears session manually (rarely needed). Useful if you force-close inventory.
    /// </summary>
    public void CancelAndClear()
    {
        ClearInternal();
        RaiseChanged();
    }

    private void RecomputeBonus()
    {
        if (burnCount < 2)
        {
            bonusOil = 0;
            return;
        }

        // IMPORTANT: Combo bonus level is capped at 5.
        // You can burn more than 5 items in a session, but bonus percent will not increase beyond level 5.
        int comboLevel = Mathf.Min(burnCount, MaxComboLevel);

        int bonusPercent = comboConfig != null
            ? comboConfig.GetBonusPercent(comboLevel)
            : BurnComboConfigSO.DefaultRule(comboLevel);

        // bonusOil must ALWAYS floor.
        float raw = baseSum * (bonusPercent / 100f);
        bonusOil = Mathf.FloorToInt(raw);
    }

    private void ClearInternal()
    {
        baseSum = 0;
        bonusOil = 0;
        burnCount = 0;
        burnedItemData.Clear();
    }

    private void RaiseChanged() => OnSessionChanged?.Invoke();
}
