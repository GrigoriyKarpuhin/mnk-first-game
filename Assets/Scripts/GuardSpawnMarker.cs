using System.Collections.Generic;
using UnityEngine;

/// <summary>Тип охранника: патрульный (с маршрутом и зрением) или конвойный пост.</summary>
public enum GuardKind
{
    /// <summary>Патрульный (GuardPatrol): ходит по точкам, видит конусом, гонится/ищет.</summary>
    Patrol,

    /// <summary>Конвойный пост (ScheduleEnforcerGuard): стоит, активируется при нарушении расписания.</summary>
    ScheduleEnforcer,
}

/// <summary>
/// Точка спавна охранника. <see cref="kind"/> выбирает тип:
/// — Patrol: маршрут = дочерние <see cref="WaypointMarker"/> по порядку (без них — стоит на месте);
/// — ScheduleEnforcer: стационарный конвойный пост на позиции самого маркера (точки игнорируются).
/// <see cref="GameGrid"/> читает маркеры при старте и создаёт нужный тип охранника.
/// </summary>
public class GuardSpawnMarker : MonoBehaviour
{
    [Tooltip("Имя надзирателя (отображается в иерархии объектов).")]
    public string guardName = "Надзиратель";

    [Tooltip("Тип: Patrol — ходит по точкам и видит; ScheduleEnforcer — стоит, ловит по расписанию.")]
    public GuardKind kind = GuardKind.Patrol;

    public List<WaypointMarker> CollectWaypoints()
    {
        var list = new List<WaypointMarker>();
        foreach (Transform child in transform)
        {
            var wp = child.GetComponent<WaypointMarker>();
            if (wp != null) list.Add(wp);
        }
        return list;
    }

    private void OnDrawGizmos()
    {
        Vector3 self = LevelMarkerGizmos.Snap(transform.position);

        // Конвойный пост — отдельный вид (фиолетовый), маршрут не рисуем.
        if (kind == GuardKind.ScheduleEnforcer)
        {
            Gizmos.color = new Color(0.7f, 0.3f, 0.9f, 0.85f);
            Gizmos.DrawCube(self, new Vector3(0.8f, 0.8f, 0.1f));
            Gizmos.color = new Color(0.85f, 0.5f, 1f, 1f);
            Gizmos.DrawWireCube(self, new Vector3(1.1f, 1.1f, 0.1f));
            return;
        }

        var waypoints = CollectWaypoints();
        Vector3 origin = waypoints.Count > 0
            ? LevelMarkerGizmos.Snap(waypoints[0].transform.position)
            : self;

        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(origin, 0.45f);

        // Линии маршрута + замыкание петли (как в патруле по кругу).
        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.9f);
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Gizmos.DrawLine(LevelMarkerGizmos.Snap(waypoints[i].transform.position),
                            LevelMarkerGizmos.Snap(waypoints[i + 1].transform.position));
        }
        if (waypoints.Count > 2)
        {
            Gizmos.DrawLine(LevelMarkerGizmos.Snap(waypoints[waypoints.Count - 1].transform.position),
                            LevelMarkerGizmos.Snap(waypoints[0].transform.position));
        }
    }
}
