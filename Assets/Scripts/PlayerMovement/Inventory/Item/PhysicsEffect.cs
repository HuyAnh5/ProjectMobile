using UnityEngine;

// ---------------- PHYSICS EFFECT (e.g. Cursed Boots) ----------------

[CreateAssetMenu(fileName = "PhysicsEffect", menuName = "Inventory/Effects/Physics")]
public class PhysicsEffect : ItemEffect
{
    [Header("Equip: low-friction slide (cursed behaviour)")]
    public bool modifyDrag = true;
    public float dragMultiplier = 0.3f; // <1 → trượt nhiều

    private readonly System.Collections.Generic.Dictionary<PlayerItemSlots, float> _originalDrag
        = new();

    public override void OnEquip(PlayerItemSlots target)
    {
        if (!modifyDrag || !target.Player) return;

        var rb = target.Player.GetComponent<Rigidbody2D>();
        if (rb != null && !_originalDrag.ContainsKey(target))
        {
            _originalDrag[target] = rb.linearDamping;
            rb.linearDamping *= dragMultiplier;
        }
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        if (!modifyDrag || !target.Player) return;

        var rb = target.Player.GetComponent<Rigidbody2D>();
        if (rb != null && _originalDrag.TryGetValue(target, out float drag))
        {
            rb.linearDamping = drag;
            _originalDrag.Remove(target);
        }
    }
}
