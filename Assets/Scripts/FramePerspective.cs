using UnityEngine;

/// <summary>
/// Единая настройка наклона кадра. Проект — чистый 2D top-down (вид строго
/// сверху): камера не наклонена (pitch 0), иллюзию объёма дают текстуры и пропы
/// (стиль The Escapists), а не геометрия. Утилита оставлена как единая точка
/// настройки на случай, если захотим вернуть лёгкий наклон в отдельной сцене.
/// </summary>
public static class FramePerspective
{
    public const float CameraPitchDegrees = 0f;

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
