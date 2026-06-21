using UnityEngine;

/// <summary>
/// Keeps a character sprite upright on screen even if helper transforms around it
/// are changed by movement, shadows, or future isometric presentation code.
/// </summary>
public sealed class CharacterScreenFacing : MonoBehaviour
{
    public static CharacterScreenFacing Attach(GameObject target)
    {
        if (target == null) return null;
        var facing = target.GetComponent<CharacterScreenFacing>();
        if (facing == null) facing = target.AddComponent<CharacterScreenFacing>();
        return facing;
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
