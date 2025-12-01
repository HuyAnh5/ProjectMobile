using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LockOnController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;     // Facing
    [SerializeField] private Light2D auraLight;           // Point Light 2D (vòng)
    [SerializeField] private Light2D fovLight;            // Point Light 2D (nón)
    [SerializeField] private LayerMask enemyMask;         // Layer địch

    [Header("Aim")]
    [SerializeField] private float loseLockDistance = 25f;

    [Header("Scan weights")]
    [SerializeField] private float facingBias = 0.6f;         // 0..1: ưu tiên gần hướng
    [SerializeField] private float auraPriorityBoost = 0.5f;  // cộng điểm nếu trong Aura

    [Header("Feedback (tuỳ chọn)")]
    [SerializeField] private bool tintFovOnLock = true;
    [SerializeField] private Color fovLockedColor = new Color(1f, 0.92f, 0.5f, 1f); // vàng ấm khi lock
    private Color fovDefaultColor;

    public Transform CurrentTarget { get; private set; }
    public bool IsHolding { get; private set; } // đang lock (giữ tay hoặc giữ Q)


    void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        if (fovLight) fovDefaultColor = fovLight.color;
    }

    void Update()
    {
        // === BẮT PHÍM Q: giữ để lock, thả để hủy ===
        if (Input.GetKeyDown(KeyCode.Q)) BeginHold();
        if (Input.GetKeyUp(KeyCode.Q)) EndHold();

        // === Logic aim ===
        if (IsHolding)
        {
            if (!IsTargetValid(CurrentTarget))
                CurrentTarget = AcquireTarget();

            Vector2 dir = (CurrentTarget)
                ? ((Vector2)CurrentTarget.position - (Vector2)transform.position).normalized
                : (player ? player.Facing : Vector2.up);

            if (player) player.SetAimOverride(dir);

            float desiredDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }
        else
        {
            if (player) player.SetAimOverride(null);

            Vector2 dir = player ? player.Facing : Vector2.up;
            float desiredDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            CurrentTarget = null;
        }

    }

    public void BeginHold()
    {
        if (IsHolding) return;
        IsHolding = true;
        CurrentTarget = AcquireTarget();
        if (tintFovOnLock && fovLight) fovLight.color = fovLockedColor;
    }

    public void EndHold()
    {
        if (!IsHolding) return;
        IsHolding = false;
        CurrentTarget = null;
        if (player) player.SetAimOverride(null);
        if (tintFovOnLock && fovLight) fovLight.color = fovDefaultColor;
    }

    bool IsTargetValid(Transform t)
    {
        if (!t) return false;
        float dist = Vector2.Distance(transform.position, t.position);
        return dist <= loseLockDistance && t.gameObject.activeInHierarchy;
    }

    Transform AcquireTarget()
    {
        Vector2 origin = transform.position;

        float rFov = fovLight ? fovLight.pointLightOuterRadius : 6f;
        float rAura = auraLight ? auraLight.pointLightOuterRadius : 1.6f;
        float scanR = Mathf.Max(rFov, rAura);

        var hits = Physics2D.OverlapCircleAll(origin, scanR, enemyMask);
        if (hits == null || hits.Length == 0) return null;

        float halfAngle = fovLight ? fovLight.pointLightOuterAngle * 0.5f : 45f;
        Vector2 facing = player ? player.Facing : Vector2.up;

        Transform best = null;
        float bestScore = float.NegativeInfinity;

        foreach (var h in hits)
        {
            Transform t = h.transform;
            Vector2 toT = (Vector2)t.position - origin;
            float dist = toT.magnitude;
            if (dist < 0.0001f) continue;

            bool inAura = dist <= rAura + 0.0001f;
            float ang = Vector2.Angle(facing, toT);
            bool inCone = ang <= halfAngle + 0.001f;
            if (!inAura && !inCone) continue;

            float cos = Mathf.Cos(ang * Mathf.Deg2Rad);
            float dirScore = Mathf.Lerp(-1f, 1f, (cos + 1f) * 0.5f);
            float distScore = 1f - Mathf.Clamp01(dist / scanR);

            float score = facingBias * dirScore + (1f - facingBias) * distScore;
            if (inAura) score += auraPriorityBoost;

            if (score > bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!fovLight || !player) return;
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, fovLight.pointLightOuterRadius);
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, auraLight ? auraLight.pointLightOuterRadius : 1.6f);
    }
#endif
}
