using UnityEngine;

/// <summary>Препятствие-укрытие: клетка становится Cover (нельзя пройти, блокирует обзор).</summary>
public class CoverObstacleMarker : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Vector3 p = LevelMarkerGizmos.Snap(transform.position);
        Gizmos.color = new Color(0.62f, 0.43f, 0.20f, 0.85f);
        Gizmos.DrawCube(p, new Vector3(0.9f, 0.9f, 0.1f));
        Gizmos.color = new Color(0.3f, 0.2f, 0.1f, 1f);
        Gizmos.DrawWireCube(p, new Vector3(1f, 1f, 0.1f));
    }
}
