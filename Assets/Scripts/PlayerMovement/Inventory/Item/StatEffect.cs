using UnityEngine;

// ---------------- STAT EFFECT ----------------

[CreateAssetMenu(fileName = "StatEffect", menuName = "Inventory/Effects/Stat")]
public class StatEffect : ItemEffect
{
    [Header("Max oil bonus (on equip / unequip)")]
    public bool modifyMaxOil;
    public float maxOilDelta;

    [Header("Move speed multiplier (on equip / unequip)")]
    public bool modifyMoveSpeed;
    public float moveSpeedMultiplier = 1.0f;

    [Header("Burn: instant oil gain")]
    public bool healOilOnBurn;
    public float burnOilAmount;

    // Cache runtime
    private readonly System.Collections.Generic.Dictionary<PlayerItemSlots, float> _originalMoveSpeed
        = new();
    private readonly System.Collections.Generic.HashSet<PlayerItemSlots> _appliedMaxOil
        = new();

    public override void OnEquip(PlayerItemSlots target)
    {
        var player = target.Player;
        var lamp = target.OilLamp;

        if (modifyMaxOil && lamp != null && !_appliedMaxOil.Contains(target))
        {
            // Tăng max oil qua property để tự clamp current nếu cần
            lamp.Capacity = lamp.Capacity + maxOilDelta;
            _appliedMaxOil.Add(target);
        }

        if (modifyMoveSpeed && player != null)
        {
            if (!_originalMoveSpeed.ContainsKey(target))
                _originalMoveSpeed[target] = player.MoveSpeed;

            player.MoveSpeed *= moveSpeedMultiplier;
        }
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        var player = target.Player;
        var lamp = target.OilLamp;

        if (modifyMaxOil && lamp != null && _appliedMaxOil.Contains(target))
        {
            // Trả lại max oil ban đầu
            lamp.Capacity = lamp.Capacity - maxOilDelta;
            _appliedMaxOil.Remove(target);
        }

        if (modifyMoveSpeed && player != null &&
            _originalMoveSpeed.TryGetValue(target, out float original))
        {
            player.MoveSpeed = original;
            _originalMoveSpeed.Remove(target);
        }
    }


    public override void OnBurn(PlayerItemSlots target)
    {
        var lamp = target.OilLamp;
        if (healOilOnBurn && lamp != null && burnOilAmount > 0f)
        {
            // Bơm dầu qua property để đi qua logic AddOil/DrainOil
            lamp.Current = lamp.Current + burnOilAmount;
        }
    }

}
