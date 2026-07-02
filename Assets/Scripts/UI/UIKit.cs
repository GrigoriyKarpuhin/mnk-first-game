using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Фабрика UI-компонентов в стиле CRT-терминала. Заменяет разрозненные
/// приватные помощники (CreatePanel/CreateText/AddBorder/Stretch/…), которые
/// раньше копипастились в каждом экране, и централизует шрифт и «хром».
///
/// Все экраны строятся ТОЛЬКО через эти методы, а цвета/кегли/отступы берут из
/// <see cref="UITheme"/>. Так правка внешнего вида живёт в одном месте.
/// </summary>
public static class UIKit
{
    private const string FontResource = "Fonts/IBMPlexMono-Regular";
    private static Font font;

    /// <summary>
    /// Шрифт терминала. Пока кастомный .ttf не импортирован в Resources/Fonts,
    /// безопасно откатывается на встроенный LegacyRuntime, чтобы код работал.
    /// </summary>
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.Load<Font>(FontResource);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>true, если загружен именно кастомный шрифт (для тестов/диагностики).</summary>
    public static bool UsingCustomFont => Font != Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    // === Инфраструктура ====================================================

    /// <summary>
    /// Корневой канвас экрана. По умолчанию ScreenSpaceCamera — только такой
    /// канвас попадает в headless-скриншот (см. SmokeCaptureTests: кадр
    /// снимается через камеру, а Overlay навешивается поверх и не рендерится).
    /// </summary>
    public static Canvas CreateRootCanvas(GameObject host, int sortingOrder, bool worldFacing = true)
    {
        EnsureEventSystem();

        var canvas = host.GetComponent<Canvas>();
        if (canvas == null) canvas = host.AddComponent<Canvas>();

        if (worldFacing)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main; // может быть null между сценами
            canvas.planeDistance = 1f;
            // Держит канвас привязанным к текущей камере при смене сцен, иначе
            // «мёртвая» камера каждый кадр грузит CPU/GPU (см. WorldCanvasCameraBinder).
            if (host.GetComponent<WorldCanvasCameraBinder>() == null)
                host.AddComponent<WorldCanvasCameraBinder>();
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = sortingOrder;

        var scaler = host.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UITheme.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = UITheme.MatchWidthOrHeight;
        // Рендерим текст с запасом плотности, чтобы динамический шрифт не мылился
        // при апскейле опорного 1280×720 до дисплеев большего разрешения.
        scaler.dynamicPixelsPerUnit = 2.5f;

        if (host.GetComponent<GraphicRaycaster>() == null) host.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var go = new GameObject("EventSystem");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    public static RectTransform Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static RectTransform FullStretch(RectTransform rect) => Stretch(rect, 0f, 0f, 0f, 0f);

    public static RectTransform Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>Прижать к верху родителя на всю ширину: отступы слева/сверху/справа + высота.</summary>
    public static RectTransform TopRect(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
        return rect;
    }

    // === Базовые блоки =====================================================

    public static Image CreatePanel(string name, Transform parent, Color fill)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = fill;
        return image;
    }

