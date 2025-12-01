using UnityEngine;
using Pathfinding;
using System.Collections.Generic;
using System.Reflection;

[DisallowMultipleComponent]
public class EnemyRunner : MonoBehaviour
{
    [Header("Refs")]
    public AIPath ai;
    public Transform player;

    [Header("Base & Control")]
    public bool useAiMaxAsBase = true;   // lấy base từ AIPath.maxSpeed
    public float baseSpeedInspector = 1.8f;
    public bool takeControlEveryFrame = true; // ép ghi đè mỗi frame
    public bool resyncBaseWhenIdle = true;    // khi không boost, cho chỉnh base trực tiếp trên AIPath

    [Header("Boost")]
    public float boostMultiplier = 2f;
    public float enterRadius = 6f;
    public float exitRadius = 8f;
    public float speedLerp = 0f;        // <=0 = đặt tức thì
    public bool requireLOSForBoost = false;
    public LayerMask losBlockMask;
    public float losInset = 0.06f;

    [Header("Visual")]
    public SpriteRenderer[] visuals;      // để trống sẽ auto-collect
    public Color normalColor = Color.white;
    public Color boostedColor = new(1f, 0.35f, 0.35f, 1f);
    public float colorLerp = 12f;         // <=0 = đổi màu tức thì

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color gizEnter = new(0.2f, 1f, 0.2f, 0.35f);
    public Color gizExit = new(0.2f, 0.6f, 1f, 0.30f);

    float baseSpeed;
    bool boosted;
    float targetSpeed;

    // mọi field tốc độ phổ biến ở component khác
    readonly string[] speedFieldNames = { "speedOverride", "moveSpeed", "speed", "maxSpeed", "Speed" };
    readonly List<(object obj, FieldInfo fi)> externalSpeedFields = new();

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
        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<SpriteRenderer>(true);

        CollectExternalSpeedFields();
    }

    void OnEnable()
    {
        if (!ai) ai = GetComponent<AIPath>();

        baseSpeed = useAiMaxAsBase ? ai.maxSpeed : baseSpeedInspector;
        boosted = false;
        targetSpeed = baseSpeed;

        // đặt ngay tốc độ ban đầu lên tất cả
        ApplySpeedToAll(baseSpeed);

        // reset màu
        foreach (var sr in visuals) if (sr) sr.color = normalColor;
    }

    void Update()
    {
        if (!ai || !player) return;

        // --- trạng thái trước khi tính ---
        bool wasBoosted = boosted;

        // --- xác định vào/ra vùng ---
        float dist = Vector2.Distance(transform.position, player.position);
        bool losOK = !requireLOSForBoost || HasLOS(transform.position, player.position);

        if (boosted) { if (dist >= exitRadius || !losOK) boosted = false; }
        else { if (dist <= enterRadius && losOK) boosted = true; }

        // --- xử lý chuyển trạng thái ---
        // 1) Vừa RỜI vùng boost -> reset NGAY về base, không MoveTowards, không tích lũy.
        if (wasBoosted && !boosted)
        {
            ApplySpeedToAll(baseSpeed);
            // cho phép chỉnh base live sau khi đã reset (nếu bạn bật tuỳ chọn này)
            if (resyncBaseWhenIdle && useAiMaxAsBase) baseSpeed = ai.maxSpeed;
        }
        else
        {
            // 2) Đang ở trong trạng thái hiện tại
            if (!boosted && resyncBaseWhenIdle && useAiMaxAsBase)
                baseSpeed = ai.maxSpeed; // chỉ khi đang KHÔNG boost

            float want = boosted ? baseSpeed * boostMultiplier : baseSpeed;

            // Vừa VÀO vùng boost -> nhảy ngay (nếu speedLerp <=0 thì cũng là nhảy ngay)
            bool justEntered = (!wasBoosted && boosted);
            float newSpeed = (speedLerp <= 0f || justEntered)
                ? want
                : Mathf.MoveTowards(ai.maxSpeed, want, speedLerp * Time.deltaTime);

            ApplySpeedToAll(newSpeed);
        }

        // --- màu nhận biết ---
        Color wantCol = boosted ? boostedColor : normalColor;
        foreach (var sr in visuals)
        {
            if (!sr) continue;
            sr.color = (colorLerp <= 0f) ? wantCol : Color.Lerp(sr.color, wantCol, colorLerp * Time.deltaTime);
        }
    }


    // ép tốc độ lên AIPath + mọi script di chuyển khác (kể cả speedOverride)
    void ApplySpeedToAll(float s)
    {
        if (ai) ai.maxSpeed = s;
        for (int i = 0; i < externalSpeedFields.Count; i++)
        {
            var (obj, fi) = externalSpeedFields[i];
            if (obj == null || fi == null) continue;
            try { fi.SetValue(obj, s); } catch { }
        }
    }

    void CollectExternalSpeedFields()
    {
        externalSpeedFields.Clear();
        var comps = GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (!c || ReferenceEquals(c, this) || c is AIPath) continue;

            var type = c.GetType();
            foreach (var name in speedFieldNames)
            {
                var fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null && fi.FieldType == typeof(float))
                    externalSpeedFields.Add((c, fi));
            }
        }
    }

    bool HasLOS(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from; float m = d.magnitude;
        if (m < 1e-4f) return true;
        d /= m;
        var hit = Physics2D.Raycast(from + d * losInset, d, m - losInset, losBlockMask);
        return !hit;
    }

    void OnDrawGizmosSelected() { if (drawGizmos) DrawG(); }
    void OnDrawGizmos() { if (drawGizmos) DrawG(); }
    void DrawG()
    {
        var p = transform.position;
        Gizmos.color = gizEnter; Gizmos.DrawWireSphere(p, enterRadius);
        Gizmos.color = gizExit; Gizmos.DrawWireSphere(p, exitRadius);
    }
}
