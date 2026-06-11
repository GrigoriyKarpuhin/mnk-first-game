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

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, int x, int y, string name, PrisonItemId requiredItem, Sprite sprite)
    {
        grid = gameGrid;
        gridX = x;
        gridY = y;
        displayName = name;
        requirement = requiredItem;
        transform.position = grid.GridToWorld(x, y);
        transform.localScale = new Vector3(grid.CellSize * 0.8f, grid.CellSize * 0.95f, 1f);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = requirement == PrisonItemId.None
            ? new Color(0.55f, 0.7f, 0.6f)
            : new Color(0.65f, 0.22f, 0.18f);
        spriteRenderer.sortingOrder = SortingLayers.Wall(transform.position.y);
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
        spriteRenderer.color = new Color(0.3f, 0.75f, 0.4f, 1f);
        transform.localScale = new Vector3(grid.CellSize * 0.82f, grid.CellSize * 0.18f, 1f);
        DialogueUI.Instance.Show($"{displayName}: открыто");
    }
}

// Предметы на карте реализованы сущностью Item и её наследниками (см. Item.cs).
