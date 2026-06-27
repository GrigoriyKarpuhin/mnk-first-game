using System;
using System.Collections.Generic;
using UnityEngine;

public enum PrisonItemId
{
    None,
    Screwdriver,
    KitchenManifest,
    ServiceBadge,
    EyeImplant,
    Transmitter,
    ExperimentReports,
    DataSource,
    ComputeModule,
    SignalAmplifier,
    ArchiveKey,
    EscapeArchiveFolder,
    Unavailable
}

public interface IGridInteractable
{
    Vector3 InteractionPosition { get; }
    void Interact(Player player);
}

/// <summary>
/// Тип двери — задаёт габариты в проёме и анимацию открытия.
/// Single — одна створка на весь проём, уезжает в левый косяк.
/// Double — двустворчатая (TODO: створки в оба косяка; пока как Single).
/// Vent   — небольшая вентрешётка/люк (полтайла), снимается целиком.
/// </summary>
public enum DoorKind { Single, Double, Vent }

public class PrisonDoor : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private int gridX;
    private int gridY;
    private string displayName;
    private PrisonItemId requirement;
    private DoorKind kind;
    private bool isOpen;
    private bool isSealed;
    private bool hasFirstOpenSide;
    private Vector2Int firstOpenSide;
    private string wrongSideMessage;
    private Action<Player> sealedInteraction;
    private SpriteRenderer spriteRenderer;
    private Vector3 basePosition;
    private Vector3 closedScale;

    // Створка заполняет проём почти целиком; при открытии «уезжает» в левый
    // косяк тонкой полоской на всю высоту (без схлопывания к центру).
    // Размерности — из общего WorldMetrics.
    private const float ClosedFill = WorldMetrics.DoorClosedFill;
    private const float OpenSliver = WorldMetrics.DoorOpenSliver;
    private const float VentFill = 0.55f;   // доля клетки для вентрешётки/люка

    public Vector3 InteractionPosition => grid != null ? grid.GridToWorld(gridX, gridY) : transform.position;
    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);
    public string DisplayName => displayName;
    public bool CanNpcTraverse => requirement == PrisonItemId.None;

    /// <summary>Высота закрытой створки в юнитах (в РОДНЫХ пропорциях арта).</summary>
    public float DoorHeight { get; private set; }

    public void Initialize(GameGrid gameGrid, int x, int y, string name, PrisonItemId requiredItem, Sprite sprite)
    {
        grid = gameGrid;
        gridX = x;
        gridY = y;
        displayName = name;
        requirement = requiredItem;

        // Створка масштабируется РАВНОМЕРНО (по ширине проёма) — без искажения
        // пропорций арта. В плоском top-down это обычный тайл в проёме.
        float uniform = grid.CellSize * ClosedFill / sprite.bounds.size.x;
        DoorHeight = sprite.bounds.size.y * uniform;

        Vector3 cell = grid.GridToWorld(x, y);
        float floorBottom = cell.y - grid.CellSize * 0.5f;
        transform.position = new Vector3(cell.x, floorBottom + DoorHeight * 0.5f, 0f);
        basePosition = transform.position;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;        // арт не тонируем
        // Створка — проп в проёме: над плоским тайлом стены, сортируется по Y.
        spriteRenderer.sortingOrder = SortingLayers.Entity(cell.y);

        closedScale = new Vector3(uniform, uniform, 1f);
        transform.localScale = closedScale;
    }

    public void Interact(Player player)
    {
        if (isSealed)
        {
            if (sealedInteraction != null)
            {
                sealedInteraction(player);
                return;
            }

            DialogueUI.Instance.Show($"{displayName}: дверь заблокирована системой безопасности");
            return;
        }

        if (isOpen)
        {
            // Повторное взаимодействие закрывает открытую дверь (без предмета).
            if (player != null && player.GridPosition == new Vector2Int(gridX, gridY))
            {
                DialogueUI.Instance.Show($"{displayName}: освободите проём, чтобы закрыть");
                return;
            }

            CloseDoor();
            return;
        }

        if (hasFirstOpenSide && (player == null || player.GridPosition != firstOpenSide))
        {
            DialogueUI.Instance.Show(wrongSideMessage, 2.2f);
            return;
        }

        if (requirement == PrisonItemId.Unavailable)
        {
            DialogueUI.Instance.Show($"{displayName}: нужен пропуск высокого уровня");
            return;
        }

        if (requirement != PrisonItemId.None && !player.HasItem(requirement))
        {
            DialogueUI.Instance.Show($"{displayName}: требуется {Player.GetItemName(requirement)}");
            return;
        }

        ApplyOpen();
        hasFirstOpenSide = false;
        DialogueUI.Instance.Show($"{displayName}: открыто");
    }

    private void ApplyOpen()
    {
        isOpen = true;
        grid.SetDoorOpen(gridX, gridY, true);

        // Тонкая полоса на всю высоту, прижатая к левому косяку.
        transform.localScale = new Vector3(closedScale.x * (OpenSliver / ClosedFill), closedScale.y, 1f);
        float dx = grid.CellSize * (ClosedFill - OpenSliver) * 0.5f;
        transform.position = basePosition + new Vector3(-dx, 0f, 0f);
    }

    /// <summary>Принудительно открыть дверь без проверки предмета — для охраны в погоне.</summary>
    public void ForceOpen()
    {
        if (isOpen) return;
        isSealed = false;
        ApplyOpen();
    }

    public void ForceClose()
    {
        if (!isOpen) return;
        CloseDoor();
    }

    public void RequireFirstOpenFrom(Vector2Int approachCell, string message)
    {
        hasFirstOpenSide = true;
        firstOpenSide = approachCell;
        wrongSideMessage = string.IsNullOrEmpty(message)
            ? $"{displayName}: механизм открывается с другой стороны"
            : message;
    }

    private void CloseDoor()
    {
        isOpen = false;
        grid.SetDoorOpen(gridX, gridY, false);
        transform.position = basePosition;
        transform.localScale = closedScale;
        DialogueUI.Instance.Show($"{displayName}: закрыто");
    }

    public void SealClosed()
    {
        isOpen = false;
        isSealed = true;
        grid.SetDoorOpen(gridX, gridY, false);
        transform.position = basePosition;
        transform.localScale = closedScale;
    }

    public void SetSealedInteraction(Action<Player> interaction)
    {
        sealedInteraction = interaction;
    }

    public void UnsealAndOpen(Player player = null)
    {
        isSealed = false;
        if (isOpen) return;
        Interact(player);
    }
}

