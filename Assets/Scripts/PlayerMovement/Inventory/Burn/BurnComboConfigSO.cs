using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Config for Inventory Burn combo bonus.
///
/// Default rule (as decided):
/// - burnCount == 1 => 0%
/// - burnCount >= 2 => bonusPercent = (2 * burnCount + 1)%
///   (2=>5, 3=>7, 4=>9, ...)
///
/// BonusOil should be computed as:
///     floor(baseSum * bonusPercent / 100)
///
/// You can override specific counts via the Overrides list.
/// </summary>
[CreateAssetMenu(menuName = "Lantern Roguelite/Burn/Burn Combo Config")]
public class BurnComboConfigSO : ScriptableObject
{
    [Serializable]
    public struct OverrideEntry
    {
        [Min(1)] public int burnCount;
        [Min(0)] public int bonusPercent;
    }

    [Header("Overrides (optional)")]
    [Tooltip("If an entry matches burnCount, its bonusPercent is used instead of the default formula.")]
    [SerializeField] private List<OverrideEntry> overrides = new();

    [Header("Default Formula (used when no override matches)")]
    [Tooltip("Minimum burn count where bonus starts. Should remain 2 for your current design.")]
    [SerializeField] private int bonusStartsAt = 2;

    [Tooltip("Default percent formula: (2*burnCount + 1). You can change the slope/offset here if you ever rebalance.")]
    [SerializeField] private int slope = 2;

    [SerializeField] private int offset = 1;

    /// <summary>
    /// Returns the bonus percent for the given burnCount.
    /// </summary>
    public int GetBonusPercent(int burnCount)
    {
        if (burnCount < bonusStartsAt)
            return 0;

        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].burnCount == burnCount)
                return Mathf.Max(0, overrides[i].bonusPercent);
        }

        // Default: 2*burnCount + 1 (when bonusStartsAt=2, slope=2, offset=1)
        return Mathf.Max(0, slope * burnCount + offset);
    }

    /// <summary>
    /// Convenience: the original hardcoded rule.
    /// </summary>
    public static int DefaultRule(int burnCount)
    {
        if (burnCount < 2) return 0;
        return 2 * burnCount + 1;
    }
}
