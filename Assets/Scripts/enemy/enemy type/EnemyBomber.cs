using UnityEngine;
using System.Collections;
using Pathfinding;

// G?n lên Bomber (?ã có AIPath + AIDestinationSetter)
[DisallowMultipleComponent]
public class EnemyBomber : MonoBehaviour
{
    [Header("Refs")]
    public AIPath ai;
    public Transform player;

    [Header("Trigger")]
    public float triggerRadius = 2.8f;
    public float chargeTime = 1.2f;
    public bool resetOnExit = true;

    [Header("Explosion")]
    public float explosionRadius = 2.2f;
    [Tooltip("Layer có th? b? phá (vd: Fence)")]
    public LayerMask breakMask;                 // => thêm Fence vào ?ây
    public float destroyDelay = 0.02f;
    public GameObject explodeVfxPrefab;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color gizTrig = new(1f, .6f, .2f, .4f);
    public Color gizExp = new(1f, .2f, .2f, .4f);

    float stayTimer;
    bool exploding;

    void Reset()
    {
        ai ??= GetComponent<AIPath>();
#if UNITY_2023_1_OR_NEWER
        if (!player) { var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include); if (pc) player = pc.transform; }
#else
        if (!player) { var pc = FindObjectOfType<PlayerController>(); if (pc) player = pc.transform; }
#endif
    }

    void OnEnable() { stayTimer = 0f; exploding = false; }

    void Update()
    {
        if (!ai || !player || exploding) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= triggerRadius)
        {
            stayTimer += Time.deltaTime;
            if (stayTimer >= chargeTime) StartCoroutine(CoExplode());
        }
        else if (resetOnExit) stayTimer = 0f;
    }

    IEnumerator CoExplode()
    {
        exploding = true;
        if (ai) { ai.canMove = false; ai.canSearch = false; }

        if (explodeVfxPrefab) Instantiate(explodeVfxPrefab, transform.position, Quaternion.identity);

        // Phá rào: tìm m?i collider trong bán kính có layer breakMask,
        // ?u tiên g?i IBreakable/BreakableFence; n?u không có, Destroy GameObject g?c c?a collider.
        var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, breakMask);
        foreach (var h in hits)
        {
            if (!h) continue;

            // Tìm IBreakable/BreakableFence ? parent n?u collider là child
            var breakable = h.GetComponentInParent<IBreakable>();
            if (breakable != null)
            {
                breakable.Break();
                continue;
            }
            var fence = h.GetComponentInParent<BreakableFence>();
            if (fence != null)
            {
                fence.Break();
                continue;
            }

            // Fallback: phá tr?c ti?p object ch?a collider
            Destroy(h.attachedRigidbody ? h.attachedRigidbody.gameObject : h.gameObject);
        }
        // --- GÂY DAMAGE LÊN PLAYER TRONG BÁN KÍNH NỔ (1 tim = 2 half-hearts) ---
        var hitsP = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var h in hitsP)
        {
            if (!h) continue;

            // Tìm Hurtbox của Player rồi chuyển tiếp sang PlayerHealth
            var hb = h.GetComponentInParent<Hurtbox>();
            if (hb != null && hb.team == Hurtbox.Team.Player)
            {
                var ph = hb.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    // Đổi tên hàm theo PlayerHealth của bạn:
                    // ví dụ: ph.DamageHalfHearts(2); // 2 nửa tim = 1 tim
                    ph.TakeDamageHalfHearts(2);
                }
            }
        }

        // TODO: gây damage lên Player n?u ? trong bán kính (k?t n?i h? th?ng damage c?a b?n)

        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);

        // --- ADD: Gây damage lên Player trong bán kính nổ (1 tim = 2 half-hearts) ---
        int playerLayer = LayerMask.NameToLayer("Player"); // hoặc bỏ qua, ta quét Hurtbox
        var hits2 = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var h in hits2)
        {
            if (!h) continue;
            var hb = h.GetComponentInParent<Hurtbox>();
            if (hb != null && hb.team == Hurtbox.Team.Player)
            {
                hb.DamagePlayerHalfHearts(2); // 2 half-hearts = 1 tim
            }
        }

        


    }

    void OnDrawGizmosSelected() { if (drawGizmos) DrawG(); }
    void OnDrawGizmos() { if (drawGizmos) DrawG(); }
    void DrawG()
    {
        Vector3 p = transform.position;
        Gizmos.color = gizTrig; Gizmos.DrawWireSphere(p, triggerRadius);
        Gizmos.color = gizExp; Gizmos.DrawWireSphere(p, explosionRadius);
    }


}

// Interface chung ?? các lo?i v?t th? phá ???c có th? implement
public interface IBreakable { void Break(); }

// M?c ??nh rào: b?n có th? ?ã có, ?ây là b?n t?i gi?n
public class BreakableFence : MonoBehaviour, IBreakable
{
    public GameObject breakVfxPrefab;
    public AudioClip breakSfx;

    public void Break()
    {
        if (breakVfxPrefab) Instantiate(breakVfxPrefab, transform.position, Quaternion.identity);
        if (breakSfx) AudioSource.PlayClipAtPoint(breakSfx, transform.position, 0.8f);
        Destroy(gameObject);
    }
}
