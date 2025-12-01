/*
 * MoveAI.cs
 * * Script này di chuyển một đối tượng AI (kẻ địch) 2D theo đường dẫn A* (A* Pathfinding Project).
 * Nó sử dụng logic "làm mượt hướng" (direction smoothing) như trong video của Deynum Studio.
 * * Yêu cầu các component:
 * 1. Rigidbody2D (đặt Gravity Scale = 0)
 * 2. Seeker (từ A* Pathfinding Project)
 * 3. Một Collider 2D (để Rigidbody2D hoạt động)
 */

using UnityEngine;
using Pathfinding; // Đừng quên thêm namespace của A*

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Seeker))]
public class MoveAI : MonoBehaviour
{
    [Header("Variables")]
    // Transform Target: Đây chính là Player mà AI sẽ bám theo.
    [Tooltip("Đối tượng mà AI sẽ bám theo (ví dụ: Player).")]
    public Transform target;

    // Target (Vector3): Trong hình ảnh của bạn, nó hiển thị vị trí X, Y, Z.
    // Đây có thể là vị trí của target, hoặc một mục tiêu cố định nào đó.
    // Tôi sẽ giữ nó là Transform để linh hoạt hơn, nhưng bạn có thể thêm một Vector3 để debug hoặc tùy chỉnh.
    // [HideInInspector] // Bạn có thể ẩn nó nếu không muốn hiển thị trong Inspector
    // public Vector3 currentTargetPosition; 

    [Header("Settings")]
    [Tooltip("Tốc độ di chuyển của AI.")]
    public float moveSpeed = 0.5f;

    [Tooltip("Khoảng cách tối thiểu đến waypoint để được coi là 'đã đến'.")]
    public float nextWaypointDistance = 0.4f;

    [Tooltip("Khoảng cách đến điểm cuối cùng của đường đi để được coi là 'đã đến đích'.")]
    public float reachedEndDistance = 0.2f; // Mới: Từ hình ảnh của bạn

    [Tooltip("Thời gian chờ (giây) giữa các lần cập nhật đường đi.")]
    public float pathUpdateTime = 0.2f; // Mới: Từ hình ảnh của bạn

    [Tooltip("Tốc độ 'xoay' để đi theo hướng mới. Giá trị càng cao, AI xoay càng nhanh và 'gắt'.")]
    [Range(0.1f, 10f)] // Giới hạn giá trị để dễ điều chỉnh
    public float directionSmoothing = 1f; // Mới: Tên theo hình ảnh của bạn (trước là turnSpeed)

    [Tooltip("Sử dụng Transform Target hay chỉ dùng vị trí cố định (nếu có)?")]
    public bool useTransformTarget = true; // Mới: Từ hình ảnh của bạn. Mặc định dùng Transform.

    // --- Biến nội bộ ---
    private Path path;
    private int currentWaypoint = 0;
    private bool reachedEndOfPath = false;

    private Seeker seeker;
    private Rigidbody2D rb;

    // Biến lưu hướng di chuyển *hiện tại* của AI sau khi đã làm mượt.
    private Vector2 currentMoveDirection = Vector2.zero;

    void Start()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();

        // Đảm bảo Rigidbody2D được thiết lập cho top-down 2D
        rb.useFullKinematicContacts = false;
        rb.gravityScale = 0;
        rb.freezeRotation = true;

        if (target == null && useTransformTarget)
        {
            Debug.LogError("Chưa gán Transform Target cho " + gameObject.name + " và useTransformTarget đang BẬT.");
            return;
        }

