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

    /// <summary>Будит охрану — ближайшие патрули переходят в розыск (как нарушение расписания).</summary>
    SummonGuards,

    /// <summary>Поднимает глобальную тревогу (RunState.RaiseAlarm).</summary>
    Alarm,
}

/// <summary>
/// Камера наблюдения (CCTV). Стоит неподвижно, всегда «включена» и рисует свою зону
/// обзора тем же конусом, что и фонарики охраны.
///
/// Камера С последствиями (<see cref="CameraResponse.SummonGuards"/>/<see cref="CameraResponse.Alarm"/>)
/// реагирует не мгновенно: пока игрок в конусе, копит тревогу той же лестницей, что и
/// охрана (<see cref="AwarenessMeter"/>), показывает «?» → «!» и тонирует конус; реакция
/// срабатывает лишь на полной тревоге. ЛЕГАЛЬНАЯ камера (<see cref="CameraResponse.None"/>)
/// лестницу/индикатор НЕ запускает — только нагнетает «ужас» разовой репликой, иначе
/// «!» обещал бы последствие, которого нет.
/// </summary>
public sealed class SurveillanceCamera : MonoBehaviour, IVisionSource
{
    private GameGrid grid;
    private Vector2Int gridPosition;
    private Vector2Int facing = Vector2Int.down;
    private int visionRange = 6;
    private string zone = "";
    private CameraResponse response = CameraResponse.None;

    // Лестница обнаружения камеры (как у охраны, но чуть медленнее — даёт время уйти
    // из конуса, прежде чем камера поднимет тревогу).
    [SerializeField] private float suspicionThreshold = 0.35f;
    [SerializeField] private float detectGainNear = 1.8f;
    [SerializeField] private float detectGainFar = 0.7f;
    [SerializeField] private float alertDecay = 0.6f;

    // Скольких охранников будит камера на полной тревоге: только ближайших N, а не всех.
    [SerializeField] private int summonGuardCount = 3;

    private readonly AwarenessMeter awareness = new();
    private bool triggered;     // защёлка: реакция/реплика уже сработала на текущем контакте
    private int coneBand = -1;  // 0 спокойна / 1 подозрение / 2 тревога — для смены цвета конуса

    /// <summary>Есть ли у камеры последствие (иначе она только нагнетает «ужас»).</summary>
    private bool HasConsequence => response != CameraResponse.None;

    private Player player;
    private VisionConeRenderer visionCone;

    // Конус камеры: холодно-красная слежка → жёлтое подозрение → красная тревога.
    private static readonly Color CameraIdleColor = new(0.85f, 0.2f, 0.28f, 0.16f);
    private static readonly Color CameraWarnColor = new(1f, 0.82f, 0.16f, 0.24f);
    private static readonly Color CameraAlarmColor = new(0.95f, 0.2f, 0.15f, 0.34f);

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

        visionCone = VisionConeRenderer.Attach(this, gameObject, CameraIdleColor);
    }

    public bool CanSeeCell(Vector2Int cell) =>
        VisionMath.CanSeeCell(grid, gridPosition, facing, visionRange, cell);

    private void Update()
    {
        if (grid == null) return;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        // Заметность игрока (приседание/движение/укрытие/маскировка) масштабирует детект;
        // в маскировке под охрану StealthExposure = 0 → камера «слепнет».
        float exposure = player.StealthExposure;
        bool visible = exposure > 0.001f && CanSeeCell(player.GridPosition);

        // Легальная зона: ни лестницы, ни шкалы «?»/«!» (они обещали бы последствие).
        // Только разовый «ужас» при попадании в кадр, со сбросом при выходе.
        if (!HasConsequence)
        {
            if (visible && !triggered)
            {
                triggered = true;
                DialogueUI.Instance.Show("Камера наблюдения провожает вас взглядом…", 1.4f);
            }
            else if (!visible)
            {
                triggered = false;
            }
            return;
        }

        float normalizedDistance = 1f;
        if (visible)
        {
            Vector2Int delta = player.GridPosition - gridPosition;
            int forwardDistance = delta.x * facing.x + delta.y * facing.y;
            normalizedDistance = Mathf.Clamp01((float)forwardDistance / Mathf.Max(1, visionRange));
        }

        awareness.Tick(visible, normalizedDistance, Time.deltaTime,
            detectGainNear * exposure, detectGainFar * exposure, alertDecay);

        UpdateConeColor();

        if (awareness.Level >= 1f)
        {
            if (!triggered)
            {
                triggered = true;
                OnFullAlert();
            }
        }
        else if (awareness.Level < suspicionThreshold * 0.5f)
        {
            // Успокоилась (игрок ушёл из конуса) — снова готова поднять тревогу.
            triggered = false;
        }
    }

    /// <summary>Цвет конуса по бэндам тревоги (как смена цвета фонарика у охраны).</summary>
    private void UpdateConeColor()
    {
        if (visionCone == null) return;
        int band = awareness.Level >= 1f ? 2 : awareness.Level >= suspicionThreshold ? 1 : 0;
        if (band == coneBand) return;
        coneBand = band;
        visionCone.SetColor(band switch
        {
            2 => CameraAlarmColor,
            1 => CameraWarnColor,
            _ => CameraIdleColor
        });
    }

    // Вызывается только для камер с последствием (None обрабатывается в Update без лестницы).
    private void OnFullAlert()
    {
        switch (response)
        {
            case CameraResponse.SummonGuards:
                SummonNearestGuards();
                DialogueUI.Instance.Show("Камера засекла вас — охрана поднята!", 1.8f);
                break;

            case CameraResponse.Alarm:
                if (RunState.RaiseAlarm())
                    DialogueUI.Instance.Show("Камера подняла тревогу!", 2f);
                break;
        }
    }

    /// <summary>
    /// Будит не всех охранников, а ближайших <see cref="summonGuardCount"/> к месту
    /// обнаружения (по дистанции на сетке), пропуская оглушённых. Направляем их ровно
    /// в клетку, где камера засекла игрока, — иначе патруль шёл бы к устаревшей точке.
    /// </summary>
    private void SummonNearestGuards()
    {
        Vector2Int spotted = player.GridPosition;
        GuardPatrol[] guards = FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None);

        System.Array.Sort(guards, (a, b) =>
            DistanceSq(a.GridPosition, spotted).CompareTo(DistanceSq(b.GridPosition, spotted)));

        int roused = 0;
        foreach (GuardPatrol guard in guards)
        {
            if (roused >= summonGuardCount) break;
            if (guard.State == GuardState.Disabled) continue; // оглушённого будить нечем
            guard.StartScheduleSearch(spotted);
            roused++;
        }
    }

    private static int DistanceSq(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    private void OnGUI()
    {
        if (!HasConsequence) return; // легальная камера не рисует шкалу «?»/«!»
        if (QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen) return;
        if (grid == null) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        float level = awareness.Level;
        if (level <= 0.02f) return;

        // Индикатор тревоги над камерой (тот же, что у охраны): шкала + «?»/«!».
        AlertIndicator.DrawMeter(mainCamera,
            transform.position + Vector3.up * grid.CellSize, level, level >= 1f);

        string glyph = level >= 1f ? "!" : level >= suspicionThreshold ? "?" : null;
        AlertIndicator.DrawGlyph(mainCamera,
            transform.position + Vector3.up * grid.CellSize * 1.35f, glyph);
    }
}
