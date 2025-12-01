using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(120)]
[DisallowMultipleComponent]
public class OccluderFade2D : MonoBehaviour
{
    [Header("Refs")]
    public SortingGroup occluderGroup;           // trên prefab Cây (root)
    public SortingGroup playerGroup;             // SortingGroup của Player
    [Tooltip("Vùng tán: Trigger Collider2D bao bọc phần che. Ưu tiên gán tay.")]
    public Collider2D occlusionArea;             // ví dụ child: OcclusionArea (isTrigger)
    [Tooltip("Nếu không có occlusionArea, dùng bounds của sprite này để ước lượng.")]
    public SpriteRenderer fallbackAreaSprite;

    public enum BehindMode { YCompare, SortingOrder }
    public BehindMode behindMode = BehindMode.YCompare;

    [Tooltip("YCompare: playerY > occluderPivotY + bias → sau")]
    public float yBias = 0.02f;
    [Tooltip("SortingOrder: hysteresis để tránh nhấp nháy")]
    public int orderHysteresis = 2;

    [Header("Mờ/Hiện")]
    [Range(0f, 1f)] public float alphaWhenBehind = 0.45f;
    [Range(0f, 1f)] public float alphaWhenFront = 1.00f;
    [Tooltip("Tốc độ lerp alpha")]
    public float fadeSpeed = 12f;

    [Header("Renderer cần mờ (tán, cành, phần cao)")]
    public SpriteRenderer[] renderersToFade;

    [Header("Fallback bounds margin")]
    public Vector2 boundsMargin = new Vector2(0.05f, 0.05f); // nới nhẹ AABB khi không có collider

    Color[] baseColors;

    void Awake()
    {
        if (!occluderGroup) occluderGroup = GetComponent<SortingGroup>();
        if (!playerGroup)
        {
#if UNITY_2023_1_OR_NEWER
            var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
#else
            var pc = FindObjectOfType<PlayerController>();
#endif
            if (pc) playerGroup = pc.GetComponent<SortingGroup>();
        }

        if (renderersToFade == null || renderersToFade.Length == 0)
            renderersToFade = GetComponentsInChildren<SpriteRenderer>(true);

        baseColors = new Color[renderersToFade.Length];
        for (int i = 0; i < renderersToFade.Length; i++)
            if (renderersToFade[i]) baseColors[i] = renderersToFade[i].color;
    }

    void LateUpdate()
    {
        if (!playerGroup || !occluderGroup) return;

        Vector3 p = playerGroup.transform.position;
        Vector3 o = occluderGroup.transform.position;

        // 1) Player có ĐANG ở trong vùng tán (occlusion area) không?
        bool inOcclusion = IsInsideOcclusionArea(p);

        // 2) Player có ĐỨNG SAU cây không?
        bool behind = false;
        if (behindMode == BehindMode.YCompare)
        {
            behind = p.y > (o.y + yBias);
        }
        else // SortingOrder
        {
            behind = (playerGroup.sortingOrder < occluderGroup.sortingOrder - orderHysteresis);
        }

        // 3) Target alpha
        float targetA = (inOcclusion && behind) ? alphaWhenBehind : alphaWhenFront;

        // 4) Lerp alpha (tự khôi phục khi rời vùng / đi ra phía trước)
        for (int i = 0; i < renderersToFade.Length; i++)
        {
            var sr = renderersToFade[i];
            if (!sr) continue;
            var c = sr.color;
            c.a = Mathf.Lerp(c.a, targetA, Time.deltaTime * fadeSpeed);
            sr.color = c;
        }
    }

    bool IsInsideOcclusionArea(Vector3 playerPos)
    {
        // Ưu tiên Collider2D trigger
        if (occlusionArea)
            return occlusionArea.OverlapPoint(playerPos);

        // Fallback: dùng bounds của sprite tán + margin
        if (fallbackAreaSprite)
        {
            Bounds b = fallbackAreaSprite.bounds;
            b.Expand(new Vector3(boundsMargin.x, boundsMargin.y, 0f));
            // Bounds.Contains cần đúng z – chuyển player z về mặt phẳng bounds
            Vector3 p = new Vector3(playerPos.x, playerPos.y, b.center.z);
            return b.Contains(p);
        }

        // Nếu không có gì, coi như không trong vùng tán để tránh mờ sai
        return false;
    }
}
