using System.Globalization;
using UnityEngine;

/// <summary>
/// Единый источник правды для внешнего вида UI — «дизайн-токены» игры.
/// По духу это <see cref="WorldMetrics"/>, но для интерфейса: здесь живут все
/// цвета, размеры шрифта, отступы и длительности анимаций. Экраны НЕ хранят
/// собственные хардкод-цвета — они берут их отсюда через <see cref="UIKit"/>.
///
/// Стиль — диегетический CRT-терминал тюремного надзора: зелёный люминофор на
/// тёмном бетоне, gunmetal-рамки, трафаретные подписи, скан-линии. Палитра
/// выведена из Design/ART_STYLE.md §2, чтобы UI и мир говорили на одном языке.
///
/// Менять внешний вид UI нужно ТОЛЬКО тут.
/// </summary>
public static class UITheme
{
    // --- Поверхности (от самой глубокой к приподнятой) ---------------------
    /// <summary>Затемнение позади модальных экранов.</summary>
    public static readonly Color Backdrop = Hex("060907", 0.72f);
    /// <summary>Фон экрана целиком (глубокая тень).</summary>
    public static readonly Color Surface = Hex("0a0d0b", 0.96f);
    /// <summary>Заливка панели.</summary>
    public static readonly Color Panel = Hex("0e1e14", 0.98f);
    /// <summary>Приподнятый блок / кнопка в покое (зелёный тёмный).</summary>
    public static readonly Color PanelRaised = Hex("183422");
    /// <summary>Утопленный «экран-колодец» — самая тёмная зона под контент.</summary>
    public static readonly Color Well = Hex("07120b");

    // --- Рамки и хром ------------------------------------------------------
    /// <summary>Внутренняя рамка панели (зелёный mid).</summary>
    public static readonly Color Border = Hex("34804a");
    /// <summary>Приглушённый разделитель.</summary>
    public static readonly Color BorderDim = Hex("183422");
    /// <summary>Изношенная металлическая рамка (gunmetal).</summary>
    public static readonly Color Chrome = Hex("3e4248");
    /// <summary>Светлая грань уголковых скобок (сталь).</summary>
    public static readonly Color ChromeHi = Hex("6e7472");

    // --- Текст -------------------------------------------------------------
    /// <summary>Основной люминофорный текст (зелёный hi).</summary>
    public static readonly Color TextPrimary = Hex("58c470");
    /// <summary>Яркий акцентный текст заголовков.</summary>
    public static readonly Color TextBright = Hex("82eb96");
    /// <summary>Вторичный текст (зелёный mid).</summary>
    public static readonly Color TextMuted = Hex("34804a");
    /// <summary>Трафаретные подписи/номера (серо-зелёный).</summary>
    public static readonly Color TextStencil = Hex("9ea090");
    /// <summary>Отключённый текст (gunmetal).</summary>
    public static readonly Color TextDisabled = Hex("3e4248");
    /// <summary>Тёмный текст поверх яркой акцентной заливки (нажатая кнопка).</summary>
    public static readonly Color OnAccent = Hex("06100a");

    // --- Акценты и состояния ----------------------------------------------
    /// <summary>Основной акцент — свечение CRT (зелёный glow).</summary>
    public static readonly Color Accent = Hex("82eb96");
    /// <summary>Ореол свечения позади акцентов (низкая альфа).</summary>
    public static readonly Color AccentGlow = Hex("82eb96", 0.35f);
    /// <summary>Успех/подтверждение.</summary>
    public static readonly Color Success = Hex("58c470");
    /// <summary>Предупреждение — приглушённый янтарь (не кислотно-жёлтый).</summary>
    public static readonly Color Warning = Hex("c8a032");
    /// <summary>Опасность/провал — ржавый красный.</summary>
    public static readonly Color Danger = Hex("8a4a2a");
    /// <summary>Полная тревога — яркий красный.</summary>
    public static readonly Color DangerBright = Hex("c04a2a");