public sealed class BedInteractable : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;
    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int bedCell, Sprite sprite, Sprite fallback)
    {
        grid = gameGrid;
        cell = bedCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : fallback;
        renderer.color = sprite != null ? Color.white : new Color(0.32f, 0.38f, 0.44f);
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y) - 1;
        float spriteSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        transform.localScale = new Vector3(
            grid.CellSize * 0.85f / Mathf.Max(0.0001f, spriteSize),
            grid.CellSize * 0.55f / Mathf.Max(0.0001f, spriteSize),
            1f);
    }

    public void Interact(Player player)
    {
        if (RunState.DayPhase == DayPhase.LightsOut)
        {
            if (!grid.IsPlayerCell(player.GridPosition))
            {
                DialogueUI.Instance.Show("После отбоя нужно быть в своей камере.", 1.8f);
                return;
            }

            RunState.StartNewDay();
            DayDirector director = FindFirstObjectByType<DayDirector>();
            if (director != null) director.ResetForNewDay();
            player.TeleportToCell(GameGrid.PlayerStartCell);
            DialogueUI.Instance.Show($"День {RunState.Day}. 08:00. Подъём.", 3f);
            return;
        }

        if (RunState.IsRestingInBed)
        {
            RunState.StopRestingInBed();
            DialogueUI.Instance.Show("Вы встали с кровати.", 1.2f);
            return;
        }

        RunState.BeginRestingInBed();
        DialogueUI.Instance.Show("Вы легли на кровать. Время ускорено x10. Нажмите WASD или E у кровати, чтобы встать.", 3f);
    }
}

public sealed class GridPortal : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int destination;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int cell, Vector2Int destinationCell, Sprite sprite)
    {
        grid = gameGrid;
        destination = destinationCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.42f, 0.56f, 0.72f, 0.9f);
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y) - 1;
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * 0.34f / Mathf.Max(0.0001f, spriteSize);
    }

    public void Interact(Player player)
    {
        if (player == null || !grid.IsWalkable(destination.x, destination.y)) return;
        player.TeleportToCell(destination);
        Camera mainCamera = Camera.main;
        CameraFollow follow = mainCamera != null ? mainCamera.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.SnapToTarget();
        DialogueUI.Instance.Show(destination.y >= BlockCPlayableLayout.Floor2OffsetY
            ? "Вы поднялись на второй этаж."
            : "Вы спустились на первый этаж.", 1.2f);
    }
}

