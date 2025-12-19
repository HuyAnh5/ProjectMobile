using UnityEngine;

/// <summary>
/// Auto melee based on Lantern aura + weapon range.
/// - Detects enemies around the player.
/// - A target must stay inside effective range continuously for
///   requiredStayTimeBeforeSwing seconds before a swing is committed.
/// - After a swing, there is a swingCooldown before next swing can start.
/// - Swing direction is from player to the chosen target (nearest).
/// - Visual slash is handled by Animator; this script only rotates the
///   weapon and plays the trigger.
/// - Actual damage is applied via OnSwingHit() (called by Animation Event),
///   using a cone or full-circle hit shape around the player.
/// - Melee can still swing when oil is 0; oil cost is optional.
/// </summary>
[DisallowMultipleComponent]
public class MeleeAutoRunner : MonoBehaviour
{
    public enum MeleeShape
    {
        Cone,       // hit enemies in a cone in front of last attack direction
        FullCircle  // hit enemies in all directions
    }

    // ----------------------------------------------------------------------
    // References
    // ----------------------------------------------------------------------

    [Header("References")]
    [SerializeField] private PlayerController player;  // owner
    [SerializeField] private OilLamp oilLamp;          // for aura + optional oil cost
    [SerializeField] private Animator animator;        // weapon animator (child)

    [Tooltip("Animator trigger name that starts the swing animation")]
    [SerializeField] private string swingTriggerName = "Swing";

    // ----------------------------------------------------------------------
    // Attack timing & cost
    // ----------------------------------------------------------------------

    [Header("Timing & Cost")]
    [Tooltip("How long the target must stay inside range before we swing (sec)")]
    [SerializeField] private float requiredStayTimeBeforeSwing = 0.0f;

    [Tooltip("Cooldown between swings (seconds)")]
    [SerializeField] private float swingCooldown = 0.4f;

    [Tooltip("If true, consume oil when we swing; melee still works at 0 oil")]
    [SerializeField] private bool consumeOilOnSwing = true;

    [Tooltip("Oil consumed per swing if consumeOilOnSwing is true")]
    [SerializeField] private float oilCostPerSwing = 0.5f;

    [Tooltip("Damage dealt to each enemy hit")]
    [SerializeField] private float damagePerHit = 3f;

    [Header("External modifiers")]
    [Tooltip("Global multiplier from items (e.g. Whetstone). 1 = no change.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Bonus knockback impulse applied on hit (from items).")]
    [SerializeField] private float knockbackForce = 0f;

    [SerializeField] private bool combatEnabled = true;

    public void SetCombatEnabled(bool enabled)
    {
        combatEnabled = enabled;

        // reset state để khỏi “nhớ mục tiêu” khi bật lại
        if (!combatEnabled)
        {
            _currentTarget = null;
            _stayTimer = 0f;
        }
    }

    public bool IsCombatEnabled()
    {
        return combatEnabled;
    }



    // Public properties so items can adjust timing, cost, damage
    public float RequiredStayTimeBeforeSwing
    {
        get => requiredStayTimeBeforeSwing;
        set => requiredStayTimeBeforeSwing = Mathf.Max(0f, value);
    }

    public float SwingCooldown
    {
        get => swingCooldown;
        set => swingCooldown = Mathf.Max(0f, value);
    }

    public bool ConsumeOilOnSwing
    {
        get => consumeOilOnSwing;
        set => consumeOilOnSwing = value;
    }

    public float OilCostPerSwing
    {
        get => oilCostPerSwing;
        set => oilCostPerSwing = Mathf.Max(0f, value);
    }

    public float DamagePerHit
    {
        get => damagePerHit;
        set => damagePerHit = Mathf.Max(0f, value);
    }


    // Multiplier chỉ áp cho cú chém tiếp theo (Burn của Đá Mài)
    float _nextHitMultiplier = 1f;

