using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Охранник общей зоны, который не занимается стелсом в обычное время.
/// Его задача — физически догнать игрока после пропуска сбора на эксперимент.
/// </summary>
public sealed class ScheduleEnforcerGuard : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 6.6f;

    private GameGrid grid;
    private Player player;
    private Vector2Int gridPosition;
    private Vector2Int nextGridPosition;
    private Vector2Int facing = Vector2Int.left;
    private Vector3 targetPosition;
    private bool isMoving;
    private bool searching;
    private bool resolved;
    private SpriteRenderer spriteRenderer;
    private SpriteWalkAnimator walkAnimator;

    public void Initialize(GameGrid gameGrid, Vector2Int startCell, Sprite sprite, bool tintSprite = true)
    {
        grid = gameGrid;
        gridPosition = startCell;
        nextGridPosition = startCell;
        targetPosition = grid.GridToWorld(startCell.x, startCell.y);
        transform.position = targetPosition;

        float spriteUnit = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.GuardScale
            / Mathf.Max(0.0001f, spriteUnit);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = SpriteWalkAnimator.FeetAnchored(sprite);
        spriteRenderer.color = tintSprite ? new Color(0.78f, 0.52f, 0.18f) : Color.white;
        if (!tintSprite) walkAnimator = SpriteWalkAnimator.TryAttach(gameObject, "guard");
        CharacterGroundShadow.Attach(gameObject);
        CharacterScreenFacing.Attach(gameObject);
        UpdateSortingOrder();
    }

    public void StartScheduleSearch()
    {
        if (resolved || searching) return;

        searching = true;
        player = FindFirstObjectByType<Player>();
        if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.45f, 0.05f);
    }

    private void Update()
    {
        if (RunState.DayPhase != DayPhase.EscortedToExperiment &&
            RunState.DayPhase != DayPhase.EscortedToCell &&
            (searching || resolved))
        {
            searching = false;
            resolved = false;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.78f, 0.52f, 0.18f);
        }

        if (resolved) return;
        if (RunState.DayPhase == DayPhase.EscortedToExperiment ||
            RunState.DayPhase == DayPhase.EscortedToCell)
        {
            StartScheduleSearch();
        }
        if (!searching) return;

        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        UpdateMovement();
        if (isMoving) return;

        if (IsPlayerCaught())
        {
            ResolveCatch();
            return;
        }

        BeginStep(FindNextStep(player.GridPosition));
    }

    private bool IsPlayerCaught()
    {
        Vector2Int delta = player.GridPosition - gridPosition;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 1;
    }

    private void ResolveCatch()
    {
        resolved = true;
        FaceToward(player.GridPosition);

        if (grid.IsRestrictedCell(player.GridPosition))
        {
            player.KillAndResetRun("Надзиратели нашли вас в закрытой зоне. Расстрел на месте.");
            return;
        }

        if (RunState.DayPhase == DayPhase.EscortedToCell)
        {
            player.TeleportToCell(GameGrid.PlayerStartCell);
            RunState.ArriveAtCellForLightsOut();
            DialogueUI.Instance.Show("Надзиратель довёл вас до камеры. Ложитесь в кровать.", 2.2f);
            return;
        }

        DialogueUI.Instance.Show("Надзиратель догнал вас и ведёт на эксперимент.", 1f);
        RunState.EnterSelectedExperiment();
    }

    private void BeginStep(Vector2Int step)
    {
        if (step == Vector2Int.zero) return;

        facing = step;
        if (walkAnimator != null) walkAnimator.SetFacing(facing);

        Vector2Int next = gridPosition + step;
        if (!grid.IsWalkable(next.x, next.y)) return;

        nextGridPosition = next;
        targetPosition = grid.GridToWorld(next.x, next.y);
        isMoving = true;
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
                if (previous.ContainsKey(next) || !grid.IsWalkable(next.x, next.y)) continue;
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

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, chaseSpeed * Time.deltaTime);
        UpdateSortingOrder();

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            gridPosition = nextGridPosition;
            isMoving = false;
        }
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

        if (walkAnimator != null) walkAnimator.SetFacing(facing);
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        }
    }
}
