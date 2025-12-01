using UnityEngine;

[DisallowMultipleComponent]
public class Hurtbox : MonoBehaviour
{
    public enum Team { Player, Enemy }

    [Header("Team")]
    public Team team = Team.Enemy;

    [Header("Targets (auto)")]
    public EnemyHealth enemyHealth;   // mới
    public PlayerHealth playerHealth; // mới

    // --- LEGACY ALIAS cho code cũ (DamageDealer.cs đang dùng) ---
    [Tooltip("Alias legacy để tương thích với code cũ (DamageDealer.cs)")]
    public EnemyHealth health;        // <-- thêm lại

    void Reset()
    {
        enemyHealth = GetComponentInParent<EnemyHealth>();
        playerHealth = GetComponentInParent<PlayerHealth>();

        // đồng bộ alias
        health = enemyHealth;

        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        // đảm bảo alias và biến mới luôn khớp nếu người dùng kéo tay trong Inspector
        if (!enemyHealth && health) enemyHealth = health;
        if (!health && enemyHealth) health = enemyHealth;
    }

    // Helper cho kẻ địch đánh Player (đơn vị half-heart)
    public bool DamagePlayerHalfHearts(int halves)
    {
        if (team != Team.Player || !playerHealth) return false;
        return playerHealth.TakeDamageHalfHearts(Mathf.Max(1, halves));
    }
}