    public float DamageMultiplier
    {
        get => damageMultiplier;
        set => damageMultiplier = Mathf.Max(0f, value);
    }

    public float KnockbackForce
    {
        get => knockbackForce;
        set => knockbackForce = Mathf.Max(0f, value);
    }

    /// <summary>Được gọi bởi item để buff cú chém kế tiếp (vd: 300%).</summary>
    public void ApplyNextHitMultiplier(float multiplier)
    {
        if (multiplier > _nextHitMultiplier)
            _nextHitMultiplier = multiplier;
    }


    [Tooltip("Which layers are considered enemies")]
    [SerializeField] private LayerMask enemyMask;

    // ----------------------------------------------------------------------
    // Range / shape
    // ----------------------------------------------------------------------

    [Header("Range & Shape")]
    [Tooltip("Weapon reach in world units (e.g. sword length)")]
    [SerializeField] private float weaponRange = 2.0f;

    [Tooltip("Require target to be inside aura radius as well as weapon range")]
    [SerializeField] private bool requireInsideAura = true;

    [Tooltip("Scale aura radius when used for melee (0.9 = slightly inside edge)")]
    [SerializeField] private float auraRadiusMultiplier = 0.9f;

    [Tooltip("Shape of the hit area")]
    [SerializeField] private MeleeShape shape = MeleeShape.Cone;

    [Tooltip("Total cone angle in degrees (only used if shape = Cone)")]
    [SerializeField] private float coneAngleDeg = 90f;
    // Public properties so items can adjust melee range / shape
    public float WeaponRange
    {
        get => weaponRange;
        set => weaponRange = Mathf.Max(0f, value);
    }

    public bool RequireInsideAura
    {
        get => requireInsideAura;
        set => requireInsideAura = value;
    }

    public float AuraRadiusMultiplier
    {
        get => auraRadiusMultiplier;
        set => auraRadiusMultiplier = Mathf.Max(0f, value);
    }

    public MeleeShape Shape
    {
        get => shape;
        set => shape = value;
    }

    public float ConeAngleDeg
    {
        get => coneAngleDeg;
        set => coneAngleDeg = Mathf.Clamp(value, 0f, 360f);
    }

    // ----------------------------------------------------------------------
    // Detection
    // ----------------------------------------------------------------------

    [Header("Detection")]
    [Tooltip("How often we scan for targets (seconds)")]
    [SerializeField] private float scanInterval = 0.05f;

    // Public property so items can speed up / slow down scan rate
    public float ScanInterval
    {
        get => scanInterval;
        set => scanInterval = Mathf.Max(0.01f, value);
    }


    // ----------------------------------------------------------------------
    // Knockback
    // ----------------------------------------------------------------------

    [Header("Knockback")]
    [Tooltip("Base knockback impulse even without items")]
    [SerializeField] private float baseKnockbackForce = 2f;

    [Tooltip("Extra knockback from items / curses")]
    [SerializeField] private float knockbackBonus = 0f;

    public float BaseKnockbackForce
    {
        get => baseKnockbackForce;
        set => baseKnockbackForce = Mathf.Max(0f, value);
    }

    public float KnockbackBonus
    {
        get => knockbackBonus;
        set => knockbackBonus = Mathf.Max(0f, value);
    }

    public float TotalKnockbackForce => Mathf.Max(0f, baseKnockbackForce + knockbackBonus);



    // ----------------------------------------------------------------------
    // Runtime state
    // ----------------------------------------------------------------------

    private float _nextScanTime;        // when we can scan again
    private float _nextSwingReadyTime;  // when we can start a new swing
    private float _stayTimer;           // how long current target stayed in range

    private Transform _currentTarget;   // target used for stay-time & direction
    private Vector2 _lastAttackDir = Vector2.up; // attack direction for cone

