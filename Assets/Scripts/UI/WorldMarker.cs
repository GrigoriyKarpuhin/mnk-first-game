using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI-элемент на общем screen-space канвасе, который каждый кадр следует за
/// мировым объектом через <see cref="Camera.WorldToScreenPoint"/>. Заменяет
/// разрозненную OnGUI-математику (screen.z &lt; 0, Screen.height - screen.y),
/// которая дублировалась в AlertIndicator/GuardPatrol/SurveillanceCamera/
/// PrisonInteraction/NPC. Создаётся через <see cref="UIKit.CreateWorldMarker"/>.
///
/// В отличие от прежних OnGUI-оверлеев, этот маркер рендерится камерой и потому
/// ВИДЕН в headless-скриншоте.
/// </summary>
public sealed class WorldMarker : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform rect;
    private Transform follow;
    private Vector3 worldOffset;
    private Camera cam;

    private CanvasGroup group;
    private Text glyph;
    private Image meterBg;
    private Image meterFill;
    private Text label;

    private bool visible = true;

    public void Configure(
        Canvas canvas, RectTransform rect, Transform follow, Vector3 worldOffset, Camera cam,
        Text glyph, Image meterBg, Image meterFill, Text label)
    {
        this.canvas = canvas;
        this.canvasRect = canvas.GetComponent<RectTransform>();
        this.rect = rect;
        this.follow = follow;
        this.worldOffset = worldOffset;
        this.cam = cam;
        this.glyph = glyph;
        this.meterBg = meterBg;
        this.meterFill = meterFill;
        this.label = label;

        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        if (glyph != null) glyph.gameObject.SetActive(false);
        if (meterBg != null) meterBg.gameObject.SetActive(false);
        if (label != null) label.gameObject.SetActive(false);
    }

    /// <summary>Глиф «?»/«!» над источником. Пустая строка — скрыть.</summary>
    public void SetGlyph(string text, Color color)
    {
        if (glyph == null) return;
        bool show = !string.IsNullOrEmpty(text);
        glyph.gameObject.SetActive(show);
        if (show)
        {
            glyph.text = text;
            glyph.color = color;
        }
    }

    /// <summary>Шкала тревоги/прогресса 0..1. Отрицательное — скрыть.</summary>
    public void SetMeter(float level01, Color color)
    {
        if (meterBg == null) return;
        bool show = level01 >= 0f;
        meterBg.gameObject.SetActive(show);
        if (show)
        {
            if (meterFill != null) meterFill.color = color;
            UIKit.SetMeter(meterFill, level01);
        }
    }

    /// <summary>Текстовая подпись под маркером. Пустая строка — скрыть.</summary>
    public void SetLabel(string text)
    {
        if (label == null) return;
        bool show = !string.IsNullOrEmpty(text);
        label.gameObject.SetActive(show);
        if (show) label.text = text;
    }

    /// <summary>Полностью показать/скрыть маркер (не трогая содержимое).</summary>
    public void SetVisible(bool value) => visible = value;

    /// <summary>Убрать маркер (владелец уничтожен/больше не нужен).</summary>
    public void Remove()
    {
        if (this != null) Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (follow == null)
        {
            Destroy(gameObject);
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null || group == null || canvasRect == null)
        {
            if (group != null) group.alpha = 0f;
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(follow.position + worldOffset);
        if (!visible || screen.z < 0f)
        {
            group.alpha = 0f;
            return;
        }

        group.alpha = 1f;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local))
        {
            rect.anchoredPosition = local;
        }
    }
}
