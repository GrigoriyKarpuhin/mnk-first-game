using UnityEngine;

/// <summary>Ящик-укрытие: клетка, где игрок прячется (E) и становится невидимым для охраны.</summary>
public class HideSpotMarker : MonoBehaviour
{
    [Tooltip("Подпись ящика (для понятности в иерархии).")]
    public string label = "Ящик";

    private void OnDrawGizmos()
    {
        Vector3 p = LevelMarkerGizmos.Snap(transform.position);
        Gizmos.color = new Color(0.18f, 0.45f, 0.75f, 0.85f);
        Gizmos.DrawCube(p, new Vector3(0.85f, 0.85f, 0.1f));
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(p, new Vector3(1f, 1f, 0.1f));
    }
}