    void Awake()
    {
        if (!player) player = GetComponentInParent<PlayerController>();
        if (!oilLamp) oilLamp = GetComponentInParent<OilLamp>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

#if UNITY_EDITOR
    void Reset()
    {
        if (!player) player = GetComponentInParent<PlayerController>();
        if (!oilLamp) oilLamp = GetComponentInParent<OilLamp>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }
#endif


    private void Update()
    {
        // NEW: nếu bị orchestrator tắt
        if (!combatEnabled)
            return;

        // Guard: need at least a player reference to work
        if (!player)
            return;

        float now = Time.time;

        // 1) Respect scan interval
        if (now < _nextScanTime)
            return;
        _nextScanTime = now + scanInterval;

        // 2) Respect swing cooldown
        if (now < _nextSwingReadyTime)
            return;

        // 3) Update current target & stay timer
        UpdateTargetAndStayTimer(Time.deltaTime);

        // If we lost target, nothing to do
        if (!_currentTarget)
            return;

        // 4) Check required stay time before swinging
        if (_stayTimer < requiredStayTimeBeforeSwing)
            return;

        // 5) All conditions satisfied -> commit swing
        StartSwing(_lastAttackDir);
    }


    // ----------------------------------------------------------------------
    // Target & timing
    // ----------------------------------------------------------------------

    /// <summary>
    /// Returns the effective radius that a target must be inside:
    /// - inside weaponRange
    /// - AND, if requireInsideAura, inside aura radius * multiplier.
    /// We implement this by taking the minimum between weaponRange and aura.
    /// </summary>
    private float GetEffectiveRadius()
    {
        float radius = weaponRange;

        if (requireInsideAura && oilLamp != null)
        {
            float auraR = oilLamp.CurrentAuraRadius * auraRadiusMultiplier;
            if (auraR > 0f)
                radius = Mathf.Min(radius, auraR);
        }

        return Mathf.Max(0.01f, radius);
    }

    /// <summary>
    /// Scans for enemies inside effective radius, selects the nearest one,
    /// and updates the stay timer accordingly.
    /// </summary>
    private void UpdateTargetAndStayTimer(float deltaTime)
    {
        Vector2 origin = player.transform.position;
        float radius = GetEffectiveRadius();

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, enemyMask);
        if (hits == null || hits.Length == 0)
        {
            // No enemies in range -> reset state
            _currentTarget = null;
            _stayTimer = 0f;
            return;
        }

        // Find nearest enemy
        float bestSqr = float.MaxValue;
        Transform best = null;

        foreach (var col in hits)
        {
            if (!col) continue;

            float sqr = ((Vector2)col.transform.position - origin).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = col.transform;
            }
        }

        if (best == null)
        {
            _currentTarget = null;
            _stayTimer = 0f;
            return;
        }

        // Calculate attack direction to nearest enemy
        Vector2 dir = ((Vector2)best.position - origin).normalized;
        if (dir.sqrMagnitude < 0.0001f)
            dir = player.Facing; // fallback

        _lastAttackDir = dir;

        // If we switched target, reset stay timer
        if (_currentTarget != best)
        {
            _currentTarget = best;
            _stayTimer = 0f;
        }

