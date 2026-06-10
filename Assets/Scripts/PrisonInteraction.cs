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
        CreateLabel($"E: {displayName}", new Color(1f, 0.75f, 0.65f));
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
        spriteRenderer.color = new Color(0.25f, 0.75f, 0.35f, 0.35f);
        transform.localScale = new Vector3(grid.CellSize * 0.18f, grid.CellSize * 0.95f, 1f);
        DialogueUI.Instance.Show($"{displayName}: открыто");
    }

    private void CreateLabel(string text, Color color)
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform);
        labelObject.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        labelObject.transform.localScale = Vector3.one * 1.25f;

        var label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.characterSize = 0.035f;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = color;
        label.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Foreground(transform.position.y);
    }
}

public class PrisonItemPickup : MonoBehaviour, IGridInteractable
{
    private PrisonItemId itemId;
    private string displayName;

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid grid, int x, int y, PrisonItemId id, string name, Color color, Sprite sprite)
    {
        itemId = id;
        displayName = name;

        if (RunState.HasPrisonItem(id))
        {
            Destroy(gameObject);
            return;
        }

        transform.position = grid.GridToWorld(x, y);
        transform.localScale = Vector3.one * grid.CellSize * 0.42f;

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        CreateLabel($"E: {displayName}");
    }

    public void Interact(Player player)
    {
        player.AddItem(itemId);
        DialogueUI.Instance.Show($"Получено: {displayName}");
        Destroy(gameObject);
    }

    private void CreateLabel(string text)
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform);
        labelObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        labelObject.transform.localScale = Vector3.one * 2.2f;

        var label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.characterSize = 0.03f;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Foreground(transform.position.y);
    }
}