public sealed class GardenSmokeSpot : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int spotCell, Sprite sprite)
    {
        grid = gameGrid;
        cell = spotCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.55f, 0.74f, 0.60f);
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y) - 1;
        transform.localScale = Vector3.one * grid.CellSize * 0.36f / Mathf.Max(0.0001f, sprite.bounds.size.x);
    }

    public void Interact(Player player)
    {
        if (!RunState.HasEvidence(EvidenceId.StaffSmokeBreakSchedule))
        {
            DialogueUI.Instance.Show("Здесь тихо. Нужно знать расписание персонала, иначе ждать можно часами.", 2.5f);
            return;
        }

        int minute = RunState.MinuteOfDay;
        if (minute >= 18 * 60 && minute < 18 * 60 + 30)
        {
            if (RunState.HasEvidence(EvidenceId.CookServiceRoute))
            {
                DialogueUI.Instance.Show("Повара уже ушли. Нового вы не услышали.", 1.5f);
                return;
            }

            RunState.MarkGardenConversationHeard(EvidenceId.CookServiceRoute);
            DialogueUI.Instance.ShowDialogueSequence(
                new DialogueUI.DialogueLine("Повар", "Из блока C опять прислали список диет. Как будто им там важнее корм, чем замки.", null),
                new DialogueUI.DialogueLine("Повар", "Я бы не таскал тележки через главный коридор, если бы сад не держал оба крыла вместе.", null),
                new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>Повара используют сад как бытовой служебный маршрут между крыльями.</color>", null));
            return;
        }

        if (minute >= 19 * 60 + 15 && minute < 19 * 60 + 45)
        {
            if (RunState.HasEvidence(EvidenceId.EscapedPrisonerRumor))
            {
                DialogueUI.Instance.Show("Охранники сменили тему. Про прошлый побег больше не говорят.", 1.5f);
                return;
            }

            RunState.MarkGardenConversationHeard(EvidenceId.EscapedPrisonerRumor);
            DialogueUI.Instance.ShowDialogueSequence(
                new DialogueUI.DialogueLine("Надзиратель", "После того беглеца нам запретили оставлять пост без сканирования.", "guard"),
                new DialogueUI.DialogueLine("Надзиратель", "Папка в архиве до сих пор лежит. Там каждый наш промах расписали построчно.", "guard"),
                new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>Уже был успешный побег. Нужно попасть на пост охраны и найти доступ к архиву.</color>", null));
            return;
        }

        if (minute >= 20 * 60 && minute < 20 * 60 + 30)
        {
            if (RunState.HasEvidence(EvidenceId.ScientistGardenRumor))
            {
                DialogueUI.Instance.Show("Учёные уже вернулись в лабораторию.", 1.5f);
                return;
            }

            RunState.MarkGardenConversationHeard(EvidenceId.ScientistGardenRumor);
            DialogueUI.Instance.ShowDialogueSequence(
                new DialogueUI.DialogueLine("Учёный", "Физическая выносливость вторична. Нам нужен момент, где субъект предаёт или спасает без приказа.", null),
                new DialogueUI.DialogueLine("Учёный", "Система подбирает состав так, чтобы у них были личные причины мешать друг другу.", null),
                new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>Эксперименты строятся вокруг социальных решений, а не только выживания.</color>", null));
            return;
        }

        DialogueUI.Instance.ShowDialogue(
            "Сад",
            "Сейчас здесь пусто. По расписанию Ракель: повара 18:00, охрана 19:15, учёные 20:00.",
            null);
    }
}

public sealed class RaquelGardenMeetingSpot : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;
    private SpriteRenderer spriteRenderer;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int meetingCell, Sprite sprite)
    {
        grid = gameGrid;
        cell = meetingCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite != null ? SpriteWalkAnimator.FeetAnchored(sprite) : grid.CreateSquareSprite();
        spriteRenderer.color = sprite != null ? Color.white : new Color(0.7f, 0.35f, 0.55f);
        spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        float spriteSize = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale / Mathf.Max(0.0001f, spriteSize);
        if (sprite != null) SpriteWalkAnimator.TryAttach(gameObject, "girl");
        CharacterGroundShadow.Attach(gameObject);
    }

    private void Update()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.enabled =
            RunState.CompetitorQuest == CompetitorQuestStage.GardenMeetingScheduled ||
            RunState.CompetitorQuest == CompetitorQuestStage.EscapeArchiveFound;
    }

    public void Interact(Player player)
    {
        if (RunState.CompetitorQuest == CompetitorQuestStage.EscapeArchiveFound &&
            RunState.HasPrisonItem(PrisonItemId.EscapeArchiveFolder))
        {
            RunState.GiveEscapeFolderToRaquel();
            DialogueUI.Instance.ShowDialogueSequence(
                new DialogueUI.DialogueLine("Ракель", "Ты всё-таки добрался до архива. Значит, от тебя правда есть польза.", "girl"),
                new DialogueUI.DialogueLine("Ракель", "Они исправили часть старых ошибок, но не все. Я знаю расписание людей, ты нашёл схему их страха.", "girl"),
                new DialogueUI.DialogueLine("Ракель", "Дальше будем планировать побег всерьёз. Не путай это с доверием.", "girl"),
                new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>Ракель готова строить совместный план побега. Продолжение route потребует новых улик и предметов.</color>", null));
            return;
        }

        if (RunState.CompetitorQuest != CompetitorQuestStage.GardenMeetingScheduled)
        {
            DialogueUI.Instance.Show("Ракель здесь нет. Она придёт только после того, как вы докажете полезность.", 2f);
            return;
        }

        if (!RunState.IsRaquelGardenMeetingWindow)
        {
            DialogueUI.Instance.Show("Ракель сказала встретиться у входа в сад в 19:00.", 2f);
            return;
        }

        if (grid == null || player == null) return;
        grid.OpenGardenForRaquelMeeting(player);
    }
}

