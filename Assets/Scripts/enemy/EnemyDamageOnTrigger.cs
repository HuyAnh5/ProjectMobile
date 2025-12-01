using UnityEngine;
using System.Collections.Generic;

/// Gây sát thương lên Player khi trigger va chạm trong đúng "pha tấn công".
/// - Dùng cho: pounceHitbox của Pouncer, swing/hitbox Runner.
/// - Bomber dùng AoE riêng (OverlapCircle) ở khắc phục bên dưới.
[DisallowMultipleComponent]
public class EnemyDamageOnTrigger : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Sát thương tính theo half-heart (1 = nửa tim, 2 = 1 tim)")]
    public int halfHearts = 1;

    [Header("Per-target cooldown")]
    [Tooltip("Khoảng tối thiểu giữa 2 lần trúng cùng một mục tiêu")]
    public float perTargetCooldown = 0.5f;

    // track đơn giản theo instance id
    private readonly Dictionary<int, float> _lastHitTime = new();

    void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    void OnTriggerStay2D(Collider2D other) => TryHit(other);

    void TryHit(Collider2D other)
    {
        var hb = other.GetComponentInParent<Hurtbox>();
        if (!hb || hb.team != Hurtbox.Team.Player) return;

        int id = other.attachedRigidbody ? other.attachedRigidbody.GetInstanceID() : other.GetInstanceID();
        if (_lastHitTime.TryGetValue(id, out float t) && Time.time - t < perTargetCooldown) return;

        // route vào PlayerHealth qua Hurtbox helper (tôn trọng i-frames trong PlayerHealth)
        if (hb.DamagePlayerHalfHearts(Mathf.Max(1, halfHearts)))
            _lastHitTime[id] = Time.time;
    }
}
