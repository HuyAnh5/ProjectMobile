using UnityEngine;
using System.Collections;
using Pathfinding;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class EnemyPouncer : MonoBehaviour
{
    [Header("Refs")]
    public AIPath ai;
    public Transform player;

    [Header("Trigger")]
    public float pounceTriggerRadius = 3.0f;   // vào tầm này sẽ cố gắng lao
    public float cooldown = 2.0f;              // hồi chiêu sau khi lao thành công

    [Header("Trajectory")]
    public float minJump = 2.5f;               // quãng lao tối thiểu
    public float maxJump = 3.5f;               // quãng lao tối đa
    [Tooltip("Độ lệch góc tối đa quanh hướng tới player (độ). 15° ~ 11.5h–12.5h")]
    public float maxAngleOffsetDeg = 15f;      // ±15° làm người chơi khó đoán
    public float pounceSpeed = 12f;            // tốc độ lao (m/s)
    public float skin = 0.05f;                 // trừ biên khi check chạm

    [Header("Blocking check (không lao nếu bị chặn)")]
    [Tooltip("Tường/cây cứng chặn pounce")]
    public LayerMask wallMask;                 // TreeSolid | Walls
    [Tooltip("Địch khác chặn pounce (không tính Player)")]
    public LayerMask enemyMask;                // Enemy
    [Tooltip("Bán kính CircleCast để phát hiện vật chặn/địch phía trước")]
    public float frontClearRadius = 0.22f;
    [Tooltip("Đẩy điểm bắt đầu để không tự va vào collider của chính mình")]
    public float startInset = 0.12f;           // tăng chút để chắc chắn không ăn vào self

    [Header("Vault over Fence")]
    [Tooltip("Layer KHÔNG va chạm với Fence trong lúc lao (vẫn va Player)")]
    public string enemyVaultLayer = "EnemyVault";
    int _originalLayer;

    [Header("Damage while pouncing (tuỳ chọn)")]
    [Tooltip("Bật collider/hitbox này chỉ trong lúc lao để gây sát thương khi lướt qua Player")]
    public Collider2D pounceHitbox;

    [Header("After pounce slow-down")]
    public float slowMultiplier = 0.35f;       // % tốc độ cơ bản trong lúc slow
    public float slowDuration = 0.8f;        // thời gian slow

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logWhyBlocked = false;
    public Color gizTrig = new(1f, .5f, .2f, .35f);
    public Color gizPath = new(.9f, .9f, .2f, .9f);
    public Color gizBlockedWall = new(1f, .2f, .2f, .9f);
    public Color gizBlockedEnemy = new(1f, .6f, .2f, .9f);

    bool cooling;
    float baseSpeed;

    // cache collider của chính mình để bỏ qua khi cast
    Collider2D[] ownCols;

    void Reset()
    {
        ai ??= GetComponent<AIPath>();
#if UNITY_2023_1_OR_NEWER
        if (!player) { var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include); if (pc) player = pc.transform; }
#else
        if (!player) { var pc = FindObjectOfType<PlayerController>(); if (pc) player = pc.transform; }
#endif
    }

    void Awake()
    {
        ownCols = GetComponentsInChildren<Collider2D>(true);
    }

    void OnEnable()
    {
        baseSpeed = ai ? ai.maxSpeed : 2f;
        cooling = false;
        if (pounceHitbox) pounceHitbox.enabled = false;
    }

    void Update()
    {
        if (!ai || !player || cooling) return;

        // chỉ cố lao khi trong tầm
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > pounceTriggerRadius) return;

        if (CanPounceNow(out Vector2 dirWithOffset, out float jumpDist))
        {
            StartCoroutine(CoPounce(dirWithOffset, jumpDist));
        }
        // nếu bị chặn → không vào cooldown, đợi tới khi thoáng
    }

    bool CanPounceNow(out Vector2 dirWithOffset, out float jumpDist)
    {
        dirWithOffset = Vector2.zero;
        jumpDist = 0f;

        Vector2 start = transform.position;
        Vector2 toPlayer = (Vector2)player.position - start;
        if (toPlayer.sqrMagnitude < 0.0001f) return false;

        // Hướng tới player + lệch góc ngẫu nhiên nhỏ
        float deltaDeg = Random.Range(-maxAngleOffsetDeg, maxAngleOffsetDeg);
        Vector2 dir = Rotate(toPlayer.normalized, deltaDeg);

        // Quãng lao ngẫu nhiên
        float desired = Random.Range(minJump, maxJump);

        // Xuất phát hơi tiến lên một chút để chắc chắn không "ăn" collider của chính mình
        Vector2 castStart = start + dir * Mathf.Max(0f, startInset);

        // --- A) Wall/Tree chặn? (Raycast) ---
        var hitWall = Physics2D.Raycast(castStart, dir, desired + skin, wallMask);
        if (hitWall.collider && !IsOwnCollider(hitWall.collider))
        {
            if (logWhyBlocked) Debug.Log($"[Pouncer] Blocked by WALL/TREE: {hitWall.collider.name}");
            lastCastA = (castStart, dir, desired);
            lastBlocked = 1;
            return false;
        }

        // --- B) Enemy phía trước? (CircleCastAll) ---
        var hits = Physics2D.CircleCastAll(castStart, frontClearRadius, dir, desired, enemyMask);
        foreach (var h in hits)
        {
            if (!h.collider) continue;
            if (IsOwnCollider(h.collider)) continue;               // bỏ qua self
            if (player && h.collider.transform == player) continue; // không coi Player là chướng ngại
            // Có enemy khác chặn đường → không lao
            if (logWhyBlocked) Debug.Log($"[Pouncer] Blocked by ENEMY: {h.collider.name}");
            lastCastB = (castStart, dir, desired, h.point);
            lastBlocked = 2;
            return false;
        }

        // OK
        dirWithOffset = dir;
        jumpDist = desired;
        lastCastA = (castStart, dir, desired);
        lastCastB = (castStart, dir, desired, castStart + dir * desired);
        lastBlocked = 0;
        return true;
    }

    IEnumerator CoPounce(Vector2 dir, float distance)
    {
        cooling = true;

        // Tắt AIPath để tự điều khiển vị trí
        if (ai) ai.canMove = false;

        // Đổi layer để xuyên Fence khi lao
        _originalLayer = gameObject.layer;
        int vaultLayer = LayerMask.NameToLayer(enemyVaultLayer);
        if (vaultLayer >= 0) gameObject.layer = vaultLayer;

        Vector2 start = transform.position;
        Vector2 end = start + dir * distance;

        // Bật hitbox gây damage trong lúc lao (nếu có)
        if (pounceHitbox) pounceHitbox.enabled = true;

        // Lao với tốc độ cố định
        float t = 0f;
        float time = distance / Mathf.Max(0.01f, pounceSpeed);
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            transform.position = Vector2.Lerp(start, end, k);
            yield return null;
        }

        if (pounceHitbox) pounceHitbox.enabled = false;

        // Trả layer & AI
        gameObject.layer = _originalLayer;
        if (ai) ai.canMove = true;

        // Giảm tốc mạnh trong thời gian ngắn rồi khôi phục
        float oldSpeed = ai.maxSpeed;
        ai.maxSpeed = baseSpeed * slowMultiplier;
        yield return new WaitForSeconds(Mathf.Max(0f, slowDuration));
        ai.maxSpeed = baseSpeed;

        // Cooldown sau khi lao THÀNH CÔNG
        yield return new WaitForSeconds(Mathf.Max(0.1f, cooldown));
        cooling = false;
    }

    bool IsOwnCollider(Collider2D col)
    {
        if (!col || ownCols == null) return false;
        for (int i = 0; i < ownCols.Length; i++)
            if (ownCols[i] == col) return true;
        return false;
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    // --- Gizmos / Debug casts ---
    (Vector2 start, Vector2 dir, float dist) lastCastA;            // wall ray
    (Vector2 start, Vector2 dir, float dist, Vector2 hit) lastCastB; // enemy circle
    int lastBlocked = 0; // 0=clear, 1=wall, 2=enemy

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        // Trigger radius
        Gizmos.color = gizTrig;
        Gizmos.DrawWireSphere(transform.position, pounceTriggerRadius);

        // Preview 3 tia (mid, ±offset) for illustration only
#if UNITY_EDITOR
        if (player)
        {
            Vector2 toP = (Vector2)player.position - (Vector2)transform.position;
            if (toP.sqrMagnitude > 0.0001f)
            {
                float deg = maxAngleOffsetDeg;
                Vector2 dirL = Rotate(toP.normalized, -deg);
                Vector2 dirR = Rotate(toP.normalized, +deg);
                Vector2 mid = toP.normalized;
                float dist = Mathf.Lerp(minJump, maxJump, 0.5f);
                Gizmos.color = gizPath;
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + mid * dist);
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + dirL * dist);
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + dirR * dist);
            }
        }
#endif
        // Last casts (why blocked)
        if (lastCastA.dist > 0f)
        {
            Gizmos.color = (lastBlocked == 1) ? gizBlockedWall : gizPath;
            Gizmos.DrawLine(lastCastA.start, lastCastA.start + lastCastA.dir * lastCastA.dist);
        }
        if (lastCastB.dist > 0f)
        {
            Gizmos.color = (lastBlocked == 2) ? gizBlockedEnemy : gizPath;
            // vẽ “ống” cast
            Vector3 a = lastCastB.start;
            Vector3 b = lastCastB.start + lastCastB.dir * lastCastB.dist;
            Gizmos.DrawLine(a, b);
            // điểm hit ước lượng
            Gizmos.DrawWireSphere(lastCastB.hit, frontClearRadius * 0.6f);
        }
    }
}
