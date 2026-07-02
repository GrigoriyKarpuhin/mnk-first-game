using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Карта тюрьмы в стиле CRT-терминала слежения. Сетка рисуется одним
/// point-фильтрованным <see cref="Texture2D"/> (пиксель = клетка) на
/// <see cref="RawImage"/>, поверх — маркеры игрока/цели/дверей и подписи комнат.
/// Логика тумана войны (состояния комнат, соседи, цвета) не изменилась —
/// поменялся только рендер (OnGUI → uGUI), поэтому карта теперь видна в
/// headless-скриншоте.
/// </summary>
public sealed class PrisonMapUI : MonoBehaviour
{
    // Цвета карты, согласованные с палитрой терминала.
    private static readonly Color Wall = UITheme.Hex("12211a");
    private static readonly Color Floor = UITheme.Hex("2c322c");
    private static readonly Color RestrictedFloor = UITheme.Hex("3a221c");
    private static readonly Color Door = UITheme.Hex("8a7320");
    private static readonly Color Cover = UITheme.Hex("34291a");
    private static readonly Color PlayerMarker = UITheme.Accent;
    private static readonly Color ObjectiveMarker = UITheme.Warning;
    private static readonly Color Fog = UITheme.Hex("06100a");
    private static readonly Color SilhouetteFloor = UITheme.Hex("0e1a12");
    private static readonly Color ClearedAccent = UITheme.Border;
    private static readonly Color DoorOpen = UITheme.Accent;
    private static readonly Color DoorLocked = UITheme.DangerBright;

    // Опорный размер области карты (в единицах 1280×720), под который вписываем сетку.
    private const float ViewportWidth = 900f;
    private const float ViewportHeight = 560f;

    private enum RoomState { Unexplored, Adjacent, Explored, Cleared }

    private static PrisonMapUI instance;

    private GameGrid grid;
    private Player player;
    private Dictionary<int, RoomState> roomStates;
    private float zoom = 1f;
    private float panX;
    private float panY;
    private float previousTimeScale = 1f;
    private bool open;

    private Canvas canvas;
    private GameObject mapRoot;
    private RectTransform viewport;
    private RawImage mapImage;
    private RectTransform mapRect;
    private RectTransform overlayRoot;
    private Texture2D mapTexture;
    private float mapScale = 1f; // ед. UI на клетку при zoom=1

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

    public static bool IsOpen => instance != null && instance.open;

    public static void Open(GameGrid activeGrid, Player activePlayer)
    {
        if (activeGrid == null || activePlayer == null) return;

        if (instance == null)
        {
            var go = new GameObject("Prison Map UI");
            instance = go.AddComponent<PrisonMapUI>();
            DontDestroyOnLoad(go);
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
        BuildUI();
    }

    private void BuildUI()
    {
        canvas = UIKit.CreateRootCanvas(gameObject, UITheme.SortMap);

        mapRoot = UIKit.CreatePanel("Map Root", canvas.transform, UITheme.Surface).gameObject;
        UIKit.FullStretch((RectTransform)mapRoot.transform);

        Text title = UIKit.CreateText("Title", mapRoot.transform, UITheme.TypeDisplay, TextAnchor.UpperLeft, UITheme.TextBright);
        title.text = "КАРТА ТЮРЬМЫ";
        title.fontStyle = FontStyle.Bold;
        UIKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(UITheme.Space8, -UITheme.Space6), new Vector2(-UITheme.Space8 * 2f, 52f));

        Text hint = UIKit.CreateStencilLabel(
            "M / ESC — ЗАКРЫТЬ · ↑/↓ МАСШТАБ · ЛКМ ТАЩИТ КАРТУ · ТЁМНОЕ — НЕ ИССЛЕДОВАНО",
            mapRoot.transform, TextAnchor.UpperLeft);
        UIKit.Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(UITheme.Space8, -UITheme.Space6 - 44f), new Vector2(-UITheme.Space8 * 2f, 22f));

        // Область карты (viewport с маской): левая зона под легенду справа.
        Image vpPanel = UIKit.CreateTerminalPanel("Viewport", mapRoot.transform, out RectTransform vpContent, scanlines: false);
        UIKit.Stretch(vpPanel.rectTransform, 32f, 24f, 308f, 110f);

        viewport = UIKit.CreatePanel("Clip", vpContent, UITheme.Well).rectTransform;
        UIKit.FullStretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        var mapGo = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        mapGo.transform.SetParent(viewport, false);
        mapImage = mapGo.GetComponent<RawImage>();
        mapImage.raycastTarget = false;
        mapRect = mapImage.rectTransform;
        mapRect.anchorMin = mapRect.anchorMax = mapRect.pivot = new Vector2(0.5f, 0.5f);

        overlayRoot = new GameObject("Overlay", typeof(RectTransform)).GetComponent<RectTransform>();
        overlayRoot.SetParent(mapRect, false);
        UIKit.FullStretch(overlayRoot);

