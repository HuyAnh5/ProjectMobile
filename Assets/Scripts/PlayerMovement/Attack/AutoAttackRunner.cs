using UnityEngine;
using UnityEngine.Rendering.Universal;

public class AutoAttackRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform fireOrigin;      // Kéo Lantern
    [SerializeField] private OilLamp oilLamp;           // Kéo OilLamp (trên Lantern)
    [SerializeField] private LockOnController lockOn;   // (tuỳ)


    [Header("Module")]
    [SerializeField] private AttackModule module;

    [Header("External modifiers")]
    [Tooltip("Global multiplier for ranged damage from items. 1 = no change.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Global fire-rate multiplier. 1 = dùng đúng fireInterval trong AttackModule.")]
    [SerializeField] private float fireRateMultiplier = 1f;

    [Tooltip("Multiplier cho tốc độ đạn (projectileSpeed).")]
    [SerializeField] private float projectileSpeedMultiplier = 1f;

    [Tooltip("Multiplier cho thời gian sống của đạn (projectileLifetime).")]
    [SerializeField] private float projectileLifetimeMultiplier = 1f;

    [Header("Knockback")]
    [Tooltip("Knockback cơ bản của vũ khí tầm xa (không có item).")]
    [SerializeField] private float baseKnockbackForce = 0f;

    [Tooltip("Knockback cộng thêm từ item / curse.")]
    [SerializeField] private float knockbackBonus = 0f;

    public float FireRateMultiplier
    {
        get => fireRateMultiplier;
        set => fireRateMultiplier = Mathf.Max(0.01f, value);
    }

    public float ProjectileSpeedMultiplier
    {
        get => projectileSpeedMultiplier;
        set => projectileSpeedMultiplier = Mathf.Max(0.01f, value);
    }

    public float ProjectileLifetimeMultiplier
    {
        get => projectileLifetimeMultiplier;
        set => projectileLifetimeMultiplier = Mathf.Max(0.01f, value);
    }

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

    /// <summary>Tổng knockback hiện tại = base + bonus (cho item chỉnh).</summary>
    public float TotalKnockbackForce => Mathf.Max(0f, baseKnockbackForce + knockbackBonus);


    // Multiplier chỉ áp cho viên đạn tiếp theo (cho các effect kiểu “next shot x3” nếu sau này cần)
    private float _nextShotMultiplier = 1f;

    public float DamageMultiplier
    {
        get => damageMultiplier;
        set => damageMultiplier = Mathf.Max(0f, value);
    }

    /// <summary>Được item gọi để buff viên đạn kế tiếp (vd: 300%).</summary>
    public void ApplyNextShotMultiplier(float multiplier)
    {
        if (multiplier > _nextShotMultiplier)
            _nextShotMultiplier = multiplier;
    }

    [Header("Oil / Weapon Upkeep")]
    [SerializeField, Min(0f)] private float oilConsumePerSec = 0.5f;

    [Header("Fire Rules")]
    [SerializeField, Min(0.01f)] private float oilCostPerShot = 1.0f;
    [SerializeField] private LayerMask enemyMask;       // Layer=Enemy
    [SerializeField] private bool alignProjectileRotation = true;

    public enum FovAxis { Up, Right, PlayerFacing }
    [SerializeField] private FovAxis fovAxis = FovAxis.Up;
    [SerializeField] private float axisOffsetDeg = 0f;
    [SerializeField] private float angleGraceDeg = 2f;
    [SerializeField] private float radiusGrace = 0.1f;

    [Header("Line of Sight (LOS)")]
    [Tooltip("Những layer CHẮN TẦM NHÌN (ví dụ TreeSolid, Walls). KHÔNG chọn Fence nếu muốn bắn xuyên rào.")]
    [SerializeField] private LayerMask losBlockMask;
    [Tooltip("Dò LOS dùng CircleCast (bán kính ≈ bán kính đạn)")]
    [SerializeField] private bool losUseCircleCast = true;
    [Tooltip("Bán kính CircleCast cho LOS (0 = ray mảnh)")]
    [SerializeField] private float losRadius = 0.08f;
    [Tooltip("Nới nhỏ khoảng bắt đầu để khỏi ‘tự va’ lúc đứng sát tường/cây")]
    [SerializeField] private float losStartInset = 0.02f;

    [Header("Toggle / Debug")]
    [SerializeField] private bool enabledModule = true;

    public void SetFiringAllowed(bool allowed)
    {
        enabledModule = allowed;
    }

    public bool IsFiringAllowed()
    {
        return enabledModule;
    }


    // chống “xả đạn dồn”
    private float nextFireTime = 0f;

    // ----- Gizmos -----
    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizFovAxis = new Color(0.3f, 0.8f, 1f, 0.9f);
    [SerializeField] private Color gizLosClear = new Color(0.2f, 1f, 0.2f, 0.9f);
    [SerializeField] private Color gizLosBlocked = new Color(1f, 0.2f, 0.2f, 0.9f);

    Vector2 gizLastFrom, gizLastTo;
    bool gizLastLosClear;

    void Reset()
    {
        if (!player) player = GetComponent<PlayerController>();
#if UNITY_2023_1_OR_NEWER
        if (!fireOrigin) { var lf = Object.FindFirstObjectByType<LanternFollow>(FindObjectsInactive.Include); if (lf) fireOrigin = lf.transform; }
        if (!oilLamp) { oilLamp = Object.FindFirstObjectByType<OilLamp>(FindObjectsInactive.Include); }
        if (!lockOn) { lockOn = Object.FindFirstObjectByType<LockOnController>(FindObjectsInactive.Include); }
        AutoWire();
#else
        if (!fireOrigin) { var lf = FindObjectOfType<LanternFollow>(); if (lf) fireOrigin = lf.transform; }
        if (!oilLamp)    { oilLamp = FindObjectOfType<OilLamp>(); }
        if (!lockOn)     { lockOn = FindObjectOfType<LockOnController>(); }
#endif
    }
    void Awake()
    {
        AutoWire();
        if (module != null)
            baseKnockbackForce = module.baseKnockback;
    }

    void Update()
    {
        if (!enabledModule || !module || !player || !fireOrigin || !oilLamp) return;
        if (Time.time < nextFireTime) return;

        // hết dầu thì khỏi bắn
        if (oilLamp.current <= 0.001f)
            return;

        // lấy hướng bắn (FOV + LOS)
        if (!TryGetAimDirectionWithLOS(out Vector2 shootDir))
            return;

        FireOnce(shootDir);

        // FireRateMultiplier > 1 → bắn nhanh hơn (khoảng cách giữa các viên nhỏ lại)
        float effectiveInterval = module.fireInterval / Mathf.Max(0.01f, fireRateMultiplier);
        nextFireTime = Time.time + Mathf.Max(0.01f, effectiveInterval);

    }




    void AutoWire()
    {
         // 1) Player
        if (!player)
        {
            player = GetComponentInParent<PlayerController>();
            if (!player)
                player = FindAnyObjectByType<PlayerController>();
        }

        // 2) OilLamp / Lantern
        if (!oilLamp)
        {
            oilLamp = GetComponentInParent<OilLamp>();
            if (!oilLamp)
                oilLamp = FindAnyObjectByType<OilLamp>();
        }

        // 3) FireOrigin: ưu tiên transform của OilLamp (Lantern)
        if (!fireOrigin)
        {
            if (player)
                fireOrigin = player.transform;         // bắn từ người Player
            else
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc) fireOrigin = pc.transform;
            }
        }


        // 4) LockOn
        if (!lockOn)
        {
            lockOn = GetComponentInParent<LockOnController>();
            if (!lockOn)
                lockOn = FindAnyObjectByType<LockOnController>();
        }
    }



    // ====== AIM + LOS ======
    bool TryGetAimDirectionWithLOS(out Vector2 dir)
    {
        dir = Vector2.zero;
        gizLastLosClear = false;
        if (oilLamp.turnOffFOVAtZero && oilLamp.current <= 0.0001f) return false;

        var fov = oilLamp.fovLight;
        if (!fov) return false;

        float rFov = fov.pointLightOuterRadius + Mathf.Max(0f, radiusGrace);
        float halfAngle = fov.pointLightOuterAngle * 0.5f;

        Vector2 origin = fireOrigin.position;
        Vector2 axis = GetFovForward(fov).normalized;
        if (axis.sqrMagnitude < 0.0001f) axis = (player && player.Facing.sqrMagnitude > 0.0001f) ? player.Facing.normalized : Vector2.up;
        if (Mathf.Abs(axisOffsetDeg) > 0.001f) axis = Rotate(axis, axisOffsetDeg).normalized;

        // 1) Ưu tiên mục tiêu LOCK nếu còn trong FOV và CÓ LOS
        if (lockOn && lockOn.CurrentTarget)
        {
            var t = lockOn.CurrentTarget;
            if (t && t.gameObject.activeInHierarchy && IsInFOV(origin, axis, t.position, rFov, halfAngle, angleGraceDeg))
            {
                if (HasLOS(origin, (Vector2)t.position))
                {
                    dir = ((Vector2)t.position - origin).normalized;
                    gizLastFrom = origin; gizLastTo = t.position; gizLastLosClear = true;
                    return true;
                }
                else
                {
                    // lưu gizmo blocked
                    gizLastFrom = origin; gizLastTo = t.position; gizLastLosClear = false;
                }
            }
        }

        // 2) Tìm mục tiêu TỐT NHẤT trong FOV và CÓ LOS
        var hits = Physics2D.OverlapCircleAll(origin, rFov, enemyMask);
        if (hits == null || hits.Length == 0) return false;

        Transform best = null;
        float bestScore = float.NegativeInfinity;

        float cosThreshold = Mathf.Cos(Mathf.Deg2Rad * Mathf.Min(180f, halfAngle + Mathf.Max(0f, angleGraceDeg)));

        foreach (var h in hits)
        {
            var t = h.attachedRigidbody ? h.attachedRigidbody.transform : h.transform;
            if (!t || !t.gameObject.activeInHierarchy) continue;

            Vector2 toT = (Vector2)t.position - origin;
            float dist = toT.magnitude;
            if (dist < 0.0001f || dist > rFov + 0.0001f) continue;

            Vector2 dNorm = toT / dist;
            float dot = Vector2.Dot(axis, dNorm);
            if (halfAngle < 179.5f && dot < cosThreshold) continue; // ngoài nón

            // LOS: chỉ chấp nhận mục tiêu nếu KHÔNG có vật cản giữa Player và Enemy
            if (!HasLOS(origin, (Vector2)t.position))
            {
                // vẽ gizmo blocked cho mục tiêu bị chặn
                gizLastFrom = origin; gizLastTo = t.position; gizLastLosClear = false;
                continue;
            }

            // chấm điểm: bám trục nón + hơi ưu tiên gần
            float axisScore = dot;
            float distScore = 1f - Mathf.Clamp01(dist / rFov);
            float score = 0.75f * axisScore + 0.25f * distScore;

            if (score > bestScore) { bestScore = score; best = t; }
        }

        if (!best) return false;

        dir = ((Vector2)best.position - origin).normalized;
        gizLastFrom = origin; gizLastTo = best.position; gizLastLosClear = true;
        return true;
    }

    bool HasLOS(Vector2 origin, Vector2 target)
    {
        Vector2 dir = (target - origin);
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return true;
        dir /= dist;

        // đẩy điểm bắt đầu một xíu để không ‘ăn’ chính collider ở chân
        Vector2 start = origin + dir * Mathf.Max(0f, losStartInset);
        float remain = Mathf.Max(0f, dist - losStartInset);

        RaycastHit2D hit;
        if (losUseCircleCast && losRadius > 0f)
            hit = Physics2D.CircleCast(start, losRadius, dir, remain, losBlockMask);
        else
            hit = Physics2D.Raycast(start, dir, remain, losBlockMask);

        // nếu trúng "đồ vật chắn" ⇒ KHÔNG có LOS
        return hit.collider == null;
    }

    // ====== helpers ======
    static bool IsInFOV(Vector2 origin, Vector2 axis, Vector2 targetPos, float rFov, float halfAngle, float graceDeg)
    {
        Vector2 toT = targetPos - origin;
        float dist = toT.magnitude;
        if (dist < 0.0001f || dist > rFov + 0.0001f) return false;
        if (halfAngle >= 179.5f) return true;
        float dot = Vector2.Dot(axis.normalized, toT / dist);
        float cosThreshold = Mathf.Cos(Mathf.Deg2Rad * Mathf.Min(180f, halfAngle + Mathf.Max(0f, graceDeg)));
        return dot >= cosThreshold;
    }

    Vector2 GetFovForward(Light2D fov)
    {
        return fovAxis switch
        {
            FovAxis.Right => (Vector2)fov.transform.right,
            FovAxis.PlayerFacing => (player && player.Facing.sqrMagnitude > 0.0001f) ? player.Facing : (Vector2)fov.transform.up,
            _ => (Vector2)fov.transform.up
        };
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r); float sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    void FireOnce(Vector2 dir)
    {
        if (!module || !fireOrigin) return;

        Quaternion rot = alignProjectileRotation
            ? Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f)
            : Quaternion.identity;

        var go = Instantiate(module.projectilePrefab, fireOrigin.position, rot);

        // --- Rigidbody / tốc độ bay ---
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            float speed = module.projectileSpeed * projectileSpeedMultiplier;
            rb.linearVelocity = dir.normalized * speed;
        }

        // --- Lifetime ---
        float life = module.projectileLifetime * projectileLifetimeMultiplier;
        if (life > 0f) Destroy(go, life);

        // --- Damage + Knockback gán vào DamageDealer ---
        var dd = go.GetComponent<DamageDealer>();
        if (dd)
        {
            float finalDamage = module.damage * damageMultiplier * _nextShotMultiplier;
            float finalKnockback = TotalKnockbackForce; // hoặc + module.baseKnockback nếu bạn thêm field đó

            ApplyDamageTeamAndKnockback(dd, finalDamage, module.team, finalKnockback);
        }

        // viên này xong thì reset buff "next shot"
        _nextShotMultiplier = 1f;

        // nếu vẫn muốn trừ dầu/viên:
        if (oilLamp && oilCostPerShot > 0f)
            oilLamp.DrainOil(oilCostPerShot);
    }


    static void ApplyDamageTeamAndKnockback(DamageDealer dd, float dmg, DamageDealer.Team team, float knockback)
    {
        var t = dd.GetType();
        var fDamage = t.GetField("damage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var fTeam = t.GetField("team", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var fKnock = t.GetField("knockbackForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (fDamage != null) fDamage.SetValue(dd, Mathf.Max(0f, dmg));
        if (fTeam != null) fTeam.SetValue(dd, team);
        if (fKnock != null) fKnock.SetValue(dd, Mathf.Max(0f, knockback));
    }


    void OnEnable()
    {
        AutoWire(); // đảm bảo oilLamp/player/fireOrigin đã có trước khi register drain
        if (oilLamp && oilConsumePerSec > 0f)
            oilLamp.RegisterExtraDrain(oilConsumePerSec);
    }

    void OnDisable()
    {
        if (oilLamp && oilConsumePerSec > 0f)
            oilLamp.UnregisterExtraDrain(oilConsumePerSec);
    }


    // ====== Gizmos ======
    void OnDrawGizmos()
    {
        if (!drawGizmos || !oilLamp || !oilLamp.fovLight || !fireOrigin) return;

        // vẽ trục nón
        var axis = GetFovForward(oilLamp.fovLight).normalized;
        if (Mathf.Abs(axisOffsetDeg) > 0.001f) axis = Rotate(axis, axisOffsetDeg).normalized;
        float r = oilLamp.fovLight.pointLightOuterRadius;
        Vector3 from = fireOrigin.position;
        Gizmos.color = gizFovAxis;
        Gizmos.DrawLine(from, from + (Vector3)axis * r);

        // vẽ LOS của mục tiêu vừa chọn/đánh giá gần nhất
        if (gizLastTo != Vector2.zero)
        {
            Gizmos.color = gizLastLosClear ? gizLosClear : gizLosBlocked;
            Gizmos.DrawLine(gizLastFrom, gizLastTo);
            if (losUseCircleCast && losRadius > 0f)
            {
                // vẽ vòng tròn nhỏ tại đầu đường để thấy bán kính LOS
                Gizmos.DrawWireSphere(gizLastFrom, losRadius);
                Gizmos.DrawWireSphere(gizLastTo, losRadius * 0.7f);
            }
        }
    }
}
