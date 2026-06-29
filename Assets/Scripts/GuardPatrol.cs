using System.Collections.Generic;
using UnityEngine;

public enum GuardState
{
    Patrol,
    Suspicious,
    Investigate,
    Search,
    Chase,
    Disabled
}

/// <summary>Точка патрульного маршрута. <see cref="Scan"/> — осмотреться по сторонам на ней.</summary>
public struct PatrolWaypoint
{
    public Vector2Int Cell;
    public bool Scan;

    public PatrolWaypoint(Vector2Int cell, bool scan = false)
    {
        Cell = cell;
        Scan = scan;
    }

    public static implicit operator PatrolWaypoint(Vector2Int cell) => new(cell);
}

public class GuardPatrol : MonoBehaviour, IVisionSource
{
    [SerializeField] private float patrolSpeed = 2.2f;
    [SerializeField] private float investigateSpeed = 3.0f;
    [SerializeField] private float chaseSpeed = 4.1f;
    [SerializeField] private int visionRange = 7;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackCooldown = 0.9f;

    // Лестница обнаружения (MGS): тревога копится, переходы по порогам.
    [SerializeField] private float suspicionThreshold = 0.4f;
    [SerializeField] private float detectGainNear = 2.5f;
    [SerializeField] private float detectGainFar = 0.8f;
    [SerializeField] private float alertDecay = 0.5f;
    [SerializeField] private float chaseLoseSightTime = 2.5f;

    // Осмотр «взглядом»: на точке охранник коротко глядит в 1–2 стороны
    // (по возможности — туда, где есть ящик/укрытие), а не крутится на месте.
    [SerializeField] private float glanceHoldMin = 1.6f;
    [SerializeField] private float glanceHoldMax = 2.8f;

    private GameGrid grid;
    private PatrolWaypoint[] route;
    private int destinationIndex = 1;
    private Vector2Int gridPosition;
    private Vector2Int nextGridPosition;
    private Vector2Int facing = Vector2Int.right;
    private Vector3 targetPosition;
    private bool isMoving;
    private float nextAttackTime;
    private SpriteRenderer spriteRenderer;
    private SpriteWalkAnimator walkAnimator;
    private Player player;
    private GuardState state = GuardState.Patrol;
    private VisionConeRenderer visionCone;

    // Память и таймеры стелс-поведения.
    private readonly AwarenessMeter awareness = new();
    private Vector2Int lastKnownPlayerCell;
    private float chaseLostTimer;

    // Осмотр взглядом: очередь сторон, в которые охранник по очереди коротко смотрит.
    private readonly List<Vector2Int> glanceQueue = new();
    private readonly HashSet<Vector2Int> reportedDisabledBodies = new();
    private float glanceTimer;
    private bool isGlancing;

    // Цвета «фонарика»: спокойный патруль — янтарный, поиск — жёлтый, погоня — красный.
    private static readonly Color PatrolConeColor = new(1f, 0.62f, 0.18f, 0.22f);
    private static readonly Color SuspectConeColor = new(1f, 0.92f, 0.2f, 0.28f);
    private static readonly Color ChaseConeColor = new(0.95f, 0.27f, 0.2f, 0.32f);

    public GuardState State => state;
    public Vector2Int GridPosition => gridPosition;
    public Vector2Int Facing => facing;
    public int VisionRange => visionRange;

    /// <summary>Текущий уровень тревоги 0..1 (для индикатора над охранником).</summary>
    public float AlertLevel => awareness.Level;

    // IVisionSource: конус прячется, когда охранник оглушён.
    public GameGrid Grid => grid;
    public bool VisionActive => state != GuardState.Disabled && grid != null;

    // Тонировать ли спрайт по состояниям (true для белого квадрата-заглушки).
    private bool tintStates = true;

