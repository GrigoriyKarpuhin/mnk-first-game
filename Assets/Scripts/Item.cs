using UnityEngine;

/// <summary>
/// Базовая сущность предмета, лежащего на карте тюрьмы. Рисует иконку, показывает
/// подпись с названием над собой при приближении игрока и подбирается по E.
/// Конкретные предметы — наследники с заданными <see cref="ItemId"/>, цветом и названием.
/// </summary>
public abstract class Item : MonoBehaviour, IGridInteractable
{
    private const float LabelRange = 2.4f;

    protected GameGrid grid;
    private SpriteRenderer iconRenderer;
    private TextMesh label;
    private Player player;

    public abstract PrisonItemId ItemId { get; }
    public abstract Color IconColor { get; }
    public abstract string DisplayName { get; }

    public Vector3 InteractionPosition => transform.position;

    public void Initialize(GameGrid gameGrid, int x, int y, Sprite sprite, bool tintIcon = true)
    {
        grid = gameGrid;
        if (RunState.HasPrisonItem(ItemId))
        {
            Destroy(gameObject);
            return;
        }

        transform.position = grid.GridToWorld(x, y);

        // Иконка — отдельный дочерний объект, чтобы масштаб не влиял на размер подписи.
        var icon = new GameObject("Icon");
        icon.transform.SetParent(transform);
        icon.transform.localPosition = Vector3.zero;
        icon.transform.localScale = Vector3.one * grid.CellSize * 0.42f;
        iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = sprite;
        iconRenderer.color = tintIcon ? IconColor : Color.white;
        iconRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);

        CreateLabel();
    }

    private void CreateLabel()
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform);
        labelObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);

        label = labelObject.AddComponent<TextMesh>();
        label.text = DisplayName;
        label.fontSize = 40;
        label.characterSize = 0.06f;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var meshRenderer = labelObject.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = label.font.material;
        meshRenderer.sortingOrder = SortingLayers.Entity(transform.position.y) + 10;

        labelObject.SetActive(false);
    }

    private void Update()
    {
        if (label == null) return;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        bool show = Vector2.Distance(transform.position, player.transform.position) <= LabelRange;
        if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
    }

    public void Interact(Player picker)
    {
        picker.AddItem(ItemId);
        picker.PlayPickupAnimation();
        DialogueUI.Instance.Show($"Получено: {DisplayName}");
        OnPicked(picker);
        Destroy(gameObject);
    }

    /// <summary>Конкретное поведение при подборе (по умолчанию ничего).</summary>
    protected virtual void OnPicked(Player picker) { }
}

public sealed class ScrewdriverItem : Item
{
    public override PrisonItemId ItemId => PrisonItemId.Screwdriver;
    public override Color IconColor => new Color(0.70f, 0.75f, 0.80f);
    public override string DisplayName => "Отвёртка";
}

public sealed class KitchenManifestItem : Item
{
    public override PrisonItemId ItemId => PrisonItemId.KitchenManifest;
    public override Color IconColor => new Color(0.95f, 0.90f, 0.55f);
    public override string DisplayName => "Лист приёмки кухни";
}

public sealed class ServiceBadgeItem : Item
{
    public override PrisonItemId ItemId => PrisonItemId.ServiceBadge;
    public override Color IconColor => new Color(0.35f, 0.80f, 0.95f);
    public override string DisplayName => "Служебный пропуск";
}

public sealed class EyeImplantItem : Item
{
    public override PrisonItemId ItemId => PrisonItemId.EyeImplant;
    public override Color IconColor => new Color(0.45f, 0.95f, 1f);
    public override string DisplayName => "Глазной имплант";

    protected override void OnPicked(Player picker)
    {
        RunState.AddImplant(ImplantId.EyeImplant);
        DialogueUI.Instance.Show("Глазной имплант установлен. Скрытые провода видны только вблизи.", 3f);
    }
}

public sealed class TransmitterItem : Item
{
    public override PrisonItemId ItemId => PrisonItemId.Transmitter;
    public override Color IconColor => new Color(0.35f, 0.95f, 0.55f);
    public override string DisplayName => "Передатчик";

    protected override void OnPicked(Player picker)
    {
        if (RunState.ProgrammerQuest == ProgrammerQuestStage.TransmitterAcquired)
        {
            DialogueUI.Instance.Show("Передатчик найден. Вернитесь к программисту.", 3f);
        }
    }
}

public sealed class ExperimentReportsItem : Item
{
    public override PrisonItemId ItemId => PrisonItemId.ExperimentReports;
    public override Color IconColor => new Color(0.90f, 0.45f, 0.45f);
    public override string DisplayName => "Отчёты экспериментов";
}
