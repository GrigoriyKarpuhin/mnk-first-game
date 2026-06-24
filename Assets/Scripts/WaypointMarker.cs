using UnityEngine;

/// <summary>Точка маршрута патруля. Порядок точек = порядок дочерних объектов охранника.</summary>
public class WaypointMarker : MonoBehaviour
{
    [Tooltip("Осмотреться по сторонам на этой точке (иначе просто идёт дальше).")]
    public bool scan = true;

    private void OnDrawGizmos()
    {
        Vector3 p = LevelMarkerGizmos.Snap(transform.position);
        Gizmos.color = scan ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.8f, 1f);
        Gizmos.DrawSphere(p, 0.28f);
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireCube(p, new Vector3(1f, 1f, 0.1f));
    }
}