        BuildLegend();

        mapRoot.SetActive(false);
    }

    private void BuildLegend()
    {
        Image legend = UIKit.CreateTerminalPanel("Legend", mapRoot.transform, out RectTransform legendContent, scanlines: false);
        UIKit.Stretch(legend.rectTransform, 998f, 24f, 32f, 110f);

        Text header = UIKit.CreateStencilLabel("ЛЕГЕНДА", legendContent, TextAnchor.UpperLeft);
        header.color = UITheme.Accent;
        UIKit.TopRect(header.rectTransform, 0f, 0f, 0f, 24f);

        (Color, string)[] rows =
        {
            (Floor, "Исследовано"),
            (SilhouetteFloor, "Смежное (контур)"),
            (Fog, "Не исследовано"),
            (Color.Lerp(Floor, ClearedAccent, 0.35f), "Зачищено"),
            (RestrictedFloor, "Закрытая зона"),
            (Cover, "Укрытие"),
            (DoorOpen, "Дверь открыта"),
            (Door, "Дверь закрыта"),
            (DoorLocked, "Дверь заперта"),
            (PlayerMarker, "Герой"),
            (ObjectiveMarker, "Активная цель"),
        };
        float y = 34f;
        foreach ((Color c, string label) in rows)
        {
            Image swatch = UIKit.CreatePanel("Swatch", legendContent, c);
            UIKit.Anchor(swatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(2f, -y - 2f), new Vector2(16f, 16f));
            Text t = UIKit.CreateText("L", legendContent, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.TextPrimary);
            t.text = label;
            UIKit.Anchor(t.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(24f, -y), new Vector2(-24f, 22f));
            y += 26f;
        }
    }

    private void Show(GameGrid activeGrid, Player activePlayer)
    {
        grid = activeGrid;
        player = activePlayer;
        roomStates = ComputeRoomStates();

        RebuildTexture();
        RebuildOverlays();
        zoom = 1f;
        panX = panY = 0f;
        ApplyTransform();

        mapRoot.SetActive(true);
        open = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void RebuildTexture()
    {
        int w = grid.Width, h = grid.Height;
        if (mapTexture != null) Destroy(mapTexture);
        mapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                pixels[y * w + x] = ColorForCell(x, y);
            }
        }
        mapTexture.SetPixels32(pixels);
        mapTexture.Apply();
        mapImage.texture = mapTexture;

        mapScale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(ViewportWidth / w, ViewportHeight / h)));
        mapRect.sizeDelta = new Vector2(w * mapScale, h * mapScale);
    }

    private void RebuildOverlays()
    {
        for (int i = overlayRoot.childCount - 1; i >= 0; i--) Destroy(overlayRoot.GetChild(i).gameObject);

        float cell = mapScale;
        bool showLabels = true;

        // Комнатные подписи (только исследованные/смежные).
        foreach (RoomLabel label in RoomLabels)
        {
            Vector2Int center = label.Center;
            if (!IsLabelVisible(center)) continue;
            Text t = UIKit.CreateText("Room", overlayRoot, UITheme.TypeCaption, TextAnchor.MiddleCenter, UITheme.TextStencil);
            t.text = label.Name;
            UIKit.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                CellLocal(center.x, center.y), new Vector2(Mathf.Clamp(label.Name.Length * 8f + 16f, 70f, 180f), 18f));
        }

        // Двери на границе исследованного.
        foreach (PrisonDoor door in grid.Doors)
        {
            if (door == null) continue;
            Vector2Int c = door.GridPosition;
            if (!HasVisitedNeighbor(c.x, c.y)) continue;
            AddDot(CellLocal(c.x, c.y), Mathf.Max(cell * 1.7f, 6f), DoorColor(door));

            if (showLabels)
            {
                string requirement = DoorRequirementLabel(door);
                if (!string.IsNullOrEmpty(requirement))
                {
                    Text dl = UIKit.CreateText("DoorLabel", overlayRoot, UITheme.TypeCaption, TextAnchor.MiddleLeft, UITheme.Warning);
                    dl.text = requirement;
                    UIKit.Anchor(dl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                        CellLocal(c.x, c.y) + new Vector2(cell * 1.2f, 0f), new Vector2(160f, 18f));
                }
            }
        }

        // Активная цель.
        if (RunState.TryGetActiveQuestTarget(out Vector2Int target, out string objLabel))
        {
            AddDot(CellLocal(target.x, target.y), Mathf.Max(cell * 3.4f, 12f), ObjectiveMarker);
            Text ol = UIKit.CreateText("ObjLabel", overlayRoot, UITheme.TypeLabel, TextAnchor.MiddleLeft, ObjectiveMarker);
            ol.text = objLabel;
            ol.fontStyle = FontStyle.Bold;
            UIKit.Anchor(ol.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                CellLocal(target.x, target.y) + new Vector2(cell * 2f, 0f), new Vector2(200f, 22f));
        }

        // Игрок.
        Vector2Int p = player.GridPosition;
        AddDot(CellLocal(p.x, p.y), Mathf.Max(cell * 2.5f, 8f), PlayerMarker);
    }

    private void AddDot(Vector2 localPos, float size, Color color)
    {
        Image border = UIKit.CreatePanel("Dot", overlayRoot, UITheme.Surface);
        UIKit.Anchor(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            localPos, new Vector2(size + 3f, size + 3f));
        Image dot = UIKit.CreatePanel("DotFill", border.transform, color);
        UIKit.FullStretch(dot.rectTransform);
        dot.rectTransform.offsetMin = new Vector2(1.5f, 1.5f);
        dot.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);
    }

    /// <summary>Позиция центра клетки в локальных координатах карты (pivot центр).</summary>
    private Vector2 CellLocal(int x, int y)
    {
        float w = mapRect.rect.width, h = mapRect.rect.height;
        float lx = ((x + 0.5f) / grid.Width - 0.5f) * w;
        float ly = ((y + 0.5f) / grid.Height - 0.5f) * h;
        return new Vector2(lx, ly);
    }

    private void Update()
    {
        if (!open) return;

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

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Vector2 drag = Mouse.current.delta.ReadValue();
            panX += drag.x;
            panY += drag.y;
        }

        ClampPan();
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        if (mapRect == null) return;
        mapRect.localScale = new Vector3(zoom, zoom, 1f);
        mapRect.anchoredPosition = new Vector2(panX, panY);
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
        float excessX = Mathf.Max(0f, mapRect.rect.width * zoom - ViewportWidth) * 0.5f;
        float excessY = Mathf.Max(0f, mapRect.rect.height * zoom - ViewportHeight) * 0.5f;
        panX = Mathf.Clamp(panX, -excessX, excessX);
        panY = Mathf.Clamp(panY, -excessY, excessY);
    }

    private void Close()
    {
        open = false;
        mapRoot.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    private void OnDestroy()
    {
        if (mapTexture != null) Destroy(mapTexture);
    }

    // === Логика тумана войны (не изменилась) ==============================

    private Dictionary<int, RoomState> ComputeRoomStates()
    {
        RoomGraph graph = grid.RoomGraph;
        var unresolved = new Dictionary<int, int>();

        void Bump(Vector2Int cell)
        {
            int id = graph.ComponentAt(cell);
            if (id < 0) return;
            unresolved.TryGetValue(id, out int n);
            unresolved[id] = n + 1;
        }

        foreach (Item item in FindObjectsByType<Item>(FindObjectsSortMode.None))
        {
            Bump(grid.WorldToGrid(item.transform.position));
        }

        foreach (IRoomObjective objective in grid.RoomObjectives)
        {
            if (objective == null || objective.IsObjectiveResolved) continue;
            Bump(objective.Cell);
        }

        var states = new Dictionary<int, RoomState>();
        foreach (RoomGraph.Room room in graph.Rooms)
        {
            if (!RunState.IsRoomVisited(room.Id))
            {
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

    private Color ColorForCell(int x, int y)
    {
        int id = grid.RoomGraph.ComponentAt(new Vector2Int(x, y));
        if (id >= 0)
        {
            switch (StateForRoom(id))
            {
                case RoomState.Unexplored: return Fog;
                case RoomState.Adjacent: return SilhouetteFloor;
                case RoomState.Cleared: return Color.Lerp(BaseColorForCell(x, y), ClearedAccent, 0.35f);
                default: return BaseColorForCell(x, y);
            }
        }

        if (HasVisitedNeighbor(x, y)) return BaseColorForCell(x, y);
        if (HasRevealedNeighbor(x, y)) return Wall;
        return Fog;
    }

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

    private Color DoorColor(PrisonDoor door)
    {
        if (door.IsOpen) return DoorOpen;
        return IsDoorLocked(door) ? DoorLocked : Door;
    }

    private static bool IsDoorLocked(PrisonDoor door)
    {
        return door.IsSealed || door.Requirement != PrisonItemId.None;
    }

    private static string DoorRequirementLabel(PrisonDoor door)
    {
        if (door.IsOpen) return null;
        PrisonItemId requirement = door.Requirement;
        if (requirement == PrisonItemId.Unavailable) return "нужен высокий доступ";
        if (requirement != PrisonItemId.None) return Player.GetItemName(requirement);
        return door.IsSealed ? "заперто" : null;
    }

    private bool IsLabelVisible(Vector2Int center)
    {
        int id = grid.RoomGraph.ComponentAt(center);
        if (id >= 0) return RunState.IsRoomVisited(id);
        return HasVisitedNeighbor(center.x, center.y);
    }
}