public sealed class GuardPostScanner : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int scannerCell, Sprite sprite)
    {
        grid = gameGrid;
        cell = scannerCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : grid.CreateSquareSprite();
        renderer.color = new Color(0.42f, 0.95f, 0.72f);
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        float spriteSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * 0.44f / Mathf.Max(0.0001f, spriteSize);
    }

    public void Interact(Player player)
    {
        if (!RunState.HasEvidence(EvidenceId.EscapedPrisonerRumor))
        {
            DialogueUI.Instance.Show("Пост охраны выглядит опасно. Пока непонятно, что здесь искать.", 2f);
            return;
        }

        if (!RunState.MaskingImplantActive)
        {
            RunState.AddEvidence(EvidenceId.GuardPostIdentityScan);
            DialogueUI.Instance.Show("Сканер поста требует личность сотрудника. Нужен активный маскировочный имплант на T.", 2.6f);
            return;
        }

        if (RunState.HasPrisonItem(PrisonItemId.ArchiveKey))
        {
            DialogueUI.Instance.Show("Вы уже забрали доступ к архиву с поста охраны.", 1.6f);
            return;
        }

        RunState.MarkArchiveKeyAcquired();
        DialogueUI.Instance.ShowDialogueSequence(
            new DialogueUI.DialogueLine("Сканер", "Личность подтверждена. Доступ сотрудника активен.", null),
            new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>На посту лежали ключи архива. Теперь можно искать папку о прошлом побеге.</color>", null));
    }
}

public sealed class EscapeArchiveFolderInteractable : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int folderCell, Sprite sprite)
    {
        grid = gameGrid;
        cell = folderCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : grid.CreateSquareSprite();
        renderer.color = new Color(0.95f, 0.78f, 0.36f);
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        float spriteSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * 0.42f / Mathf.Max(0.0001f, spriteSize);
    }

    public void Interact(Player player)
    {
        if (!RunState.HasPrisonItem(PrisonItemId.ArchiveKey))
        {
            DialogueUI.Instance.Show("Архивный шкаф закрыт. Нужны ключи с поста охраны.", 2f);
            return;
        }

        if (RunState.HasPrisonItem(PrisonItemId.EscapeArchiveFolder))
        {
            DialogueUI.Instance.Show("Вы уже забрали папку о сбежавшем заключённом.", 1.5f);
            return;
        }

        RunState.MarkEscapeArchiveFound();
        DialogueUI.Instance.ShowDialogueSequence(
            new DialogueUI.DialogueLine("Архив", "Дело: побег заключённого C-17. После анализа охрана усилила посты, но не закрыла маршрут полностью.", null),
            new DialogueUI.DialogueLine("Архив", "Причины провала: ложная идентификация сотрудника, садовый переход между крыльями, поздняя реакция патруля.", null),
            new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>Эта папка может помочь восстановить маршрут побега. Её можно изучать самому или показать Ракель.</color>", null));
    }
}

