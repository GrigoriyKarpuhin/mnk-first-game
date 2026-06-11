using System.Collections.Generic;
using UnityEngine;

public enum GuardState
{
    Patrol,
    Chase,
    Disabled
}

public class GuardPatrol : MonoBehaviour
{
    [SerializeField] private float patrolSpeed = 3.2f;
    [SerializeField] private float chaseSpeed = 5.2f;
    [SerializeField] private float endpointPause = 5f;
    [SerializeField] private int visionRange = 7;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackCooldown = 0.9f;

    private GameGrid grid;
    private Vector2Int[] route;
    private int destinationIndex = 1;
    private Vector2Int gridPosition;
    private Vector2Int nextGridPosition;
    private Vector2Int facing = Vector2Int.right;
    private Vector3 targetPosition;
    private bool isMoving;
    private float pauseTimer;
    private float nextAttackTime;
    private SpriteRenderer spriteRenderer;
    private Player player;
    private GuardState state = GuardState.Patrol;

    public GuardState State => state;
    public Vector2Int GridPosition => gridPosition;
    public Vector2Int Facing => facing;
    public int VisionRange => visionRange;

    // Тонировать ли спрайт по состояниям (true для белого квадрата-заглушки).
    private bool tintStates = true;

    public void Initialize(GameGrid gameGrid, Vector2Int[] patrolRoute, Sprite sprite, bool tintSprite = true)
    {
        tintStates = tintSprite;
        grid = gameGrid;
        route = patrolRoute;
        gridPosition = route[0];
        nextGridPosition = gridPosition;
        targetPosition = grid.GridToWorld(gridPosition.x, gridPosition.y);
        transform.position = targetPosition;
        transform.localScale = Vector3.one * grid.CellSize * 0.72f;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = tintStates ? PatrolColor() : Color.white;
        if (!tintStates) SpriteWalkAnimator.TryAttach(gameObject, "guard");
        UpdateSortingOrder();
        FaceToward(route[1]);
        pauseTimer = endpointPause;
    }

    private void Update()
    {
        if (state == GuardState.Disabled) return;

        UpdateMovement();
        if (isMoving) return;

        if (state == GuardState.Chase)
        {
            UpdateChase();
            return;
        }

        CheckVision();
        UpdatePatrol();
    }

    private void UpdatePatrol()
    {
        if (route == null || route.Length < 2) return;

        pauseTimer -= Time.deltaTime;
        if (pauseTimer > 0f) return;

        Vector2Int destination = route[destinationIndex];
        if (gridPosition == destination)
        {
            destinationIndex = destinationIndex == 0 ? 1 : 0;
            destination = route[destinationIndex];
            FaceToward(destination);
            pauseTimer = endpointPause;
            return;
        }

        BeginStep(FindNextStep(destination));
    }

    private void UpdateChase()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        Vector2Int delta = player.GridPosition - gridPosition;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 1)
        {
            FaceToward(player.GridPosition);
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                player.TakeDamage(attackDamage);
            }
            return;
        }

        BeginStep(FindNextStep(player.GridPosition));
    }

    private void BeginStep(Vector2Int step)
    {
        if (step == Vector2Int.zero) return;

        facing = step;
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

        float speed = state == GuardState.Chase ? chaseSpeed : patrolSpeed;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        UpdateSortingOrder();
        CheckVision();

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            gridPosition = nextGridPosition;
            isMoving = false;
        }
    }

    private void CheckVision()
    {
        if (state != GuardState.Patrol) return;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        if (!CanSeeCell(player.GridPosition)) return;

        state = GuardState.Chase;
        spriteRenderer.color = tintStates ? new Color(1f, 0.08f, 0.05f) : new Color(1f, 0.5f, 0.45f);
        DialogueUI.Instance.Show("Надзиратель заметил вас!", 1.4f);
    }

    public bool CanSeeCell(Vector2Int target)
    {
        if (state == GuardState.Disabled || grid == null) return false;

        Vector2Int delta = target - gridPosition;
        int forwardDistance = delta.x * facing.x + delta.y * facing.y;
        if (forwardDistance <= 0 || forwardDistance > visionRange) return false;

        Vector2Int side = new Vector2Int(-facing.y, facing.x);
        int sideDistance = Mathf.Abs(delta.x * side.x + delta.y * side.y);
        if (sideDistance > forwardDistance) return false;

        return HasClearLineOfSight(target);
    }

    private bool HasClearLineOfSight(Vector2Int target)
    {
        int x = gridPosition.x;
        int y = gridPosition.y;
        int dx = Mathf.Abs(target.x - x);
        int dy = Mathf.Abs(target.y - y);
        int stepX = x < target.x ? 1 : -1;
        int stepY = y < target.y ? 1 : -1;
        int error = dx - dy;

        while (x != target.x || y != target.y)
        {
            int doubledError = error * 2;
            if (doubledError > -dy)
            {
                error -= dy;
                x += stepX;
            }

            if (doubledError < dx)
            {
                error += dx;
                y += stepY;
            }

            if ((x != target.x || y != target.y) && grid.BlocksVision(x, y)) return false;
        }

        return true;
    }

    public bool CanBeSilentlyTakedownBy(Player attacker)
    {
        if (state != GuardState.Patrol) return false;
        Vector2 toAttacker = attacker.transform.position - transform.position;
        if (toAttacker.magnitude > grid.CellSize * 1.35f) return false;
        return Vector2.Dot(toAttacker.normalized, facing) < -0.75f;
    }

    public void SilentTakedown()
    {
        state = GuardState.Disabled;
        isMoving = false;
        spriteRenderer.color = tintStates ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.55f, 0.55f, 0.6f);
        transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        DialogueUI.Instance.Show("Надзиратель тихо устранён.", 1.4f);
    }

    private void FaceToward(Vector2Int destination)
    {
        Vector2Int delta = destination - gridPosition;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            facing = new Vector2Int((int)Mathf.Sign(delta.x), 0);
        }
        else
        {
            facing = new Vector2Int(0, (int)Mathf.Sign(delta.y));
        }
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
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null || !CanBeSilentlyTakedownBy(player)) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

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
}
