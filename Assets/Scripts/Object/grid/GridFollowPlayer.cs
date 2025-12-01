using UnityEngine;
using Pathfinding; // đảm bảo có dòng này

public class GridFollowPlayer : MonoBehaviour
{
    public Transform player;
    public float moveThreshold = 10f;
    public float snapStep = 1f;
    public float minScanInterval = 0.75f;

    Vector3 lastCenter;
    float nextScanTime;

    void Start()
    {
        if (player == null)
            player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include)?.transform;

        var gg = AstarPath.active?.data?.gridGraph;
        if (gg != null) lastCenter = gg.center;   // ← dùng gg != null
    }

    void Update()
    {
        var gg = AstarPath.active?.data?.gridGraph;
        if (gg == null || player == null) return; // ← sửa dòng này

        Vector3 target = gg.center;
        target.x = Mathf.Round(player.position.x / snapStep) * snapStep;
        target.y = Mathf.Round(player.position.y / snapStep) * snapStep;

        if ((target - lastCenter).sqrMagnitude >= moveThreshold * moveThreshold
            && Time.time >= nextScanTime)
        {
            gg.center = target;
            AstarPath.active.Scan();
            lastCenter = target;
            nextScanTime = Time.time + minScanInterval;
        }
    }
}



public static class AstarGraphUpdater
{
    public static void UpdateFor(GameObject go, float extraPadding = 0.05f)
    {
        if (AstarPath.active == null || !go) return;

        // Gộp bounds của tất cả Collider2D con (nếu có nhiều)
        var cols = go.GetComponentsInChildren<Collider2D>();
        if (cols != null && cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            b.Expand(extraPadding);
            AstarPath.active.UpdateGraphs(b);
            return;
        }

        // fallback: renderer bounds
        var r = go.GetComponentInChildren<Renderer>();
        if (r)
        {
            Bounds b = r.bounds; b.Expand(extraPadding);
            AstarPath.active.UpdateGraphs(b);
            return;
        }

        // tối thiểu: 1×1 quanh transform
        var bb = new Bounds(go.transform.position, Vector3.one * (1f + extraPadding));
        AstarPath.active.UpdateGraphs(bb);
    }
}