        // Bắt đầu vòng lặp cập nhật đường đi
        // Sử dụng pathUpdateTime từ Inspector
        InvokeRepeating(nameof(UpdatePath), 0f, pathUpdateTime);
    }

    /// <summary>
    /// Yêu cầu Seeker tìm một đường đi mới.
    /// </summary>
    void UpdatePath()
    {
        Vector3 targetPos;

        if (useTransformTarget && target != null)
        {
            targetPos = target.position;
        }
        else if (!useTransformTarget /* && (có thể thêm logic để dùng một Vector3 cố định nếu bạn muốn) */)
        {
            // Nếu bạn muốn AI đi tới một vị trí cố định khi useTransformTarget = false
            // Bạn sẽ cần thêm một public Vector3 ở đây, ví dụ: public Vector3 fixedTargetPosition;
            // Hiện tại, nếu không có Transform Target và không dùng Transform, AI sẽ không cập nhật đường đi.
            Debug.LogWarning("Không có Transform Target hoặc useTransformTarget đang TẮT, AI sẽ không tìm đường đi mới.");
            return;
        }
        else
        {
            return; // Không có target hoặc không dùng transform target
        }

        // Cập nhật currentTargetPosition (nếu bạn muốn hiển thị debug trong inspector)
        // currentTargetPosition = targetPos;

        if (seeker.IsDone())
        {
            seeker.StartPath(rb.position, targetPos, OnPathComplete);
        }
    }

    /// <summary>
    /// Callback được gọi khi Seeker hoàn thành việc tìm đường.
    /// </summary>
    void OnPathComplete(Path p)
    {
        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;
            reachedEndOfPath = false;
        }
        else
        {
            Debug.LogError("Lỗi tìm đường: " + p.errorLog);
        }
    }

    /// <summary>
    /// Sử dụng FixedUpdate vì chúng ta đang thao tác với Rigidbody (vật lý).
    /// </summary>
    void FixedUpdate()
    {
        if (path == null || target == null && useTransformTarget)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Kiểm tra xem đã đến cuối đường đi chưa bằng `reachedEndDistance`
        // Nếu đường đi có và đến gần điểm cuối cùng của toàn bộ đường đi
        if (path.vectorPath.Count > 0 && Vector2.Distance(rb.position, path.vectorPath[path.vectorPath.Count - 1]) < reachedEndDistance)
        {
            reachedEndOfPath = true;
            rb.linearVelocity = Vector2.zero; // Dừng lại khi đến nơi
            return;
        }
        else
        {
            reachedEndOfPath = false;
        }

        // Nếu đã đến waypoint cuối cùng trong path nhưng chưa đến reachedEndDistance
        if (currentWaypoint >= path.vectorPath.Count)
        {
            // Vẫn có thể cần di chuyển một chút đến điểm cuối cùng
            // hoặc chờ cập nhật đường đi mới.
            // Để đơn giản, ta sẽ chỉ reset currentWaypoint về cuối cùng để không bị lỗi out of bounds.
            currentWaypoint = path.vectorPath.Count - 1;
            if (currentWaypoint < 0) return; // Đảm bảo không có lỗi nếu path rỗng
        }

        // --- LOGIC DI CHUYỂN VÀ LÀM MƯỢT HƯỚNG ---

        // 1. Lấy hướng MỤC TIÊU (targetDirection): Hướng từ vị trí hiện tại đến waypoint tiếp theo
        Vector2 targetDirection = ((Vector2)path.vectorPath[currentWaypoint] - rb.position).normalized;

        // 2. Làm mượt hướng (Direction Smoothing)
        // Sử dụng `directionSmoothing` từ Inspector
        currentMoveDirection = Vector2.Lerp(currentMoveDirection, targetDirection, Time.fixedDeltaTime * directionSmoothing);

        // 3. Thiết lập vận tốc (Set Velocity)
        // Áp dụng vận tốc dựa trên hướng ĐÃ ĐƯỢC LÀM MƯỢT (currentMoveDirection)
        rb.linearVelocity = currentMoveDirection * moveSpeed;

        // ----------------------------------------

        // 4. Kiểm tra để đi đến waypoint tiếp theo
        float distanceToWaypoint = Vector2.Distance(rb.position, path.vectorPath[currentWaypoint]);
        if (distanceToWaypoint < nextWaypointDistance)
        {
            currentWaypoint++;
        }
    }

    /// <summary>
    /// (Tùy chọn) Vẽ Gizmos để debug, giống như trong video.
    /// </summary>
    void OnDrawGizmos()
    {
        // Vẽ đường đi
        if (path != null)
        {
            for (int i = 0; i < path.vectorPath.Count - 1; i++)
            {
                Gizmos.color = Color.cyan; // Màu đường đi
                Gizmos.DrawLine((Vector2)path.vectorPath[i], (Vector2)path.vectorPath[i + 1]);
            }
        }

        // Chỉ vẽ khi đang chạy game và có Rigidbody
        if (Application.isPlaying && rb != null)
        {
            // Vẽ hướng MỤC TIÊU (Màu đỏ)
            if (path != null && currentWaypoint < path.vectorPath.Count)
            {
                Vector2 targetDir = ((Vector2)path.vectorPath[currentWaypoint] - rb.position).normalized;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(rb.position, rb.position + targetDir * 2f);
            }

            // Vẽ hướng HIỆN TẠI (Đã làm mượt - Màu vàng)
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(rb.position, rb.position + currentMoveDirection * 2f);
        }
    }
}