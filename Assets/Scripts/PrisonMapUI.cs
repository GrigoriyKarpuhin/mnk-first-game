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
    // Туман войны: неисследованные комнаты и стены вокруг них — почти чёрные.
    private static readonly Color Fog = new(0.02f, 0.025f, 0.03f, 1f);
    // Смежная (ещё не исследованная) комната: тёмная заливка-контур — виден силуэт комнаты.
    private static readonly Color SilhouetteFloor = new(0.06f, 0.07f, 0.085f, 1f);
    // Акцент «зачищено»: холодный сине-зелёный подмешивается в цвет пола.
    private static readonly Color ClearedAccent = new(0.30f, 0.62f, 0.55f, 1f);
    private static readonly Color DoorOpen = new(0.36f, 0.85f, 0.42f, 1f);
    private static readonly Color DoorLocked = new(0.86f, 0.32f, 0.28f, 1f);

    // Adjacent — смежная с посещённой комната: рисуем её контур, но заливка «неисследовано».
    private enum RoomState { Unexplored, Adjacent, Explored, Cleared }

    private static PrisonMapUI instance;

    private GameGrid grid;
    private Player player;
    private System.Collections.Generic.Dictionary<int, RoomState> roomStates;
    private float zoom = 1f;
    private float panX;
    private float panY;
    private float previousTimeScale = 1f;
    private GUIStyle titleStyle;
    private GUIStyle smallStyle;
    private GUIStyle legendStyle;
    private GUIStyle roomLabelStyle;
    private GUIStyle objectiveLabelStyle;
    private GUIStyle doorLabelStyle;

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
        if (!IsOpen) return;

        if (Keyboard.current != null)
        {
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
        }

        // Перетаскивание области видимости зажатой ЛКМ — карта следует за курсором.
        // GUI-ось Y направлена вниз, delta мыши — вверх, поэтому по Y вычитаем.
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Vector2 drag = Mouse.current.delta.ReadValue();
            panX += drag.x;
            panY -= drag.y;
        }

        ClampPan();
    }

    private void Show(GameGrid activeGrid, Player activePlayer)
    {
        grid = activeGrid;
        player = activePlayer;
        roomStates = ComputeRoomStates();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        enabled = true;
    }

    /// <summary>
    /// Состояние каждой комнаты на момент открытия карты (модалка, timeScale=0 — считаем один раз).
    /// Незакрытая задача = живой предмет в комнате ИЛИ незавершённый IRoomObjective.
    /// Правило: не заходил → Unexplored; задач не осталось → Cleared; иначе → Explored.
    /// «Пустая» посещённая комната (задач не было) сразу Cleared — по решению дизайна.
    /// </summary>
    private System.Collections.Generic.Dictionary<int, RoomState> ComputeRoomStates()
    {
        RoomGraph graph = grid.RoomGraph;
        var unresolved = new System.Collections.Generic.Dictionary<int, int>();

        void Bump(Vector2Int cell)
        {
            int id = graph.ComponentAt(cell);
            if (id < 0) return;
            unresolved.TryGetValue(id, out int n);
            unresolved[id] = n + 1;
        }

        // Живые предметы на сцене = ещё не собранный лут (подобранный Item само-уничтожается).
        foreach (Item item in FindObjectsByType<Item>(FindObjectsSortMode.None))
        {
            Bump(grid.WorldToGrid(item.transform.position));
        }

        // Завершаемые интеракции (головоломки, сканер, папка, замок).
        foreach (IRoomObjective objective in grid.RoomObjectives)
        {
            if (objective == null || objective.IsObjectiveResolved) continue;
            Bump(objective.Cell);
        }

        var states = new System.Collections.Generic.Dictionary<int, RoomState>();
        foreach (RoomGraph.Room room in graph.Rooms)
        {
            if (!RunState.IsRoomVisited(room.Id))
            {
                // Смежные (по двери) с посещённой комнатой рисуем как контур, остальное — туман.
                states[room.Id] = HasVisitedNeighborRoom(room) ? RoomState.Adjacent : RoomState.Unexplored;
                continue;
            }

            states[room.Id] = unresolved.TryGetValue(room.Id, out int left) && left > 0
                ? RoomState.Explored
                : RoomState.Cleared;
        }

        return states;
    }

    private static bool HasVisitedNeighborRoom(RoomGraph.Room room)
    {
        foreach (int neighbor in room.Neighbors)
        {
            if (RunState.IsRoomVisited(neighbor)) return true;
        }
        return false;
    }

    private RoomState StateForRoom(int id)
    {
        return roomStates != null && roomStates.TryGetValue(id, out RoomState s) ? s : RoomState.Unexplored;
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
                "M / Esc — закрыть · ↑/↓ масштаб · ЛКМ тащит карту · тёмное — не исследовано · двери показывают доступ",
                smallStyle);

            Rect mapRect = CalculateMapRect(margin, 92f, Screen.width - margin * 2f, Screen.height - 126f);
            DrawPanel(mapRect);
            DrawMap(mapRect);
            DrawDoors(mapRect);
            DrawRoomLabels(mapRect);
            DrawObjective(mapRect);
            DrawPlayer(mapRect);
            DrawLegend(new Rect(mapRect.xMax + 18f, mapRect.y, Mathf.Max(180f, Screen.width - mapRect.xMax - 34f), 346f));
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
        int id = grid.RoomGraph.ComponentAt(new Vector2Int(x, y));
        if (id >= 0)
        {
            // Внутренняя клетка комнаты: красим по состоянию исследования.
            switch (StateForRoom(id))
            {
                case RoomState.Unexplored: return Fog;
                case RoomState.Adjacent:   return SilhouetteFloor;                                  // силуэт смежной комнаты
                case RoomState.Cleared:    return Color.Lerp(BaseColorForCell(x, y), ClearedAccent, 0.35f);
                default:                   return BaseColorForCell(x, y);                            // Explored
            }
        }

        // Стена/дверь. У границы посещённой комнаты — настоящие цвета (дверь жёлтая,
        // поверх неё DrawDoors). У контура смежной комнаты — как стена (двери неисследованного
        // не раскрываем). Глубже — туман.
        if (HasVisitedNeighbor(x, y)) return BaseColorForCell(x, y);
        if (HasRevealedNeighbor(x, y)) return Wall;
        return Fog;
    }

    /// <summary>Базовый цвет клетки по типу тайла (как было до тумана войны).</summary>
    private Color BaseColorForCell(int x, int y)
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

    /// <summary>Есть ли рядом (по 4 сторонам) клетка посещённой комнаты — для показа стен/дверей.</summary>
    private bool HasVisitedNeighbor(int x, int y)
    {
        RoomGraph graph = grid.RoomGraph;
        return IsVisitedRoomAt(graph, x + 1, y) || IsVisitedRoomAt(graph, x - 1, y)
            || IsVisitedRoomAt(graph, x, y + 1) || IsVisitedRoomAt(graph, x, y - 1);
    }

    private static bool IsVisitedRoomAt(RoomGraph graph, int x, int y)
    {
        int id = graph.ComponentAt(new Vector2Int(x, y));
        return id >= 0 && RunState.IsRoomVisited(id);
    }

    /// <summary>Есть ли рядом раскрытая комната (посещённая ИЛИ смежная-силуэт) — рисуем контур.</summary>
    private bool HasRevealedNeighbor(int x, int y)
    {
        return IsRevealedRoomAt(x + 1, y) || IsRevealedRoomAt(x - 1, y)
            || IsRevealedRoomAt(x, y + 1) || IsRevealedRoomAt(x, y - 1);
    }

    private bool IsRevealedRoomAt(int x, int y)
    {
        int id = grid.RoomGraph.ComponentAt(new Vector2Int(x, y));
        return id >= 0 && StateForRoom(id) != RoomState.Unexplored;
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

    private void DrawDoors(Rect rect)
    {
        GUI.BeginGroup(rect);
        Rect localRect = new(0f, 0f, rect.width, rect.height);
        float tileSize = TileSize(localRect);
        bool showLabels = tileSize >= 4.2f;

        foreach (PrisonDoor door in grid.Doors)
        {
            if (door == null) continue;
            Vector2Int cell = door.GridPosition;
            // Показываем дверь на границе исследованного — даже если за ней ещё туман.
            if (!HasVisitedNeighbor(cell.x, cell.y)) continue;

            Rect cr = CellRect(localRect, cell.x, cell.y, tileSize);
            float size = Mathf.Max(tileSize * 1.7f, 5f);
            Rect marker = new(cr.center.x - size * 0.5f, cr.center.y - size * 0.5f, size, size);

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(marker.x - 1f, marker.y - 1f, marker.width + 2f, marker.height + 2f), Texture2D.whiteTexture);
            GUI.color = DoorColor(door);
            GUI.DrawTexture(marker, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (showLabels)
            {
                string requirement = DoorRequirementLabel(door);
                if (!string.IsNullOrEmpty(requirement))
                {
                    GUI.Label(new Rect(marker.xMax + 4f, marker.y - 5f, 160f, 22f), requirement, doorLabelStyle);
                }
            }
        }

        GUI.color = Color.white;
        GUI.EndGroup();
    }

    private Color DoorColor(PrisonDoor door)
    {
        if (door.IsOpen) return DoorOpen;
        return IsDoorLocked(door) ? DoorLocked : Door;
    }

    /// <summary>Заперта ли дверь: система безопасности, требуется предмет или высокий доступ.</summary>
    private static bool IsDoorLocked(PrisonDoor door)
    {
        return door.IsSealed || door.Requirement != PrisonItemId.None;
    }

    /// <summary>Подпись требования у закрытой двери (null — без подписи).</summary>
    private static string DoorRequirementLabel(PrisonDoor door)
    {
        if (door.IsOpen) return null;
        PrisonItemId requirement = door.Requirement;
        if (requirement == PrisonItemId.Unavailable) return "нужен высокий доступ";
        if (requirement != PrisonItemId.None) return Player.GetItemName(requirement);
        return door.IsSealed ? "заперто" : null;
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
            if (!IsLabelVisible(center)) continue;   // не спойлерим неисследованное
            Rect cell = CellRect(localRect, center.x, center.y, tileSize);
            float width = Mathf.Clamp(label.Name.Length * 8f + 18f, 72f, 180f);
            Rect labelRect = new(cell.center.x - width * 0.5f, cell.center.y - 10f, width, 22f);
            GUI.Label(labelRect, label.Name, roomLabelStyle);
        }
        GUI.EndGroup();
    }

    /// <summary>Показывать ли подпись комнаты: только посещённой (смежные-силуэты не подписываем).</summary>
    private bool IsLabelVisible(Vector2Int center)
    {
        int id = grid.RoomGraph.ComponentAt(center);
        if (id >= 0) return RunState.IsRoomVisited(id);
        return HasVisitedNeighbor(center.x, center.y);
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
        float y = rect.y + 34f;
        const float step = 26f;
        DrawLegendRow(rect.x, y, Floor, "Исследовано"); y += step;
        DrawLegendRow(rect.x, y, SilhouetteFloor, "Смежное (контур)"); y += step;
        DrawLegendRow(rect.x, y, Fog, "Не исследовано"); y += step;
        DrawLegendRow(rect.x, y, Color.Lerp(Floor, ClearedAccent, 0.35f), "Зачищено"); y += step;
        DrawLegendRow(rect.x, y, RestrictedFloor, "Закрытая зона"); y += step;
        DrawLegendRow(rect.x, y, Cover, "Укрытие"); y += step;
        DrawLegendRow(rect.x, y, DoorOpen, "Дверь открыта"); y += step;
        DrawLegendRow(rect.x, y, Door, "Дверь закрыта"); y += step;
        DrawLegendRow(rect.x, y, DoorLocked, "Дверь заперта (нужен предмет)"); y += step;
        DrawLegendRow(rect.x, y, PlayerMarker, "Герой"); y += step;
        DrawLegendRow(rect.x, y, ObjectiveMarker, "Активная цель");
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
        doorLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.96f, 0.7f, 0.62f, 1f) },
        };
    }
}
