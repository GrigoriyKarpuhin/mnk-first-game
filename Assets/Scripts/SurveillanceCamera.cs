using UnityEngine;

/// <summary>
/// Реакция камеры на обнаружение игрока. Расширяемый хук: по умолчанию камера
/// ничего не делает (только видит и создаёт «ужас»), но конкретную камеру в
/// конкретной зоне можно настроить на призыв охраны или тревогу.
/// </summary>
public enum CameraResponse
{
    /// <summary>Ничего не делает: только смотрит и нагнетает (камеры в блоке наблюдения).</summary>
    None,

    /// <summary>Будит охрану — все патрули переходят в розыск (как нарушение расписания).</summary>
    SummonGuards,

    /// <summary>Поднимает глобальную тревогу (RunState.RaiseAlarm).</summary>
    Alarm,
}

/// <summary>
/// Камера наблюдения (CCTV). Стоит неподвижно, всегда «включена» и рисует свою зону
/// обзора тем же конусом, что и фонарики охраны. При попадании игрока в зону вызывает
/// настроенную реакцию <see cref="CameraResponse"/> — хук для зональных триггеров.
/// </summary>
public sealed class SurveillanceCamera : MonoBehaviour, IVisionSource
{
    private GameGrid grid;
    private Vector2Int gridPosition;
    private Vector2Int facing = Vector2Int.down;
    private int visionRange = 6;
    private string zone = "";
    private CameraResponse response = CameraResponse.None;

    private Player player;
    private bool playerInView;

    // Холодно-красный конус: считывается как «слежка», отличается от янтаря охраны.
    private static readonly Color CameraConeColor = new(0.85f, 0.2f, 0.28f, 0.16f);

    public GameGrid Grid => grid;
    public Vector2Int GridPosition => gridPosition;
    public Vector2Int Facing => facing;
    public int VisionRange => visionRange;
    public bool VisionActive => grid != null;

    /// <summary>Зона/место камеры — для зональной логики реакций.</summary>
    public string Zone => zone;

    /// <summary>Текущая реакция камеры на обнаружение (по умолчанию None).</summary>
    public CameraResponse Response { get => response; set => response = value; }

    public void Initialize(
        GameGrid gameGrid,
        Vector2Int cell,
        Vector2Int cameraFacing,
        int range,
        string zoneLabel,
        CameraResponse cameraResponse,
        Sprite sprite)
    {
        grid = gameGrid;
        gridPosition = cell;
        facing = cameraFacing == Vector2Int.zero ? Vector2Int.down : cameraFacing;
        visionRange = Mathf.Max(1, range);
        zone = zoneLabel ?? "";
        response = cameraResponse;

        Vector3 worldPos = grid.GridToWorld(cell.x, cell.y);
        transform.position = worldPos;

        var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = sprite != null ? Color.white : new Color(0.18f, 0.2f, 0.24f);
        float spriteUnit = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        transform.localScale = Vector3.one * grid.CellSize * 0.9f / Mathf.Max(0.0001f, spriteUnit);
        spriteRenderer.sortingOrder = SortingLayers.Foreground(worldPos.y);

        VisionConeRenderer.Attach(this, gameObject, CameraConeColor);
    }

    public bool CanSeeCell(Vector2Int cell) =>
        VisionMath.CanSeeCell(grid, gridPosition, facing, visionRange, cell);

    private void Update()
    {
        if (grid == null) return;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        if (player.IsDisguisedAsGuard)
        {
            playerInView = false;
            return;
        }

        bool seesPlayer = CanSeeCell(player.GridPosition);

        // Реагируем один раз на вход в зону, сбрасываемся при выходе.
        if (seesPlayer && !playerInView) OnPlayerSpotted();
        playerInView = seesPlayer;
    }

    private void OnPlayerSpotted()
    {
        switch (response)
        {
            case CameraResponse.SummonGuards:
                SummonGuards();
                DialogueUI.Instance.Show("Камера засекла вас — охрана поднята!", 1.8f);
                break;

            case CameraResponse.Alarm:
                if (RunState.RaiseAlarm())
                    DialogueUI.Instance.Show("Камера подняла тревогу!", 2f);
                break;

            case CameraResponse.None:
            default:
                // Только «ужас»: камера провожает игрока взглядом, без последствий.
                DialogueUI.Instance.Show("Камера наблюдения провожает вас взглядом…", 1.4f);
                break;
        }
    }

    private static void SummonGuards()
    {
        GuardPatrol[] guards = FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None);
        foreach (GuardPatrol guard in guards) guard.StartScheduleSearch();
    }
}
