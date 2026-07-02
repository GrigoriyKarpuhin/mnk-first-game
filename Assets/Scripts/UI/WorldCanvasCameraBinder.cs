using UnityEngine;

/// <summary>
/// Держит <see cref="Canvas.worldCamera"/> у ScreenSpaceCamera-канваса
/// привязанным к текущей <see cref="Camera.main"/>. Нужно потому, что UI-канвасы
/// живут через <see cref="Object.DontDestroyOnLoad"/> и переживают смену сцен
/// (тюрьма → эксперимент), а вот камера сцены — нет: после загрузки новой сцены
/// прежняя камера уничтожена, и канвас с «мёртвой» камерой каждый кадр сыплет
/// предупреждениями и рвёт рендер (это и грузит CPU/GPU в эксперименте).
///
/// Camera.main вызывается ТОЛЬКО когда ссылка потеряна, поэтому в устоявшемся
/// состоянии компонент практически бесплатен.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class WorldCanvasCameraBinder : MonoBehaviour
{
    private Canvas canvas;

    private void Awake() => canvas = GetComponent<Canvas>();

    private void OnEnable() => Rebind();

    private void LateUpdate()
    {
        if (canvas == null) return;
        // Уже привязаны к живой камере (Unity-перегруженный == null ловит и
        // уничтоженную) — ничего не делаем, Camera.main не дёргаем.
        bool bound = canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null;
        if (!bound) Rebind();
    }

    private void Rebind()
    {
        if (canvas == null) return;
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Есть камера — всегда возвращаемся в ScreenSpaceCamera (в т.ч. после
            // временного отката в Overlay, когда камеры ещё не было).
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            // Камеры нет — не оставляем «мёртвый» ScreenSpaceCamera.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