public sealed class ShortcutLock : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;
    private bool opened;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, Vector2Int lockCell, Sprite sprite)
    {
        grid = gameGrid;
        cell = lockCell;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.45f, 0.95f, 1f);
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * 0.42f / Mathf.Max(0.0001f, spriteSize);
    }

    public void Interact(Player player)
    {
        if (opened)
        {
            DialogueUI.Instance.Show("Shortcut уже открыт.", 1.2f);
            return;
        }

        if (!RunState.IsImplantActive(ImplantId.EyeImplant))
        {
            DialogueUI.Instance.Show("Панель покрыта скрытыми дорожками. Включите глазной имплант на R.", 2.4f);
            return;
        }

        opened = true;
        grid.OpenBlockCShortcut();
        DialogueUI.Instance.Show("Вы увидели скрытую схему замка и открыли shortcut обратно.", 2.6f);
    }
}

// Предметы на карте реализованы сущностью Item и её наследниками (см. Item.cs).

[Flags]
public enum WireDirection
{
    None = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8,
}

public interface ICircuitPuzzle
{
    void NodeRotated();
}

public readonly struct CircuitNodeSpec
{
    public readonly string DisplayName;
    public readonly Vector2Int Cell;
    public readonly WireDirection Directions;
    public readonly int StartingRotations;
    public readonly bool Rotatable;
    public readonly bool Source;
    public readonly bool Target;

    public CircuitNodeSpec(
        string displayName,
        Vector2Int cell,
        WireDirection directions,
        int startingRotations,
        bool rotatable,
        bool source = false,
        bool target = false)
    {
        DisplayName = displayName;
        Cell = cell;
        Directions = directions;
        StartingRotations = startingRotations;
        Rotatable = rotatable;
        Source = source;
        Target = target;
    }
}

public class EngineeringCircuitPuzzle : MonoBehaviour, ICircuitPuzzle
{
    private const float ScanRadius = 2.35f;

    private readonly List<CircuitNode> nodes = new List<CircuitNode>();
    private readonly Dictionary<Vector2Int, CircuitNode> nodesByCell = new Dictionary<Vector2Int, CircuitNode>();
    private GameGrid grid;
    private PrisonDoor entrance;
    private Player player;
    private bool entranceSealed;
    private bool solved;
    private Vector2Int coordinateOffset;
    private GridArea engineeringArea = new(8, 20, 12, 26);
    private readonly List<Vector2Int> secretPassage = new List<Vector2Int>();

    public void Initialize(
        GameGrid gameGrid,
        PrisonDoor engineeringEntrance,
        Sprite consoleSprite,
        Sprite squareSprite,
        Vector2Int offset = default,
        GridArea? area = null,
        IEnumerable<Vector2Int> passage = null)
    {
        grid = gameGrid;
        entrance = engineeringEntrance;
        coordinateOffset = offset;
        if (area.HasValue) engineeringArea = area.Value;
        if (passage != null) secretPassage.AddRange(passage);

        CreateNode("Источник", At(9, 20), WireDirection.Up, 0, false, true, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 1", At(9, 21), WireDirection.Up | WireDirection.Down, 1, true, false, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 2", At(9, 22), WireDirection.Down | WireDirection.Right, 1, true, false, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 3", At(10, 22), WireDirection.Left | WireDirection.Right, 1, true, false, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 4", At(11, 22), WireDirection.Left | WireDirection.Up, 2, true, false, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 5", At(11, 23), WireDirection.Up | WireDirection.Down, 1, true, false, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 6", At(11, 24), WireDirection.Down | WireDirection.Right, 3, true, false, false, consoleSprite, squareSprite);
        CreateNode("Распределитель 7", At(12, 24), WireDirection.Left | WireDirection.Up, 1, true, false, false, consoleSprite, squareSprite);
        CreateNode("Механизм тайного прохода", At(12, 25), WireDirection.Down, 0, false, false, true, consoleSprite, squareSprite);

        RecalculatePower();
    }

