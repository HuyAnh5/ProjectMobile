using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "WhetstoneEffect", menuName = "Inventory/Effects/Whetstone")]
public class WhetstoneEffect : ItemEffect
{
    [Header("Equip: melee buff")]
    [Tooltip("T?ng % damage c?n chi?n (0.1 = +10%)")]
    public float meleeDamageBonusPercent = 0.10f;

    [Tooltip("Knockback thêm cho m?i cú chém")]
    public float knockbackBonus = 4f;

    [Header("Burn: next melee hit ×300%")]
    public float nextHitMultiplier = 3f;

    // Cache multiplier c? cho t?ng MeleeAutoRunner
    private readonly Dictionary<MeleeAutoRunner, float> _originalDamageMul = new();
    private readonly Dictionary<MeleeAutoRunner, float> _originalKnockback = new();

    public override void OnEquip(PlayerItemSlots target)
    {
        if (!target.Player) return;

        var melees = target.Player.GetComponentsInChildren<MeleeAutoRunner>(true);
        foreach (var mr in melees)
        {
            if (!_originalDamageMul.ContainsKey(mr))
                _originalDamageMul[mr] = mr.DamageMultiplier;
            if (!_originalKnockback.ContainsKey(mr))
                _originalKnockback[mr] = mr.KnockbackForce;

            mr.DamageMultiplier = mr.DamageMultiplier * (1f + meleeDamageBonusPercent);
            mr.KnockbackForce = mr.KnockbackForce + knockbackBonus;
        }
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        if (!target.Player) return;

        var melees = target.Player.GetComponentsInChildren<MeleeAutoRunner>(true);
        foreach (var mr in melees)
        {
            if (_originalDamageMul.TryGetValue(mr, out float mul))
                mr.DamageMultiplier = mul;

            if (_originalKnockback.TryGetValue(mr, out float kb))
                mr.KnockbackForce = kb;
        }

        _originalDamageMul.Clear();
        _originalKnockback.Clear();
    }

    public override void OnBurn(PlayerItemSlots target)
    {
        if (!target.Player) return;

        var melees = target.Player.GetComponentsInChildren<MeleeAutoRunner>(true);
        foreach (var mr in melees)
        {
            mr.ApplyNextHitMultiplier(nextHitMultiplier);
        }
    }
}
