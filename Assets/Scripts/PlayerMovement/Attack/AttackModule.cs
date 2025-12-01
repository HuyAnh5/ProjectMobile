using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackModule", menuName = "Combat/Attack Module")]
public class AttackModule : ScriptableObject
{
    [Header("Fire")]
    [Min(0.05f)] public float fireInterval = 0.6f; // giây/viên (DoD: 0.6)
    [Range(0f, 45f)] public float spreadDegrees = 0f; // tản đạn (tùy)

    [Header("Knockback (ranged)")]
    [Tooltip("Knockback cơ bản của mọi projectile thuộc module này.")]
    public float baseKnockback = 0f;


    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 14f;
    public float projectileLifetime = 3.0f;

    [Header("Damage (tùy)")]
    public float damage = 3f;
    public DamageDealer.Team team = DamageDealer.Team.Player;

}