    private void Update()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        if (!entranceSealed && engineeringArea.Contains(player.GridPosition.x, player.GridPosition.y))
        {
            entranceSealed = true;
            if (entrance != null) entrance.SealClosed();
            DialogueUI.Instance.Show("Вход заблокирован. В комнате должен быть другой выход.", 2.5f);
        }

        bool implantInstalled = RunState.IsImplantActive(ImplantId.EyeImplant);
        foreach (CircuitNode node in nodes)
        {
            float distance = Vector2.Distance(player.transform.position, node.transform.position);
            node.SetScanned(implantInstalled && distance <= ScanRadius);
        }
    }

    private Vector2Int At(int x, int y) => new Vector2Int(x, y) + coordinateOffset;

    private void CreateNode(
        string displayName,
        Vector2Int cell,
        WireDirection baseDirections,
        int startingRotations,
        bool rotatable,
        bool source,
        bool target,
        Sprite consoleSprite,
        Sprite squareSprite)
    {
        var nodeObject = new GameObject(displayName);
        nodeObject.transform.SetParent(transform);
        var node = nodeObject.AddComponent<CircuitNode>();
        node.Initialize(this, grid, cell, baseDirections, startingRotations, rotatable, source, target, consoleSprite, squareSprite);
        nodes.Add(node);
        nodesByCell[cell] = node;
    }

    public void NodeRotated()
    {
        RecalculatePower();
    }

    private void RecalculatePower()
    {
        foreach (CircuitNode node in nodes) node.SetPowered(false);

        var queue = new Queue<CircuitNode>();
        foreach (CircuitNode node in nodes)
        {
            if (!node.IsSource) continue;
            node.SetPowered(true);
            queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            CircuitNode current = queue.Dequeue();
            foreach ((WireDirection direction, Vector2Int offset) in DirectionOffsets())
            {
                if (!current.Has(direction)) continue;
                Vector2Int neighborCell = current.Cell + offset;
                if (!nodesByCell.TryGetValue(neighborCell, out CircuitNode neighbor)) continue;
                if (!neighbor.Has(Opposite(direction)) || neighbor.IsPowered) continue;
                neighbor.SetPowered(true);
                queue.Enqueue(neighbor);
            }
        }

        if (!solved && nodes.Exists(node => node.IsTarget && node.IsPowered))
        {
            solved = true;
            OpenSecretPassage();
        }
    }

    private void OpenSecretPassage()
    {
        if (secretPassage.Count > 0)
        {
            foreach (Vector2Int cell in secretPassage) grid.SetTileAndRefresh(cell.x, cell.y, TileType.Floor);
        }
        else
        {
            for (int x = 13; x <= 18; x++) grid.SetTileAndRefresh(x, 25, TileType.Floor);
            for (int y = 23; y <= 24; y++) grid.SetTileAndRefresh(18, y, TileType.Floor);
        }
        if (Application.isPlaying)
        {
            DialogueUI.Instance.Show("Цепь замкнута. В стене открылся технический проход.", 3f);
        }
    }

    public static IEnumerable<(WireDirection, Vector2Int)> DirectionOffsets()
    {
        yield return (WireDirection.Up, Vector2Int.up);
        yield return (WireDirection.Right, Vector2Int.right);
        yield return (WireDirection.Down, Vector2Int.down);
        yield return (WireDirection.Left, Vector2Int.left);
    }

    public static WireDirection Opposite(WireDirection direction)
    {
        return direction switch
        {
            WireDirection.Up => WireDirection.Down,
            WireDirection.Right => WireDirection.Left,
            WireDirection.Down => WireDirection.Up,
            WireDirection.Left => WireDirection.Right,
            _ => WireDirection.None,
        };
    }
}

public sealed class ProgrammerCircuitPuzzle : MonoBehaviour, ICircuitPuzzle
{
    private const float ScanRadius = 2.35f;

    private readonly List<CircuitNode> nodes = new List<CircuitNode>();
    private readonly Dictionary<Vector2Int, CircuitNode> nodesByCell = new Dictionary<Vector2Int, CircuitNode>();
    private GameGrid grid;
    private Player player;
    private PrisonItemId rewardItem;
    private string solvedMessage;
    private bool solved;

