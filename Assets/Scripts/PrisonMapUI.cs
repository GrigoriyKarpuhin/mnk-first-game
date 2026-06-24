using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrisonMapUI : MonoBehaviour
{
    private static readonly Color Background = new(0.015f, 0.02f, 0.025f, 0.96f);
    private static readonly Color Panel = new(0.045f, 0.06f, 0.07f, 1f);
    private static readonly Color Border = new(0.36f, 0.78f, 0.72f, 1f);
    private static readonly Color Wall = new(0.12f, 0.15f, 0.18f, 1f);
    private static readonly Color Floor = new(0.42f, 0.45f, 0.48f, 1f);
    private static readonly Color RestrictedFloor = new(0.48f, 0.27f, 0.25f, 1f);
    private static readonly Color Door = new(0.88f, 0.68f, 0.24f, 1f);
    private static readonly Color Cover = new(0.36f, 0.24f, 0.14f, 1f);
    private static readonly Color PlayerMarker = new(0.15f, 0.78f, 1f, 1f);

    private static PrisonMapUI instance;

    private GameGrid grid;
    private Player player;
    private float previousTimeScale = 1f;
    private GUIStyle titleStyle;
    private GUIStyle smallStyle;
    private GUIStyle legendStyle;

    public static bool IsOpen => instance != null && instance.enabled;

    public static void Open(GameGrid activeGrid, Player activePlayer)
    {
        if (activeGrid == null || activePlayer == null) return;

        if (instance == null)
        {
            var go = new GameObject("Prison Map UI");
            instance = go.AddComponent<PrisonMapUI>();
            DontDestroyOnLoad(go);
            instance.enabled = false;
        }

        instance.Show(activeGrid, activePlayer);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        enabled = false;
    }

    private void Update()
    {
        if (!IsOpen || Keyboard.current == null) return;
        if (Keyboard.current.mKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void Show(GameGrid activeGrid, Player activePlayer)
    {
        grid = activeGrid;
        player = activePlayer;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        enabled = true;
    }

    private void Close()
    {
        enabled = false;
        Time.timeScale = previousTimeScale;
    }

    private void OnGUI()
    {
        if (grid == null || player == null)
        {
            Close();
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = -1100;
        try
        {
            BuildStyles();

            GUI.color = Background;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float margin = 34f;
            GUI.Label(new Rect(margin, 22f, Screen.width - margin * 2f, 48f), "КАРТА ТЮРЬМЫ", titleStyle);
            GUI.Label(new Rect(margin, 58f, Screen.width - margin * 2f, 26f), "M / Esc — закрыть · голубой маркер — текущее положение героя", smallStyle);

            Rect mapRect = CalculateMapRect(margin, 92f, Screen.width - margin * 2f, Screen.height - 126f);
            DrawPanel(mapRect);
            DrawMap(mapRect);
            DrawPlayer(mapRect);
            DrawLegend(new Rect(mapRect.xMax + 18f, mapRect.y, Mathf.Max(180f, Screen.width - mapRect.xMax - 34f), 190f));
        }
        finally
        {
            GUI.color = Color.white;
            GUI.depth = previousDepth;
        }
    }

    private Rect CalculateMapRect(float left, float top, float maxWidth, float maxHeight)
    {
        float legendReserve = maxWidth >= 900f ? 230f : 0f;
        float availableWidth = maxWidth - legendReserve;
        float tileSize = Mathf.Floor(Mathf.Min(availableWidth / grid.Width, maxHeight / grid.Height));
        tileSize = Mathf.Max(2f, tileSize);

        float width = tileSize * grid.Width;
        float height = tileSize * grid.Height;
        float x = left + Mathf.Max(0f, (availableWidth - width) * 0.5f);
        return new Rect(x, top, width, height);
    }

    private void DrawPanel(Rect rect)
    {
        GUI.color = Panel;
        GUI.DrawTexture(new Rect(rect.x - 8f, rect.y - 8f, rect.width + 16f, rect.height + 16f), Texture2D.whiteTexture);
        GUI.color = Border;
        GUI.DrawTexture(new Rect(rect.x - 8f, rect.y - 8f, rect.width + 16f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x - 8f, rect.yMax + 6f, rect.width + 16f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x - 8f, rect.y - 8f, 2f, rect.height + 16f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax + 6f, rect.y - 8f, 2f, rect.height + 16f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawMap(Rect rect)
    {
        float tileSize = rect.width / grid.Width;
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                GUI.color = ColorForCell(x, y);
                GUI.DrawTexture(CellRect(rect, x, y, tileSize), Texture2D.whiteTexture);
            }
        }
        GUI.color = Color.white;
    }

    private Color ColorForCell(int x, int y)
    {
        TileType type = grid.GetTileType(x, y);
        if (grid.IsRestrictedCell(x, y) && type != TileType.Wall) return RestrictedFloor;

        return type switch
        {
            TileType.Floor => Floor,
            TileType.Door => Door,
            TileType.Cover => Cover,
            _ => Wall,
        };
    }

    private void DrawPlayer(Rect rect)
    {
        float tileSize = rect.width / grid.Width;
        Vector2Int position = player.GridPosition;
        Rect cell = CellRect(rect, position.x, position.y, tileSize);
        float size = Mathf.Max(tileSize * 2.5f, 8f);
        Rect marker = new(cell.center.x - size * 0.5f, cell.center.y - size * 0.5f, size, size);

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(marker.x - 2f, marker.y - 2f, marker.width + 4f, marker.height + 4f), Texture2D.whiteTexture);
        GUI.color = PlayerMarker;
        GUI.DrawTexture(marker, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private Rect CellRect(Rect mapRect, int x, int y, float tileSize)
    {
        float px = mapRect.x + x * tileSize;
        float py = mapRect.y + (grid.Height - y - 1) * tileSize;
        return new Rect(px, py, tileSize, tileSize);
    }

    private void DrawLegend(Rect rect)
    {
        if (rect.width < 170f) return;

        GUI.Label(new Rect(rect.x, rect.y, rect.width, 26f), "Легенда", legendStyle);
        DrawLegendRow(rect.x, rect.y + 34f, Floor, "Открытая зона");
        DrawLegendRow(rect.x, rect.y + 60f, RestrictedFloor, "Закрытая зона");
        DrawLegendRow(rect.x, rect.y + 86f, Door, "Дверь");
        DrawLegendRow(rect.x, rect.y + 112f, Cover, "Укрытие");
        DrawLegendRow(rect.x, rect.y + 138f, PlayerMarker, "Герой");
    }

    private void DrawLegendRow(float x, float y, Color color, string label)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y + 4f, 18f, 18f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 28f, y, 180f, 24f), label, smallStyle);
    }

    private void BuildStyles()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 38,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.95f, 0.95f, 1f) },
        };
        smallStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            normal = { textColor = new Color(0.78f, 0.86f, 0.86f, 1f) },
        };
        legendStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Border },
        };
    }
}