    public void Initialize(GameGrid gameGrid, PatrolWaypoint[] patrolRoute, Sprite sprite, bool tintSprite = true)
    {
        tintStates = tintSprite;
        grid = gameGrid;
        route = patrolRoute;
        gridPosition = route[0].Cell;
        nextGridPosition = gridPosition;
        targetPosition = grid.GridToWorld(gridPosition.x, gridPosition.y);
        transform.position = targetPosition;
        // Нормализуем по размеру спрайта — чтобы охрана была правильного размера
        // при любом разрешении арта (как у игрока), а не только при 64px.
        float spriteUnit = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.GuardScale
            / Mathf.Max(0.0001f, spriteUnit);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = SpriteWalkAnimator.FeetAnchored(sprite);
        spriteRenderer.color = tintStates ? PatrolColor() : Color.white;
        if (!tintStates) walkAnimator = SpriteWalkAnimator.TryAttach(gameObject, "guard");
        CharacterGroundShadow.Attach(gameObject);
        if (walkAnimator != null) walkAnimator.SetFacing(facing);
        UpdateSortingOrder();
        FaceToward(route[Mathf.Min(1, route.Length - 1)].Cell);

        // «Фонарик»: видимая в мире зона обзора (заменяет конус из миникарты).
        visionCone = VisionConeRenderer.Attach(this, gameObject, PatrolConeColor);
    }

    private void Update()
    {
        if (state == GuardState.Disabled) return;

        UpdateMovement();
        if (isMoving) return;

        switch (state)
        {
            case GuardState.Patrol:
                UpdateAwareness();
                if (state == GuardState.Patrol) UpdatePatrol();
                break;
            case GuardState.Suspicious:
                UpdateAwareness();
                if (state == GuardState.Suspicious) UpdateSuspicious();
                break;
            case GuardState.Investigate:
                UpdateAwareness();
                if (state == GuardState.Investigate) UpdateInvestigate();
                break;
            case GuardState.Search:
                UpdateAwareness();
                if (state == GuardState.Search) UpdateSearch();
                break;
            case GuardState.Chase:
                UpdateChase();
                break;
        }
    }

    private void UpdatePatrol()
    {
        if (route == null || route.Length < 2) return;

        // Дошёл до точки — пока коротко осматривается, стоит на месте.
        if (isGlancing)
        {
            TickGlance();
            return;
        }

        PatrolWaypoint destination = route[destinationIndex];
        if (gridPosition == destination.Cell)
        {
            int arrived = destinationIndex;
            // Цикл по всем точкам маршрута (не пинг-понг двух концов).
            destinationIndex = (destinationIndex + 1) % route.Length;

            // На ключевой точке — пара коротких взглядов (в т.ч. в сторону ящиков),
            // затем сразу дальше; без долгого кручения на месте.
            if (route[arrived].Scan)
            {
                BeginGlance(Random.Range(1, 3));
                return;
            }

            FaceToward(route[destinationIndex].Cell);
            return;
        }

        BeginStep(FindNextStep(destination.Cell));
    }

    private void UpdateSuspicious()
    {
        // Стоит на месте и всматривается в сторону последнего контакта.
        FaceToward(lastKnownPlayerCell);

        // Тревога спала — игрок, похоже, ускользнул: идём проверить точку.
        if (awareness.Level < suspicionThreshold * 0.5f)
        {
            EnterState(GuardState.Investigate);
        }
    }

    private void UpdateInvestigate()
    {
        if (gridPosition == lastKnownPlayerCell)
        {
            EnterState(GuardState.Search);
            return;
        }

        Vector2Int step = FindNextStep(lastKnownPlayerCell);
        if (step == Vector2Int.zero)
        {
            // Путь недоступен — осматриваемся отсюда.
            EnterState(GuardState.Search);
            return;
        }
        BeginStep(step);
    }

    private void UpdateSearch()
    {
        // Осмотрелся по сторонам на месте поиска — и вернулся на маршрут.
        if (isGlancing)
        {
            TickGlance();
            return;
        }

        ReturnToPatrol();
    }

    private void UpdateChase()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        if (player.IsDisguisedAsGuard)
        {
            ReturnToPatrol();
            return;
        }