    // --- Строки списков и выделение ---------------------------------------
    /// <summary>Строка списка в покое.</summary>
    public static readonly Color RowNormal = Hex("0d1a12");
    /// <summary>Активная (не выбранная) строка.</summary>
    public static readonly Color RowActive = Hex("1a3524");
    /// <summary>Выбранная строка/вкладка.</summary>
    public static readonly Color Selected = Hex("244a30");

    // --- Состояния кнопки (буквальные цвета, изображение — белое) ----------
    // Никакой пере-экспозиции: ни один тон не превышает 1.0, в отличие от
    // старого хака new Color(1.45f, 1.45f, 1.45f).
    public static readonly Color ButtonNormal = Hex("183422");
    public static readonly Color ButtonHover = Hex("2f6b45");
    public static readonly Color ButtonPressed = Hex("82eb96");
    public static readonly Color ButtonSelected = Hex("2a5c3a");
    public static readonly Color ButtonDisabled = Hex("12211a");

    // --- Шкала кегля (px при опорном разрешении 1280×720) ------------------
    public const int TypeDisplay = 40; // заголовки экранов
    public const int TypeTitle = 28;   // заголовки панелей, имя говорящего
    public const int TypeHeader = 22;  // заголовки секций
    public const int TypeBody = 18;    // основной читаемый текст
    public const int TypeLabel = 15;   // трафаретные подписи, хинты клавиш
    public const int TypeCaption = 12; // микротекст HUD, сноски

    // --- Шкала отступов (база 4px) ----------------------------------------
    public const float Space1 = 4f;
    public const float Space2 = 8f;
    public const float Space3 = 12f;
    public const float Space4 = 16f;
    public const float Space6 = 24f;
    public const float Space8 = 32f;

    // --- Толщины рамок и уголковые скобки ---------------------------------
    public const float BorderThin = 1f;
    public const float BorderMed = 2f;
    public const float BorderThick = 3f;
    public const float BracketLen = 18f;   // длина плеча уголковой скобки
    public const float BracketThick = 2f;  // толщина скобки

    // --- Длительности анимаций (в НЕмасштабированных секундах) -------------
    // Модальные экраны ставят Time.timeScale=0, поэтому любая анимация UI
    // обязана считать время через Time.unscaledDeltaTime.
    public const float MotionFast = 0.08f;    // нажатие кнопки
    public const float MotionMed = 0.16f;     // затухание панели
    public const float MotionSlow = 0.30f;    // открытие экрана
    public const float ScanlineSpeed = 0.6f;  // прокрутка UV скан-линий, ед./с

    // --- Слои сортировки Canvas (замена GUI.depth) ------------------------
    // Мир рисуется спрайтами на слое Default с Y-сортировкой (см. SortingLayers):
    // сущности дают order до ~6–7 тыс., Foreground — до ~23 тыс. UI обязан быть
    // ВЫШЕ всего мира, иначе спрайты «протыкают» полноэкранные модалки.
    public const int SortHud = 25000;
    public const int SortWorldMarkers = 25500;
    public const int SortExperiment = 26000;
    public const int SortMap = 30000;
    public const int SortBoard = 30100;
    public const int SortDialogue = 30200;
    public const int SortJournal = 30300;

    // --- Опорное разрешение (совпадает с существующими канвасами) ---------
    public static readonly Vector2 ReferenceResolution = new(1280f, 720f);
    public const float MatchWidthOrHeight = 0.5f;

    /// <summary>
    /// Разбирает строку «RRGGBB» в <see cref="Color"/> с заданной альфой.
    /// Инвариантная культура — чтобы парсинг не зависел от локали.
    /// </summary>
    public static Color Hex(string rgb, float alpha = 1f)
    {
        byte r = byte.Parse(rgb.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(rgb.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(rgb.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
        return new Color32(r, g, b, a);
    }
}
