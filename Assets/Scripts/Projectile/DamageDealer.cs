using UnityEngine;

/// Gây sát thương khi chạm Hurtbox phe đối địch. Thường gắn vào đạn (projectile).
public class DamageDealer : MonoBehaviour
{
    public enum Team { Player, Enemy }
    [Header("Deal")]
    [SerializeField] float damage = 3f;
    [SerializeField] Team team = Team.Player;
    [SerializeField] bool destroyOnHit = true;

    [Header("Knockback")]
    [SerializeField] float knockbackForce = 2f;

    public float KnockbackForce
    {
        get => knockbackForce;
        set => knockbackForce = Mathf.Max(0f, value);
    }


    void OnTriggerEnter2D(Collider2D other)
    {
        var hb = other.GetComponent<Hurtbox>();
        if (!hb) return;

        // khác phe mới xử lý
        bool isOppositeTeam =
            (team == Team.Player && hb.team == Hurtbox.Team.Enemy) ||
            (team == Team.Enemy && hb.team == Hurtbox.Team.Player);

        if (!isOppositeTeam) return;

        // 1) Gây damage
        if (hb.health) hb.health.TakeDamage(damage);

        // 2) Knockback (nếu có enemyHealth và force > 0)
        if (knockbackForce > 0f && hb.enemyHealth)
        {
            // Hướng lùi = ngược hướng enemy đang "nhìn"
            Vector2 backDir = -(Vector2)hb.enemyHealth.transform.up;

            // fallback: nếu up = (0,0) thì lùi xa khỏi projectile
            if (backDir.sqrMagnitude < 0.0001f)
            {
                Vector2 enemyPos = hb.enemyHealth.transform.position;
                backDir = (enemyPos - (Vector2)transform.position).normalized;
            }

            hb.enemyHealth.ApplyKnockback(backDir, knockbackForce);
        }

        // 3) Hủy projectile (nếu cấu hình)
        if (destroyOnHit) Destroy(gameObject);
    }

}
