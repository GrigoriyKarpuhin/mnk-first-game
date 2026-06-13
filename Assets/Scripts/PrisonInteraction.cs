using UnityEngine;

public enum PrisonItemId
{
    None,
    Screwdriver,
    KitchenManifest,
    ServiceBadge,
    EyeImplant,
    ExperimentReports,
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
    private SpriteRenderer spriteRenderer;
    private Vector3 basePosition;
    private Vector3 closedScale;

    // Створка заполняет проём почти целиком; при открытии «уезжает» в левый
    // косяк тонкой полоской на всю высоту (без схлопывания к центру).
    // Размерности — из общего WorldMetrics.
    private const float ClosedFill = WorldMetrics.DoorClosedFill;
    private const float OpenSliver = WorldMetrics.DoorOpenSliver;
    private const float VentFill = 0.55f;   // доля клетки для вентрешётки/люка

    public Vector3 InteractionPosition => transform.position;

    /// <summary>Высота закрытой створки в юнитах (в РОДНЫХ пропорциях арта).</summary>
    public float DoorHeight { get; private set; }

    /// <summary>Мировой Y верха закрытой створки — отсюда GameGrid строит перемычку.</summary>
    public float TopWorldY => basePosition.y + DoorHeight * 0.5f;

    public void Initialize(GameGrid gameGrid, int x, int y, string name, PrisonItemId requiredItem, Sprite sprite)
    {
        grid = gameGrid;
        gridX = x;
        gridY = y;
        displayName = name;
        requirement = requiredItem;

        // Створка масштабируется РАВНОМЕРНО (по ширине проёма) — без искажения
        // пропорций арта. По высоте дверь обычно ниже стены; оставшийся проём
        // сверху закрывает бетонная перемычка (её строит GameGrid, см.
        // CreateDoor → CreateLintel).
        float uniform = grid.CellSize * ClosedFill / sprite.bounds.size.x;
        DoorHeight = sprite.bounds.size.y * uniform;

        Vector3 cell = grid.GridToWorld(x, y);
        float floorBottom = cell.y - grid.CellSize * 0.5f;
        transform.position = new Vector3(cell.x, floorBottom + DoorHeight * 0.5f, 0f);
        basePosition = transform.position;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;        // арт не тонируем
        spriteRenderer.sortingOrder = SortingLayers.Wall(cell.y);

        closedScale = new Vector3(uniform, uniform, 1f);
        transform.localScale = closedScale;
    }

    public void Interact(Player player)
    {
        if (isOpen)
        {
            DialogueUI.Instance.Show($"{displayName}: уже открыто");
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

        isOpen = true;
        grid.SetDoorOpen(gridX, gridY, true);

        // Тонкая полоса на всю высоту, прижатая к левому косяку.
        transform.localScale = new Vector3(closedScale.x * (OpenSliver / ClosedFill), closedScale.y, 1f);
        float dx = grid.CellSize * (ClosedFill - OpenSliver) * 0.5f;
        transform.position = basePosition + new Vector3(-dx, 0f, 0f);
        DialogueUI.Instance.Show($"{displayName}: открыто");
    }
}

// Предметы на карте реализованы сущностью Item и её наследниками (см. Item.cs).