        // Видит ли охранник игрока прямо сейчас. Прятки в коробке/укрытии (exposure == 0)
        // или разрыв линии обзора делают игрока невидимым даже в активной погоне.
        bool sees = player.StealthExposure > 0.001f && CanSeeCell(player.GridPosition);
        if (sees)
        {
            lastKnownPlayerCell = player.GridPosition;
            chaseLostTimer = chaseLoseSightTime;
        }
        else
        {
            chaseLostTimer -= Time.deltaTime;
            if (chaseLostTimer <= 0f)
            {
                // Игрок пропал — идём проверять последнюю замеченную точку, а не его реальную клетку.
                EnterState(GuardState.Investigate);
                return;
            }
        }

        // Пока не видим игрока — гонимся к последней замеченной клетке, а не к настоящей.
        Vector2Int target = sees ? player.GridPosition : lastKnownPlayerCell;
        Vector2Int delta = target - gridPosition;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 1)
        {
            FaceToward(target);

            // Схватить/ударить можно только пока охранник реально видит игрока.
            // Спрятался рядом — стоит впритык, но не находит и вскоре уйдёт искать.
            if (sees && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                if (RunState.DayPhase == DayPhase.EscortedToExperiment &&
                    grid != null &&
                    grid.IsRestrictedCell(player.GridPosition))
                {
                    player.KillAndResetRun("Надзиратели нашли вас в закрытой зоне. Расстрел на месте.");
                    return;
                }

                if (RunState.DayPhase == DayPhase.EscortedToCell)
                {
                    if (grid != null && grid.IsRestrictedCell(player.GridPosition))
                    {
                        player.KillAndResetRun("Надзиратели нашли вас в закрытой зоне после отбоя. Расстрел на месте.");
                        return;
                    }

                    player.TeleportToCell(GameGrid.PlayerStartCell);
                    RunState.ArriveAtCellForLightsOut();
                    ReturnToPatrol();
                    DialogueUI.Instance.Show("Надзиратель довёл вас до камеры. Ложитесь в кровать.", 2.2f);
                    return;
                }

                player.TakeDamage(attackDamage);
            }
            return;
        }

        BeginStep(FindNextStep(target));
    }

    private void BeginStep(Vector2Int step)
    {
        if (step == Vector2Int.zero) return;

        facing = step;
        Vector2Int next = gridPosition + step;
        if (!CanTraverse(next.x, next.y)) return;

        // В погоне охрана выламывает закрытые двери, чтобы не упустить игрока.
        if (grid.GetTileType(next.x, next.y) == TileType.Door)
        {
            PrisonDoor door = grid.DoorAt(next);
            if (door != null) door.ForceOpen();
            else grid.SetDoorOpen(next.x, next.y, true);
        }

        nextGridPosition = next;
        targetPosition = grid.GridToWorld(next.x, next.y);
        isMoving = true;
    }

    // Проходимость для охраны: пол всегда; закрытые двери — только в погоне (откроет на ходу).
    private bool CanTraverse(int x, int y)
    {
        if (grid.IsWalkable(x, y)) return true;
        return state == GuardState.Chase && grid.GetTileType(x, y) == TileType.Door;
    }

    private Vector2Int FindNextStep(Vector2Int destination)
    {
        var queue = new Queue<Vector2Int>();
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(gridPosition);
        previous[gridPosition] = gridPosition;

        Vector2Int[] directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == destination) break;

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (previous.ContainsKey(next) || !CanTraverse(next.x, next.y)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(destination)) return Vector2Int.zero;

        Vector2Int stepCell = destination;
        while (previous[stepCell] != gridPosition)
        {
            stepCell = previous[stepCell];
        }

        return stepCell - gridPosition;
    }

    private void UpdateMovement()
    {
        if (!isMoving) return;

        float speed = state switch
        {
            GuardState.Chase => chaseSpeed,
            GuardState.Investigate => investigateSpeed,
            _ => patrolSpeed
        };
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        UpdateSortingOrder();
        UpdateAwareness();

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            gridPosition = nextGridPosition;
            isMoving = false;
        }
    }

    /// <summary>
    /// Лестница обнаружения: копит тревогу, пока игрок в конусе, и спадает, когда нет.
    /// По порогам ведёт охранника Patrol → Suspicious → Chase. В Chase не вмешивается.
    /// </summary>
    private void UpdateAwareness()
    {
        if (state == GuardState.Chase || state == GuardState.Disabled) return;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        if (TrySpotDisabledGuard(out Vector2Int bodyCell))
        {
            reportedDisabledBodies.Add(bodyCell);
            lastKnownPlayerCell = bodyCell;
            if (grid != null) grid.ReportRestrictedIncident(bodyCell, "body-found");
            DialogueUI.Instance.Show("Надзиратель обнаружил выведенного из строя охранника.", 1.8f);
            EnterState(GuardState.Search);
            return;
        }

        if (player.IsDisguisedAsGuard)
        {
            awareness.Tick(false, 1f, Time.deltaTime, detectGainNear, detectGainFar, alertDecay);
            return;
        }

        // Заметность игрока (приседание/движение/укрытие/прятки) масштабирует детект.
        float exposure = player.StealthExposure;
        bool visible = exposure > 0.001f && CanSeeCell(player.GridPosition);
        float normalizedDistance = 1f;
        if (visible)
        {
            lastKnownPlayerCell = player.GridPosition;
            Vector2Int delta = player.GridPosition - gridPosition;
            int forwardDistance = delta.x * facing.x + delta.y * facing.y;
            normalizedDistance = Mathf.Clamp01((float)forwardDistance / Mathf.Max(1, visionRange));
        }

        awareness.Tick(visible, normalizedDistance, Time.deltaTime,
            detectGainNear * exposure, detectGainFar * exposure, alertDecay);

        if (awareness.Level >= 1f)
        {
            EnterState(GuardState.Chase);
            return;
        }

        if (state == GuardState.Patrol && awareness.Level >= suspicionThreshold)
        {
            EnterState(GuardState.Suspicious);
        }
    }

    private bool TrySpotDisabledGuard(out Vector2Int bodyCell)
    {
        bodyCell = default;
        if (grid == null) return false;

        foreach (GuardPatrol guard in FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None))
        {
            if (guard == null || guard == this || guard.State != GuardState.Disabled) continue;
            if (reportedDisabledBodies.Contains(guard.GridPosition)) continue;
            if (!CanSeeCell(guard.GridPosition)) continue;

            bodyCell = guard.GridPosition;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Набрать очередь из нескольких сторон для осмотра. Приоритет — стороны, где рядом
    /// есть ящик/укрытие («заглянуть за»), остальное добираем случайно.
    /// </summary>
    private void BeginGlance(int count)
    {
        glanceQueue.Clear();

        var toward = new List<Vector2Int>();
        var rest = new List<Vector2Int>();
        foreach (Vector2Int dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            (HasCoverToward(dir) ? toward : rest).Add(dir);
        }
        Shuffle(toward);
        Shuffle(rest);

        foreach (Vector2Int dir in toward) if (glanceQueue.Count < count) glanceQueue.Add(dir);
        foreach (Vector2Int dir in rest) if (glanceQueue.Count < count) glanceQueue.Add(dir);

        isGlancing = glanceQueue.Count > 0;
        glanceTimer = 0f; // первый взгляд — сразу
    }

    /// <summary>Отсчитывает удержание взгляда и переводит его на следующую сторону.</summary>
    private void TickGlance()
    {
        glanceTimer -= Time.deltaTime;
        if (glanceTimer > 0f) return;

        if (glanceQueue.Count == 0)
        {
            isGlancing = false;
            return;
        }

        Vector2Int dir = glanceQueue[0];
        glanceQueue.RemoveAt(0);
        facing = dir;
        if (walkAnimator != null) walkAnimator.SetFacing(dir);
        glanceTimer = Random.Range(glanceHoldMin, glanceHoldMax);
    }

    /// <summary>Есть ли в нескольких клетках по направлению ящик/укрытие (куда стоит заглянуть).</summary>
    private bool HasCoverToward(Vector2Int dir)
    {
        for (int i = 1; i <= 3; i++)
        {
            Vector2Int c = gridPosition + dir * i;
            if (grid.GetTileType(c.x, c.y) == TileType.Cover || grid.IsHideSpot(c)) return true;
            if (grid.BlocksVision(c.x, c.y)) break; // упёрлись в стену — дальше не смотрим
        }
        return false;
    }

    private static void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void CancelGlance()
    {
        isGlancing = false;
        glanceQueue.Clear();
    }

    private void EnterState(GuardState next)
    {
        if (state == next) return;
        state = next;

        switch (next)
        {
            case GuardState.Suspicious:
                CancelGlance();
                DialogueUI.Instance.Show("Надзиратель что-то заметил…", 1.2f);
                break;
            case GuardState.Investigate:
                CancelGlance();
                break;
            case GuardState.Search:
                // На точке поиска — пара-тройка взглядов по сторонам, затем назад на маршрут.
                BeginGlance(Random.Range(2, 4));
                break;
            case GuardState.Chase:
                CancelGlance();
                awareness.SetMax();
                chaseLostTimer = chaseLoseSightTime;
                nextAttackTime = 0f;
                if (grid != null && grid.IsRestrictedCell(lastKnownPlayerCell))
                    grid.ReportRestrictedIncident(lastKnownPlayerCell, "guard-spotted");
                DialogueUI.Instance.Show("Надзиратель заметил вас!", 1.4f);
                break;
        }

        ApplyStateVisuals();
    }

    private void ReturnToPatrol()
    {
        state = GuardState.Patrol;
        awareness.Reset();
        CancelGlance();
        // Возврат к ближайшей точке маршрута.
        destinationIndex = NearestRouteIndex();
        ApplyStateVisuals();
    }

    private int NearestRouteIndex()
    {
        if (route == null || route.Length == 0) return 0;
        int best = 0;
        int bestDist = int.MaxValue;
        for (int i = 0; i < route.Length; i++)
        {
            Vector2Int d = route[i].Cell - gridPosition;
            int dist = Mathf.Abs(d.x) + Mathf.Abs(d.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
    }

    private void ApplyStateVisuals()
    {
        if (visionCone != null)
        {
            visionCone.SetColor(state switch
            {
                GuardState.Chase => ChaseConeColor,
                GuardState.Suspicious or GuardState.Investigate or GuardState.Search => SuspectConeColor,
                _ => PatrolConeColor
            });
        }

        if (tintStates && spriteRenderer != null)
        {
            spriteRenderer.color = state switch
            {
                GuardState.Chase => new Color(1f, 0.08f, 0.05f),
                GuardState.Suspicious or GuardState.Investigate or GuardState.Search => new Color(0.95f, 0.78f, 0.12f),
                GuardState.Disabled => new Color(0.25f, 0.25f, 0.28f),
                _ => PatrolColor()
            };
        }
    }

    public bool CanSeeCell(Vector2Int target)
    {
        if (state == GuardState.Disabled) return false;
        return VisionMath.CanSeeCell(grid, gridPosition, facing, visionRange, target);
    }

    public bool CanBeSilentlyTakedownBy(Player attacker)
    {
        // Устранять можно во всех состояниях, кроме активной погони и уже оглушённого.
        if (state == GuardState.Chase || state == GuardState.Disabled) return false;
        Vector2 toAttacker = attacker.transform.position - transform.position;
        if (toAttacker.magnitude > grid.CellSize * 1.35f) return false;
        return Vector2.Dot(toAttacker.normalized, facing) < -0.75f;
    }

    public void SilentTakedown()
    {
        if (grid != null) grid.ReportRestrictedIncident(gridPosition, "guard-disabled");
        state = GuardState.Disabled;
        isMoving = false;
        if (tintStates) spriteRenderer.color = new Color(0.25f, 0.25f, 0.28f);
        transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        DialogueUI.Instance.Show("Надзиратель тихо устранён.", 1.4f);
    }

    /// <summary>
    /// Услышать шум из клетки (отвлечение игрока): идём проверить точку.
    /// Не реагируем, если оглушены или уже активно гонимся. Возвращает true, если отвлеклись.
    /// </summary>
    public bool HearNoise(Vector2Int cell)
    {
        if (state == GuardState.Disabled || state == GuardState.Chase) return false;

        lastKnownPlayerCell = cell;
        EnterState(GuardState.Investigate);
        return true;
    }

    /// <summary>
    /// Перевести охранника в розыск (нарушение расписания или вызов камерой).
    /// <paramref name="alertCell"/> — клетка, куда направить охрану (где засекли игрока);
    /// без неё охранник идёт к последней известной точке, как раньше.
    /// </summary>
    public void StartScheduleSearch(Vector2Int? alertCell = null)
    {
        if (state == GuardState.Disabled || grid == null) return;

        player = FindFirstObjectByType<Player>();
        if (alertCell.HasValue) lastKnownPlayerCell = alertCell.Value;
        EnterState(GuardState.Chase);
    }

    public void RespawnAtRouteStart()
    {
        if (grid == null || route == null || route.Length == 0) return;

        state = GuardState.Patrol;
        awareness.Reset();
        CancelGlance();
        reportedDisabledBodies.Clear();
        isMoving = false;
        chaseLostTimer = 0f;
        nextAttackTime = 0f;

        destinationIndex = route.Length > 1 ? 1 : 0;
        gridPosition = route[0].Cell;
        nextGridPosition = gridPosition;
        targetPosition = grid.GridToWorld(gridPosition.x, gridPosition.y);
        transform.position = targetPosition;
        transform.rotation = Quaternion.identity;

        if (spriteRenderer != null && tintStates) spriteRenderer.color = PatrolColor();
        if (walkAnimator != null)
        {
            walkAnimator.enabled = true;
            if (route.Length > 1) walkAnimator.SetFacing(route[1].Cell - route[0].Cell);
        }

        if (route.Length > 1) FaceToward(route[1].Cell);
        ApplyStateVisuals();
        UpdateSortingOrder();
    }

    private void FaceToward(Vector2Int destination)
    {
        Vector2Int delta = destination - gridPosition;
        if (delta == Vector2Int.zero) return;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            facing = new Vector2Int((int)Mathf.Sign(delta.x), 0);
        }
        else
        {
            facing = new Vector2Int(0, (int)Mathf.Sign(delta.y));
        }
        // Поворот на месте: сразу обновляем визуальный ракурс под новый facing,
        // не дожидаясь движения (иначе на концах патруля спрайт смотрит не туда).
        if (walkAnimator != null) walkAnimator.SetFacing(facing);
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        }
    }

    private static Color PatrolColor() => new Color(0.75f, 0.12f, 0.12f);

    private void OnGUI()
    {
        if (QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen) return;
        if (grid == null) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Индикатор тревоги над охранником (MGS-стиль): шкала заполнения + «?»/«!».
        DrawAlertMeter(mainCamera);
        DrawAlertGlyph(mainCamera);

        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null || !CanBeSilentlyTakedownBy(player)) return;

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(transform.position);
        if (screenPosition.z < 0f) return;

        const float width = 190f;
        const float height = 26f;
        Rect promptRect = new Rect(
            screenPosition.x - width * 0.5f,
            Screen.height - screenPosition.y - 48f,
            width,
            height
        );

        GUI.Box(promptRect, "Скрытно устранить — F");
    }

    private void DrawAlertGlyph(Camera mainCamera)
    {
        string glyph = state switch
        {
            GuardState.Chase => "!",
            GuardState.Suspicious or GuardState.Investigate or GuardState.Search => "?",
            _ => null
        };
        if (glyph == null) return;

        AlertIndicator.DrawGlyph(mainCamera,
            transform.position + Vector3.up * grid.CellSize * 1.35f, glyph);
    }

    /// <summary>Полоска тревоги над охранником: насколько сильно он «палит» игрока.</summary>
    private void DrawAlertMeter(Camera mainCamera)
    {
        float level = state == GuardState.Chase ? 1f : awareness.Level;
        bool elevated = state != GuardState.Patrol && state != GuardState.Disabled;
        if (level <= 0.02f && !elevated) return;

        AlertIndicator.DrawMeter(mainCamera,
            transform.position + Vector3.up * grid.CellSize, level, state == GuardState.Chase);
    }
}
