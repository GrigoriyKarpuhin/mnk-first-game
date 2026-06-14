using UnityEngine;

/// <summary>
/// Камера плавно следует за целью (игроком)
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Цель за которой следует камера (обычно Player)")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Tooltip("Скорость следования (больше = быстрее догоняет)")]
    [Range(1f, 20f)]
    [SerializeField] private float smoothSpeed = 5f;

    [Tooltip("Смещение камеры относительно цели")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    [Header("Perspective Framing")]
    [SerializeField] private bool useFramePerspective = true;

    [Header("Zoom Settings")]
    [Tooltip("Размер камеры (меньше = ближе/крупнее игрок)")]
    [Range(1f, 20f)]
    [SerializeField] private float cameraSize = 4f;

    [Header("Bounds (опционально)")]
    [Tooltip("Ограничить движение камеры")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographicSize = cameraSize;
        }
        ApplyFramePerspective();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        ApplyFramePerspective();

        // Целевая позиция
        Vector3 cameraOffset = useFramePerspective ? FramePerspective.CompensateOffset(offset) : offset;
        Vector3 targetPosition = target.position + cameraOffset;

        // Ограничение по границам
        if (useBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        // Плавное движение
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPosition, 
            ref velocity, 
            1f / smoothSpeed
        );

        // Обновляем размер камеры если изменился в инспекторе
        if (cam != null && !Mathf.Approximately(cam.orthographicSize, cameraSize))
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, cameraSize, Time.deltaTime * smoothSpeed);
        }
    }

    /// <summary>
    /// Устанавливает цель для камеры
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetFramePerspective(bool enabled)
    {
        useFramePerspective = enabled;
        ApplyFramePerspective();
    }

    /// <summary>
    /// Мгновенно перемещает камеру к цели (без плавности)
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 cameraOffset = useFramePerspective ? FramePerspective.CompensateOffset(offset) : offset;
        transform.position = target.position + cameraOffset;
    }

    private void ApplyFramePerspective()
    {
        if (useFramePerspective)
        {
            FramePerspective.Apply(cam);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Обновляем размер камеры в редакторе при изменении
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographicSize = cameraSize;
        }
        ApplyFramePerspective();
    }

    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;

        // Рисуем границы камеры
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(
            (minBounds.x + maxBounds.x) / 2f,
            (minBounds.y + maxBounds.y) / 2f,
            0
        );
        Vector3 size = new Vector3(
            maxBounds.x - minBounds.x,
            maxBounds.y - minBounds.y,
            0.1f
        );
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