    public static Text CreateText(string name, Transform parent, int fontSize, TextAnchor align, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = Font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color ?? UITheme.TextPrimary;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>Утопленный тёмный «экран-колодец» под контент (как стекло CRT).</summary>
    public static Image CreateScreen(string name, Transform parent) => CreatePanel(name, parent, UITheme.Well);

    // === Терминальный «хром» ==============================================

    /// <summary>
    /// Полноценная диегетическая панель: заливка + металлическая рамка +
    /// внутренняя зелёная рамка + уголковые скобки + (опц.) скан-линии.
    /// Возвращает Image самой панели; <paramref name="content"/> — уже
    /// вписанный внутрь рамок RectTransform, который заполняет вызывающий код.
    /// </summary>
    public static Image CreateTerminalPanel(
        string name,
        Transform parent,
        out RectTransform content,
        bool scanlines = true,
        bool brackets = true,
        Color? fill = null)
    {
        Image panel = CreatePanel(name, parent, fill ?? UITheme.Panel);
        var panelRect = panel.rectTransform;

        AddFrame(panelRect, UITheme.Chrome, UITheme.Border, UITheme.BorderThick, UITheme.BorderThin);
        if (scanlines) AddScanlineOverlay(panelRect);
        if (brackets) AddCornerBrackets(panelRect, UITheme.ChromeHi, UITheme.BracketLen, UITheme.BracketThick);

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(panel.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        float inset = UITheme.BorderThick + UITheme.Space3;
        Stretch(content, inset, inset, inset, inset);
        return panel;
    }

    /// <summary>Двухслойная рамка: внешний хром + внутренняя тонкая зелёная линия.</summary>
    public static void AddFrame(RectTransform panel, Color chrome, Color inner, float chromeThick, float innerThick)
    {
        AddEdges(panel, chrome, chromeThick, 0f);
        AddEdges(panel, inner, innerThick, chromeThick);
    }

    private static void AddEdges(RectTransform panel, Color color, float thickness, float margin)
    {
        // Top
        CreateEdge(panel, color, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -(margin + thickness * 0.5f)), new Vector2(-margin * 2f, thickness));
        // Bottom
        CreateEdge(panel, color, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, margin + thickness * 0.5f), new Vector2(-margin * 2f, thickness));
        // Left
        CreateEdge(panel, color, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(margin + thickness * 0.5f, 0f), new Vector2(thickness, -margin * 2f));
        // Right
        CreateEdge(panel, color, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-(margin + thickness * 0.5f), 0f), new Vector2(thickness, -margin * 2f));
    }

    private static void CreateEdge(RectTransform parent, Color color, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        Image edge = CreatePanel("Edge", parent, color);
        edge.raycastTarget = false;
        RectTransform r = edge.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    /// <summary>Восемь коротких штрихов — уголковые скобки на четырёх углах.</summary>
    public static void AddCornerBrackets(RectTransform panel, Color color, float len, float thick)
    {
        // (anchor, pivot, horizontal-arm dir sign, vertical-arm dir sign)
        var corners = new[]
        {
            new Vector2(0f, 1f), // TL
            new Vector2(1f, 1f), // TR
            new Vector2(0f, 0f), // BL
            new Vector2(1f, 0f), // BR
        };
        foreach (Vector2 c in corners)
        {
            float sx = c.x < 0.5f ? 1f : -1f; // рука тянется внутрь
            float sy = c.y < 0.5f ? 1f : -1f;
            // горизонтальная рука
            Bracket(panel, color, c, new Vector2(sx * len * 0.5f, sy * thick * 0.5f), new Vector2(len, thick));
            // вертикальная рука
            Bracket(panel, color, c, new Vector2(sx * thick * 0.5f, sy * len * 0.5f), new Vector2(thick, len));
        }
    }

    private static void Bracket(RectTransform parent, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Image arm = CreatePanel("Bracket", parent, color);
        arm.raycastTarget = false;
        Anchor(arm.rectTransform, anchor, anchor, anchor, pos, size);
    }

    /// <summary>
    /// Тайлящийся оверлей скан-линий поверх панели. Пока спрайт не сгенерирован,
    /// безопасно возвращает null (панель просто без скан-линий).
    /// </summary>
    public static Image AddScanlineOverlay(RectTransform panel)
    {
        var sprite = Resources.Load<Sprite>("Sprites/ui_scanline");
        if (sprite == null) return null;

        Image overlay = CreatePanel("Scanlines", panel, Color.white);
        overlay.sprite = sprite;
        overlay.type = Image.Type.Tiled;
        overlay.raycastTarget = false;
        FullStretch(overlay.rectTransform);
        overlay.transform.SetAsLastSibling();
        return overlay;
    }

    /// <summary>Трафаретная подпись: приглушённый серо-зелёный, ВЕРХНИЙ регистр.</summary>
    public static Text CreateStencilLabel(string text, Transform parent, TextAnchor align = TextAnchor.MiddleLeft)
    {
        Text label = CreateText("Stencil", parent, UITheme.TypeLabel, align, UITheme.TextStencil);
        label.text = text != null ? text.ToUpperInvariant() : string.Empty;
        return label;
    }

    // === Виджеты ===========================================================

    /// <summary>Кнопка с корректными состояниями (без пере-экспозиции >1.0).</summary>
    public static Button CreateButton(string label, Transform parent, UnityAction onClick, out Text labelText, int fontSize = UITheme.TypeBody)
    {
        Image bg = CreatePanel($"Button:{label}", parent, Color.white);
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = UITheme.ButtonNormal;
        colors.highlightedColor = UITheme.ButtonHover;
        colors.pressedColor = UITheme.ButtonPressed;
        colors.selectedColor = UITheme.ButtonSelected;
        colors.disabledColor = UITheme.ButtonDisabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = UITheme.MotionFast;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);

        labelText = CreateText("Label", bg.transform, fontSize, TextAnchor.MiddleCenter, UITheme.TextPrimary);
        labelText.text = label;
        labelText.raycastTarget = false;
        Stretch(labelText.rectTransform, UITheme.Space3, UITheme.Space1, UITheme.Space3, UITheme.Space1);
        return button;
    }

    public static Button CreateButton(string label, Transform parent, UnityAction onClick, int fontSize = UITheme.TypeBody)
        => CreateButton(label, parent, onClick, out _, fontSize);

    /// <summary>Строка списка: левый текст, фон меняет вызывающий (выбор/актив).</summary>
    public static Button CreateListRow(string label, Transform parent, UnityAction onClick, out Image bg, out Text labelText)
    {
        bg = CreatePanel($"Row:{label}", parent, UITheme.RowNormal);
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.None; // фон ведёт вызывающий вручную
        if (onClick != null) button.onClick.AddListener(onClick);

        labelText = CreateText("Label", bg.transform, UITheme.TypeBody, TextAnchor.MiddleLeft, UITheme.TextPrimary);
        labelText.text = label;
        labelText.raycastTarget = false;
        labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
        Stretch(labelText.rectTransform, UITheme.Space3, UITheme.Space1, UITheme.Space3, UITheme.Space1);
        return button;
    }

    /// <summary>Горизонтальная лента вкладок равной ширины.</summary>
    public static Button[] CreateTabBar(Transform parent, out GameObject bar, params string[] tabNames)
    {
        Image barImage = CreatePanel("TabBar", parent, Color.clear);
        barImage.raycastTarget = false;
        FullStretch(barImage.rectTransform); // заполнить родителя, иначе вкладки схлопнутся
        bar = barImage.gameObject;
        var tabs = new Button[tabNames.Length];
        float gap = UITheme.Space2;
        for (int i = 0; i < tabNames.Length; i++)
        {
            Button tab = CreateButton(tabNames[i], bar.transform, null);
            RectTransform r = tab.GetComponent<RectTransform>();
            float frac = 1f / tabNames.Length;
            r.anchorMin = new Vector2(i * frac, 0f);
            r.anchorMax = new Vector2((i + 1) * frac, 1f);
            r.offsetMin = new Vector2(i == 0 ? 0f : gap * 0.5f, 0f);
            r.offsetMax = new Vector2(i == tabNames.Length - 1 ? 0f : -gap * 0.5f, 0f);
            tabs[i] = tab;
        }
        return tabs;
    }

    /// <summary>Шкала (тревога/прогресс/HP). Уровень задаётся <see cref="SetMeter"/>.</summary>
    public static Image CreateMeter(string name, Transform parent, out Image fill, Color? fillColor = null)
    {
        Image bg = CreatePanel(name, parent, UITheme.Well);
        bg.raycastTarget = false;
        fill = CreatePanel("Fill", bg.transform, fillColor ?? UITheme.Accent);
        fill.raycastTarget = false;
        RectTransform fr = fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(1f, 1f);
        fr.offsetMin = new Vector2(UITheme.BorderThin, UITheme.BorderThin);
        fr.offsetMax = new Vector2(-UITheme.BorderThin, -UITheme.BorderThin);
        return bg;
    }

    /// <summary>Задаёт заполнение шкалы, созданной через <see cref="CreateMeter"/>.</summary>
    public static void SetMeter(Image fill, float level01)
    {
        if (fill == null) return;
        level01 = Mathf.Clamp01(level01);
        RectTransform fr = fill.rectTransform;
        fr.anchorMax = new Vector2(level01, 1f);
    }

    /// <summary>Чип-подсказка клавиши: «[E] ДЕЙСТВИЕ» в терминальном стиле.</summary>
    public static GameObject CreateKeyHintChip(string key, string action, Transform parent, out Text text)
    {
        Image chip = CreatePanel($"Chip:{key}", parent, UITheme.PanelRaised);
        AddEdges(chip.rectTransform, UITheme.BorderDim, UITheme.BorderThin, 0f);
        text = CreateText("Text", chip.transform, UITheme.TypeLabel, TextAnchor.MiddleCenter, UITheme.TextStencil);
        text.text = ChipMarkup(key, action);
        text.raycastTarget = false;
        Stretch(text.rectTransform, UITheme.Space2, UITheme.Space1, UITheme.Space2, UITheme.Space1);
        return chip.gameObject;
    }

    /// <summary>Разметка чипа: клавиша акцентным цветом, действие — трафаретом.</summary>
    public static string ChipMarkup(string key, string action)
    {
        string k = ColorTag(UITheme.Accent, key);
        return string.IsNullOrEmpty(action) ? k : $"{k} {action.ToUpperInvariant()}";
    }

    /// <summary>
    /// Вертикальная прокручиваемая область. Возвращает Content-RectTransform,
    /// который заполняет вызывающий (строки сверху вниз), затем задаёт его высоту
    /// через <see cref="SetScrollContentHeight"/>.
    /// </summary>
    public static RectTransform CreateScrollView(string name, Transform parent, out ScrollRect scroll)
    {
        Image vp = CreatePanel(name, parent, Color.clear);
        vp.raycastTarget = true; // чтобы колесо мыши доходило до ScrollRect
        vp.gameObject.AddComponent<RectMask2D>();
        scroll = vp.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = vp.rectTransform;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(vp.transform, false);
        var content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        scroll.content = content;
        return content;
    }

    public static void SetScrollContentHeight(RectTransform content, float height)
    {
        content.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>Тонкая линия-ребро между двумя точками в локальных координатах родителя.</summary>
    public static Image CreateLine(Transform parent, Vector2 a, Vector2 b, Color color, float thickness = 2f)
    {
        Image line = CreatePanel("Line", parent, color);
        line.raycastTarget = false;
        RectTransform r = line.rectTransform;
        Vector2 dir = b - a;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(dir.magnitude, thickness);
        r.anchoredPosition = a;
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        return line;
    }

    /// <summary>Небольшой всплывающий тост (уведомление). Скрыт по умолчанию.</summary>
    public static GameObject CreateToast(Transform parent, out Text label)
    {
        Image panel = CreateTerminalPanel("Toast", parent, out RectTransform content, scanlines: false);
        label = CreateText("Text", content, UITheme.TypeBody, TextAnchor.MiddleCenter, UITheme.TextPrimary);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        FullStretch(label.rectTransform);
        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    // === Мировые маркеры (глиф/шкала/подпись над объектом) =================

    private static Canvas worldMarkerCanvas;

    /// <summary>Общий screen-space канвас для всех мировых маркеров.</summary>
    public static Canvas WorldMarkerCanvas
    {
        get
        {
            if (worldMarkerCanvas != null) return worldMarkerCanvas;
            var go = new GameObject("World Markers");
            Object.DontDestroyOnLoad(go);
            worldMarkerCanvas = CreateRootCanvas(go, UITheme.SortWorldMarkers, worldFacing: true);
            // Маркеры не должны перехватывать клики.
            var ray = go.GetComponent<GraphicRaycaster>();
            if (ray != null) ray.enabled = false;
            return worldMarkerCanvas;
        }
    }

    /// <summary>
    /// Создаёт маркер, следящий за мировым <paramref name="follow"/> через
    /// WorldToScreenPoint. Возвращает компонент-дескриптор, которым экраны
    /// управляют содержимым (глиф «?/!», шкала тревоги, подпись).
    /// </summary>
    public static WorldMarker CreateWorldMarker(
        string name, Transform follow, Vector3 worldOffset, Camera cam,
        bool wantGlyph = false, bool wantMeter = false, bool wantLabel = false)
    {
        Canvas canvas = WorldMarkerCanvas;
        var container = new GameObject(name, typeof(RectTransform));
        container.transform.SetParent(canvas.transform, false);
        var rect = container.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 60f);

        Image meterBg = null, meterFill = null;
        Text glyph = null, label = null;

        if (wantMeter)
        {
            meterBg = CreateMeter("Meter", container.transform, out meterFill, UITheme.Warning);
            Anchor(meterBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(48f, 8f));
        }
        if (wantGlyph)
        {
            glyph = CreateText("Glyph", container.transform, UITheme.TypeTitle, TextAnchor.MiddleCenter, UITheme.Warning);
            glyph.fontStyle = FontStyle.Bold;
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(40f, 34f));
        }
        if (wantLabel)
        {
            label = CreateText("Label", container.transform, UITheme.TypeLabel, TextAnchor.MiddleCenter, UITheme.TextStencil);
            Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 24f));
        }

        var marker = container.AddComponent<WorldMarker>();
        marker.Configure(canvas, rect, follow, worldOffset, cam, glyph, meterBg, meterFill, label);
        return marker;
    }

    // === Утилиты ===========================================================

    /// <summary>Оборачивает текст в rich-text тег цвета.</summary>
    public static string ColorTag(Color color, string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
}