        // Accumulate time that this target stays in range
        _stayTimer += deltaTime;
    }

    // ----------------------------------------------------------------------
    // Swing & animation
    // ----------------------------------------------------------------------

    /// <summary>
    /// Starts a swing:
    /// - sets next swing cooldown,
    /// - optionally drains oil (without blocking if oil is 0),
    /// - rotates weapon to face attackDir,
    /// - triggers swing animation.
    /// Actual hit is applied later via OnSwingHit().
    /// </summary>
    private void StartSwing(Vector2 attackDir)
    {
        float now = Time.time;

        // Set next time a new swing can start
        _nextSwingReadyTime = now + swingCooldown;

        // Optional oil cost: melee still works at 0 oil
        if (consumeOilOnSwing && oilLamp != null && oilCostPerSwing > 0f)
        {
            oilLamp.DrainOil(oilCostPerSwing);
        }

        // Reset stay timer; target must "earn" the next swing again
        _stayTimer = 0f;

        // Rotate weapon GameObject so visual slash faces attack direction
        if (attackDir.sqrMagnitude > 0.0001f)
        {
            float angleDeg = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
        }

        // Trigger swing animation or apply hit immediately if no animator
        if (animator && !string.IsNullOrEmpty(swingTriggerName))
        {
            animator.SetTrigger(swingTriggerName);
        }
        else
        {
            OnSwingHit();
        }
    }

    // ----------------------------------------------------------------------
    // Hit resolution
    // ----------------------------------------------------------------------

    /// <summary>
    /// Called from the swing animation (Animation Event).
    /// Applies damage to all enemies inside effective radius and, if Cone,
    /// also inside the cone angle in front of _lastAttackDir.
    /// </summary>
    /// <summary>
    /// Called from the swing animation (Animation Event).
    /// Applies damage to all enemies inside effective radius and, if Cone,
    /// also inside the cone angle in front of _lastAttackDir.
    /// </summary>
    public void OnSwingHit()
    {
        if (!player)
            return;

        Vector2 origin = player.transform.position;
        float radius = GetEffectiveRadius();

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, enemyMask);
        if (hits == null || hits.Length == 0)
            return;

        float halfCone = coneAngleDeg * 0.5f;

        foreach (var col in hits)
        {
            if (!col) continue;

            // Cone: lọc theo góc
            if (shape == MeleeShape.Cone)
            {
                Vector2 toTarget = ((Vector2)col.transform.position - origin).normalized;
                float angle = Vector2.Angle(_lastAttackDir, toTarget);
                if (angle > halfCone)
                    continue;
            }

            var hb = col.GetComponent<Hurtbox>();
            if (!hb || hb.enemyHealth == null)
                continue;

            // Damage = base * global * buff 1-hit
            float finalDamage = damagePerHit * damageMultiplier * _nextHitMultiplier;
            hb.enemyHealth.TakeDamage(finalDamage);

            // Knockback (nếu có)
            if (knockbackForce > 0f)
            {
                // lùi theo hướng enemy đang quay mặt
                Vector2 backDir = -(Vector2)hb.enemyHealth.transform.up;
                if (backDir.sqrMagnitude < 0.0001f)
                {
                    backDir = ((Vector2)hb.enemyHealth.transform.position - origin).normalized;
                }

                hb.enemyHealth.ApplyKnockback(backDir, knockbackForce);
            }
        }

        // reset buff one-shot
        _nextHitMultiplier = 1f;
    }




    // ----------------------------------------------------------------------
    // Auto-wire
    // ----------------------------------------------------------------------

    private void AutoWire()
    {
        // Player
        if (!player)
        {
            player = GetComponentInParent<PlayerController>();
            if (!player)
                player = FindAnyObjectByType<PlayerController>();
        }

        // OilLamp / Lantern
        if (!oilLamp)
        {
            oilLamp = GetComponentInParent<OilLamp>();
            if (!oilLamp)
                oilLamp = FindAnyObjectByType<OilLamp>();
        }

        // Animator trên weapon/child
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }



    // ----------------------------------------------------------------------
    // Gizmos
    // ----------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!player) return;

        Vector2 origin = player.transform.position;
        float radius = Application.isPlaying ? GetEffectiveRadius() : weaponRange;

        // Draw radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, radius);

        // Draw cone bounds if needed
        if (shape == MeleeShape.Cone)
        {
            Vector2 baseDir = Application.isPlaying ? _lastAttackDir : player.Facing;
            float half = coneAngleDeg * 0.5f;

            Vector2 left = Quaternion.Euler(0f, 0f, +half) * baseDir;
            Vector2 right = Quaternion.Euler(0f, 0f, -half) * baseDir;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + left * radius);
            Gizmos.DrawLine(origin, origin + right * radius);
        }
    }
#endif
}
