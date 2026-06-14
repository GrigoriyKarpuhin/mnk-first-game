using UnityEngine;

/// <summary>
/// Держит спрайт персонажа развёрнутым к наклонённой 2D-камере. Без этого
/// персонаж рисуется в плоскости пола и визуально выглядит лежащим.
/// </summary>
public class CharacterScreenFacing : MonoBehaviour
{
    public static CharacterScreenFacing Attach(GameObject target)
    {
        if (target == null) return null;
        var facing = target.GetComponent<CharacterScreenFacing>();
        if (facing == null) facing = target.AddComponent<CharacterScreenFacing>();
        facing.Apply();
        return facing;
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        transform.rotation = FramePerspective.CharacterBillboardRotation;
    }
}
