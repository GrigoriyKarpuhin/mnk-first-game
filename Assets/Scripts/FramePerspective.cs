using UnityEngine;

/// <summary>
/// Единая настройка лёгкого наклона кадра. Проект остаётся 2D/orthographic,
/// но камера слегка смотрит под углом, чтобы пол читался как поверхность.
/// </summary>
public static class FramePerspective
{
    public const float CameraPitchDegrees = 18f;

    public static Quaternion CameraRotation => Quaternion.Euler(CameraPitchDegrees, 0f, 0f);
    public static Quaternion CharacterBillboardRotation => CameraRotation;

    public static void Apply(Camera camera)
    {
        if (camera == null) return;
        camera.transform.rotation = CameraRotation;
    }

    /// <summary>
    /// Компенсирует сдвиг фокуса при наклоне камеры, чтобы цель оставалась в центре.
    /// </summary>
    public static Vector3 CompensateCameraPosition(Vector3 desiredCameraPosition, float focusZ = 0f)
    {
        float distanceToFocusPlane = Mathf.Abs(focusZ - desiredCameraPosition.z);
        float pitch = CameraPitchDegrees * Mathf.Deg2Rad;
        desiredCameraPosition.y += Mathf.Tan(pitch) * distanceToFocusPlane;
        return desiredCameraPosition;
    }

    public static Vector3 CompensateOffset(Vector3 offset)
    {
        float pitch = CameraPitchDegrees * Mathf.Deg2Rad;
        offset.y += Mathf.Tan(pitch) * Mathf.Abs(offset.z);
        return offset;
    }
}