    public void Initialize(
        GameGrid gameGrid,
        PrisonItemId reward,
        string message,
        IEnumerable<CircuitNodeSpec> specs,
        Sprite consoleSprite,
        Sprite squareSprite)
    {
        grid = gameGrid;
        rewardItem = reward;
        solvedMessage = message;

        foreach (CircuitNodeSpec spec in specs)
        {
            CreateNode(spec, consoleSprite, squareSprite);
        }

        RecalculatePower();
    }

    private void Update()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        bool implantActive = RunState.IsImplantActive(ImplantId.EyeImplant);
        foreach (CircuitNode node in nodes)
        {
            float distance = Vector2.Distance(player.transform.position, node.transform.position);
            node.SetScanned(implantActive && distance <= ScanRadius);
        }
    }

    private void CreateNode(CircuitNodeSpec spec, Sprite consoleSprite, Sprite squareSprite)
    {
        var nodeObject = new GameObject(spec.DisplayName);
        nodeObject.transform.SetParent(transform);
        var node = nodeObject.AddComponent<CircuitNode>();
        node.Initialize(
            this,
            grid,
            spec.Cell,
            spec.Directions,
            spec.StartingRotations,
            spec.Rotatable,
            spec.Source,
            spec.Target,
            consoleSprite,
            squareSprite);
        nodes.Add(node);
        nodesByCell[spec.Cell] = node;
    }

    public void NodeRotated()
    {
        RecalculatePower();
    }

    private void RecalculatePower()
    {
        foreach (CircuitNode node in nodes) node.SetPowered(false);

        var queue = new Queue<CircuitNode>();
        foreach (CircuitNode node in nodes)
        {
            if (!node.IsSource) continue;
            node.SetPowered(true);
            queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            CircuitNode current = queue.Dequeue();
            foreach ((WireDirection direction, Vector2Int offset) in EngineeringCircuitPuzzle.DirectionOffsets())
            {
                if (!current.Has(direction)) continue;
                Vector2Int neighborCell = current.Cell + offset;
                if (!nodesByCell.TryGetValue(neighborCell, out CircuitNode neighbor)) continue;
                if (!neighbor.Has(EngineeringCircuitPuzzle.Opposite(direction)) || neighbor.IsPowered) continue;
                neighbor.SetPowered(true);
                queue.Enqueue(neighbor);
            }
        }

        if (!solved && nodes.Exists(node => node.IsTarget && node.IsPowered))
        {
            solved = true;
            RunState.AddPrisonItem(rewardItem);
            if (Application.isPlaying)
            {
                DialogueUI.Instance.Show(solvedMessage, 3f);
            }
        }
    }
}

public class CircuitNode : MonoBehaviour, IGridInteractable
{
    private static readonly Color HiddenWire = new Color(0f, 0f, 0f, 0f);
    private static readonly Color IdleWire = new Color(0.2f, 0.75f, 0.95f, 0.72f);
    private static readonly Color PoweredWire = new Color(1f, 0.78f, 0.18f, 1f);

    private readonly List<SpriteRenderer> wireRenderers = new List<SpriteRenderer>();
    private ICircuitPuzzle puzzle;
    private WireDirection baseDirections;
    private int rotations;
    private bool rotatable;
    private bool scanned;
    private SpriteRenderer objectRenderer;
    private Player nearbyPlayer;

    public Vector2Int Cell { get; private set; }
    public bool IsSource { get; private set; }
    public bool IsTarget { get; private set; }
    public bool IsPowered { get; private set; }
    public Vector3 InteractionPosition => transform.position;

