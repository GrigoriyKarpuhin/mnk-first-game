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
    private static readonly Color ObjectiveMarker = new(1f, 0.76f, 0.22f, 1f);

    private static PrisonMapUI instance;

    private GameGrid grid;
    private Player player;
    private float zoom = 1f;
    private float panX;
    private float panY;
    private float previousTimeScale = 1f;
    private GUIStyle titleStyle;
    private GUIStyle smallStyle;
    private GUIStyle legendStyle;
    private GUIStyle roomLabelStyle;
    private GUIStyle objectiveLabelStyle;

    private readonly struct RoomLabel
    {
        public readonly string Name;
        public readonly GridArea Area;

        public RoomLabel(string name, GridArea area)
        {
            Name = name;
            Area = area;
        }

        public Vector2Int Center => new((Area.MinX + Area.MaxX) / 2, (Area.MinY + Area.MaxY) / 2);
    }

    private static readonly RoomLabel[] RoomLabels =
    {
        new("Камера", new GridArea(15, 3, 19, 7)),
        new("Общая зона", new GridArea(14, 9, 49, 52)),
        new("Столовая", new GridArea(3, 16, 12, 25)),
        new("Сад", new GridArea(1, 34, 12, 46)),
        new("Санитарное крыло", new GridArea(51, 18, 86, 38)),
        new("Туалеты", new GridArea(58, 32, 65, 38)),
        new("Кухня", new GridArea(66, 41, 78, 51)),
        new("Комната персонала", new GridArea(92, 49, 99, 55)),
        new("Склад", new GridArea(105, 42, 112, 49)),
        new("Лаборатория", new GridArea(103, 61, 111, 69)),
        new("Инженерная", new GridArea(116, 61, 124, 69)),
        new("Тех. крыло", new GridArea(130, 50, 140, 60)),
        new("Архив", new GridArea(142, 50, 152, 60)),
        new("Релейная", new GridArea(142, 34, 152, 44)),
        new("2 этаж", new GridArea(14, 84, 49, 127)),
    };

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
            return;
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame) SetZoom(zoom + 0.25f);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame) SetZoom(zoom - 0.25f);

        float panStep = 54f;
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) panX += panStep;
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) panX -= panStep;

        ClampPan();
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
            GUI.Label(new Rect(margin, 58f, Screen.width - margin * 2f, 26f),
                "M / Esc — закрыть · ↑/↓ масштаб · ←/→ сдвиг · голубой — герой · жёлтый — активная цель",
                smallStyle);

            Rect mapRect = CalculateMapRect(margin, 92f, Screen.width - margin * 2f, Screen.height - 126f);
            DrawPanel(mapRect);
            DrawMap(mapRect);
            DrawRoomLabels(mapRect);
            DrawObjective(mapRect);
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
        return new Rect(left, top, availableWidth, maxHeight);
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
        GUI.BeginGroup(rect);
        Rect localRect = new(0f, 0f, rect.width, rect.height);
        float tileSize = TileSize(localRect);
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                GUI.color = ColorForCell(x, y);
                GUI.DrawTexture(CellRect(localRect, x, y, tileSize), Texture2D.whiteTexture);
            }
        }
        GUI.color = Color.white;
        GUI.EndGroup();
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
        GUI.BeginGroup(rect);
        Rect localRect = new(0f, 0f, rect.width, rect.height);
        float tileSize = TileSize(localRect);
        Vector2Int position = player.GridPosition;
        Rect cell = CellRect(localRect, position.x, position.y, tileSize);
        float size = Mathf.Max(tileSize * 2.5f, 8f);
        Rect marker = new(cell.center.x - size * 0.5f, cell.center.y - size * 0.5f, size, size);

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(marker.x - 2f, marker.y - 2f, marker.width + 4f, marker.height + 4f), Texture2D.whiteTexture);
        GUI.color = PlayerMarker;
        GUI.DrawTexture(marker, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.EndGroup();
    }

    private void DrawObjective(Rect rect)
    {
        if (!RunState.TryGetActiveQuestTarget(out Vector2Int target, out string label)) return;

        GUI.BeginGroup(rect);
        Rect localRect = new(0f, 0f, rect.width, rect.height);
        float tileSize = TileSize(localRect);
        Rect cell = CellRect(localRect, target.x, target.y, tileSize);
        float size = Mathf.Max(tileSize * 3.4f, 12f);
        Rect marker = new(cell.center.x - size * 0.5f, cell.center.y - size * 0.5f, size, size);

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(marker.x - 2f, marker.y - 2f, marker.width + 4f, marker.height + 4f), Texture2D.whiteTexture);
        GUI.color = ObjectiveMarker;
        GUI.DrawTexture(marker, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(marker.xMax + 6f, marker.y - 2f, 180f, 28f), label, objectiveLabelStyle);
        GUI.EndGroup();
    }

    private void DrawRoomLabels(Rect rect)
    {
        float tileSize = TileSize(new Rect(0f, 0f, rect.width, rect.height));
        if (tileSize < 4.2f) return;

        GUI.BeginGroup(rect);
        Rect localRect = new(0f, 0f, rect.width, rect.height);
        foreach (RoomLabel label in RoomLabels)
        {
            Vector2Int center = label.Center;
            Rect cell = CellRect(localRect, center.x, center.y, tileSize);
            float width = Mathf.Clamp(label.Name.Length * 8f + 18f, 72f, 180f);
            Rect labelRect = new(cell.center.x - width * 0.5f, cell.center.y - 10f, width, 22f);
            GUI.Label(labelRect, label.Name, roomLabelStyle);
        }
        GUI.EndGroup();
    }

    private Rect CellRect(Rect mapRect, int x, int y, float tileSize)
    {
        float mapWidth = grid.Width * tileSize;
        float mapHeight = grid.Height * tileSize;
        float originX = mapRect.x + (mapRect.width - mapWidth) * 0.5f + panX;
        float originY = mapRect.y + (mapRect.height - mapHeight) * 0.5f + panY;
        float px = originX + x * tileSize;
        float py = originY + (grid.Height - y - 1) * tileSize;
        return new Rect(px, py, tileSize, tileSize);
    }

    private float TileSize(Rect rect)
    {
        return Mathf.Max(2f, Mathf.Floor(Mathf.Min(rect.width / grid.Width, rect.height / grid.Height) * zoom));
    }

    private void SetZoom(float nextZoom)
    {
        float oldZoom = zoom;
        zoom = Mathf.Clamp(nextZoom, 0.75f, 3f);
        if (!Mathf.Approximately(oldZoom, zoom))
        {
            panX *= zoom / oldZoom;
            panY *= zoom / oldZoom;
        }
        ClampPan();
    }

    private void ClampPan()
    {
        if (grid == null) return;
        Rect viewport = CalculateMapRect(34f, 92f, Screen.width - 68f, Screen.height - 126f);
        float tileSize = TileSize(new Rect(0f, 0f, viewport.width, viewport.height));
        float excessX = Mathf.Max(0f, grid.Width * tileSize - viewport.width);
        float excessY = Mathf.Max(0f, grid.Height * tileSize - viewport.height);
        panX = Mathf.Clamp(panX, -excessX * 0.5f, excessX * 0.5f);
        panY = Mathf.Clamp(panY, -excessY * 0.5f, excessY * 0.5f);
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
        DrawLegendRow(rect.x, rect.y + 164f, ObjectiveMarker, "Активная цель");
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
        roomLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.82f, 0.88f, 0.86f, 0.95f) },
        };
        objectiveLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = ObjectiveMarker },
        };
    }
}
