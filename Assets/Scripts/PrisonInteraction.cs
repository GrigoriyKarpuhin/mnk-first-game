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

public class PrisonDoor : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private int gridX;
    private int gridY;
    private string displayName;
    private PrisonItemId requirement;
    private bool isOpen;
    private SpriteRenderer spriteRenderer;
    private Vector3 basePosition;
    private Vector3 closedScale;

    // Створка заполняет проём почти целиком; при открытии «уезжает» в левый
    // косяк тонкой полоской на всю высоту (без схлопывания к центру).
    // Размерности — из общего WorldMetrics.
    private const float ClosedFill = WorldMetrics.DoorClosedFill;
    private const float OpenSliver = WorldMetrics.DoorOpenSliver;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, int x, int y, string name, PrisonItemId requiredItem, Sprite sprite)
    {
        grid = gameGrid;
        gridX = x;
        gridY = y;
        displayName = name;
        requirement = requiredItem;
        transform.position = grid.GridToWorld(x, y);
        basePosition = transform.position;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;        // арт не тонируем
        spriteRenderer.sortingOrder = SortingLayers.Wall(transform.position.y);

        float spriteUnit = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        closedScale = Vector3.one * grid.CellSize * ClosedFill / spriteUnit;
        closedScale.z = 1f;
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