    public void Initialize(
        ICircuitPuzzle owner,
        GameGrid grid,
        Vector2Int cell,
        WireDirection directions,
        int startingRotations,
        bool canRotate,
        bool source,
        bool target,
        Sprite consoleSprite,
        Sprite squareSprite)
    {
        puzzle = owner;
        Cell = cell;
        baseDirections = directions;
        rotations = startingRotations % 4;
        rotatable = canRotate;
        IsSource = source;
        IsTarget = target;
        transform.position = grid.GridToWorld(cell.x, cell.y);

        var objectVisual = new GameObject("Fixture");
        objectVisual.transform.SetParent(transform);
        objectVisual.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        objectRenderer = objectVisual.AddComponent<SpriteRenderer>();
        objectRenderer.sprite = consoleSprite != null ? consoleSprite : squareSprite;
        objectRenderer.color = source ? new Color(0.35f, 1f, 0.45f)
            : target ? new Color(1f, 0.35f, 0.25f)
            : Color.white;
        objectRenderer.sortingOrder = SortingLayers.Entity(transform.position.y) - 1;
        float spriteSize = Mathf.Max(objectRenderer.sprite.bounds.size.x, objectRenderer.sprite.bounds.size.y);
        objectVisual.transform.localScale = Vector3.one * grid.CellSize * 0.42f / spriteSize;
        objectVisual.transform.rotation = Quaternion.Euler(0f, 0f, -rotations * 90f);

        foreach ((WireDirection direction, _) in EngineeringCircuitPuzzle.DirectionOffsets())
        {
            var segment = new GameObject($"Wire_{direction}");
            segment.transform.SetParent(transform);
            segment.transform.localPosition = DirectionVector(direction) * grid.CellSize * 0.25f;
            segment.transform.localScale = direction == WireDirection.Up || direction == WireDirection.Down
                ? new Vector3(grid.CellSize * 0.10f, grid.CellSize * 0.55f, 1f)
                : new Vector3(grid.CellSize * 0.55f, grid.CellSize * 0.10f, 1f);
            var renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.sortingOrder = SortingLayers.Floor + 2;
            wireRenderers.Add(renderer);
        }

        RefreshVisuals();
    }

    public bool Has(WireDirection direction) => (CurrentDirections() & direction) != 0;

    public void SetPowered(bool powered)
    {
        IsPowered = powered;
        RefreshVisuals();
    }

    public void SetScanned(bool isScanned)
    {
        if (scanned == isScanned) return;
        scanned = isScanned;
        RefreshVisuals();
    }

    public void Interact(Player player)
    {
        if (!rotatable)
        {
            DialogueUI.Instance.Show(IsSource ? "Источник аварийного питания." : "Механизм скрыт внутри стены.");
            return;
        }

        rotations = (rotations + 1) % 4;
        objectRenderer.transform.rotation = Quaternion.Euler(0f, 0f, -rotations * 90f);
        puzzle.NodeRotated();
    }

    private WireDirection CurrentDirections()
    {
        WireDirection result = baseDirections;
        for (int i = 0; i < rotations; i++) result = RotateClockwise(result);
        return result;
    }

    private void RefreshVisuals()
    {
        WireDirection directions = CurrentDirections();
        int index = 0;
        foreach ((WireDirection direction, _) in EngineeringCircuitPuzzle.DirectionOffsets())
        {
            SpriteRenderer renderer = wireRenderers[index++];
            renderer.gameObject.SetActive((directions & direction) != 0);
            renderer.color = scanned ? (IsPowered ? PoweredWire : IdleWire) : HiddenWire;
        }
    }

    private static WireDirection RotateClockwise(WireDirection directions)
    {
        WireDirection result = WireDirection.None;
        if ((directions & WireDirection.Up) != 0) result |= WireDirection.Right;
        if ((directions & WireDirection.Right) != 0) result |= WireDirection.Down;
        if ((directions & WireDirection.Down) != 0) result |= WireDirection.Left;
        if ((directions & WireDirection.Left) != 0) result |= WireDirection.Up;
        return result;
    }

    private static Vector3 DirectionVector(WireDirection direction)
    {
        return direction switch
        {
            WireDirection.Up => Vector3.up,
            WireDirection.Right => Vector3.right,
            WireDirection.Down => Vector3.down,
            WireDirection.Left => Vector3.left,
            _ => Vector3.zero,
        };
    }

    private void OnGUI()
    {
        if (QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen) return;
        if (!rotatable) return;
        if (nearbyPlayer == null) nearbyPlayer = FindFirstObjectByType<Player>();
        if (nearbyPlayer == null || Vector2.Distance(transform.position, nearbyPlayer.transform.position) > 1.2f) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(transform.position);
        if (screenPosition.z < 0f) return;

        GUI.Box(new Rect(
            screenPosition.x - 62f,
            Screen.height - screenPosition.y - 48f,
            124f,
            24f
        ), "E — повернуть");
    }
}
