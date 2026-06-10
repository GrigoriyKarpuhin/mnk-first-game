using UnityEngine;
using System.Collections.Generic;

public class GuardPatrol : MonoBehaviour
{
    [SerializeField] private float stepInterval = 0.45f;
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private int visionRange = 7;

    private GameGrid grid;
    private Vector2Int[] route;
    private int routeIndex;
    private Vector2Int gridPosition;
    private Vector2Int facing = Vector2Int.right;
    private Vector3 targetPosition;
    private bool isMoving;
    private float stepTimer;
    private float nextAlertTime;
    private SpriteRenderer spriteRenderer;
    private Player player;

    public void Initialize(GameGrid gameGrid, Vector2Int[] patrolRoute, Sprite sprite)
    {
        grid = gameGrid;
        route = patrolRoute;
        gridPosition = route[0];
        targetPosition = grid.GridToWorld(gridPosition.x, gridPosition.y);
        transform.position = targetPosition;
        transform.localScale = Vector3.one * grid.CellSize * 0.72f;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = new Color(0.75f, 0.12f, 0.12f);
        UpdateSortingOrder();

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform);
        labelObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var label = labelObject.AddComponent<TextMesh>();
        label.text = "ОХРАНА";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.characterSize = 0.045f;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.35f, 0.25f);
        label.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Foreground(transform.position.y);
    }

    private void Update()
    {
        UpdateMovement();
        UpdatePatrol();
        CheckVision();
    }

    private void UpdatePatrol()
    {
        if (isMoving || route == null || route.Length < 2) return;

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;
        stepTimer = stepInterval;

        Vector2Int waypoint = route[routeIndex];
        if (gridPosition == waypoint)
        {
            routeIndex = (routeIndex + 1) % route.Length;
            waypoint = route[routeIndex];
        }

        Vector2Int step = FindNextStep(waypoint);

        if (step == Vector2Int.zero) return;
        facing = step;

        Vector2Int next = gridPosition + step;
        if (!grid.IsWalkable(next.x, next.y))
        {
            routeIndex = (routeIndex + 1) % route.Length;
            return;
        }

        gridPosition = next;
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

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        UpdateSortingOrder();
        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    private void CheckVision()
    {
        if (Time.time < nextAlertTime) return;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) return;

        Vector2Int playerPosition = player.GridPosition;
        Vector2Int delta = playerPosition - gridPosition;
        bool inForwardLine = facing.x != 0
            ? delta.y == 0 && Mathf.Sign(delta.x) == facing.x && Mathf.Abs(delta.x) <= visionRange
            : delta.x == 0 && Mathf.Sign(delta.y) == facing.y && Mathf.Abs(delta.y) <= visionRange;

        if (!inForwardLine) return;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        for (int i = 1; i < distance; i++)
        {
            Vector2Int checkedCell = gridPosition + facing * i;
            if (grid.BlocksVision(checkedCell.x, checkedCell.y)) return;
        }

        nextAlertTime = Time.time + 2f;
        DialogueUI.Instance.Show("Надзиратель заметил вас. Подозрение зоны повышено.", 2f);
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        }
    }
}
