using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Граф комнат, выведенный из логического грида <see cref="GameGrid"/> заливкой.
///
/// Модель: внутренность комнаты — клетки <see cref="TileType.Floor"/> или
/// <see cref="TileType.Cover"/> (укрытие стоит на полу и считается частью комнаты).
/// Граница — <see cref="TileType.Wall"/>. Дверь (<see cref="TileType.Door"/>) —
/// портал: при заливке её не пересекаем, но она порождает ребро между двумя
/// комнатами по разные стороны проёма.
///
/// Источник истины — те же тайлы, что и в рантайме: грид строится в
/// <c>GameGrid.InitializeGrid</c> из <see cref="BlockCPlayableLayout"/>. Заливка
/// читает грид через <see cref="GameGrid.GetTileType"/> (ленивая инициализация),
/// поэтому строитель работает и в EditMode без сцены — как в LayoutIntegrityTests.
/// </summary>
public sealed class RoomGraph
{
    /// <summary>Связная компонента внутренних клеток — одна «комната» в навигации.</summary>
    public sealed class Room
    {
        public int Id;
        public readonly List<Vector2Int> Cells = new();
        public Vector2Int Centroid;
        public Vector2Int Min;
        public Vector2Int Max;
        public int Floor;                                       // 1 или 2
        public readonly SortedSet<int> Neighbors = new();       // комнаты, соединённые дверью
        public readonly List<Vector2Int> DeadEndDoors = new();  // дверь с полом только с одной стороны
    }

    /// <summary>Ребро графа: дверь, соединяющая две комнаты.</summary>
    public readonly struct Edge
    {
        public readonly int A;
        public readonly int B;
        public readonly Vector2Int Door;

        public Edge(int a, int b, Vector2Int door)
        {
            A = a;
            B = b;
            Door = door;
        }
    }

    public readonly List<Room> Rooms = new();
    public readonly List<Edge> Edges = new();
    public readonly List<Vector2Int> OrphanDoors = new();       // дверь без пола ни с одной стороны

    private int width;
    private int height;
    private int[,] component;                                   // id комнаты или -1

    /// <summary>id комнаты в клетке или -1, если клетка не внутренность/за гридом.</summary>
    public int ComponentAt(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height) return -1;
        return component[cell.x, cell.y];
    }

    public Room RoomAt(Vector2Int cell)
    {
        int id = ComponentAt(cell);
        return id >= 0 ? Rooms[id] : null;
    }

    private static bool IsInterior(TileType type) => type == TileType.Floor || type == TileType.Cover;

    /// <summary>Построить граф комнат из грида.</summary>
    public static RoomGraph Build(GameGrid grid)
    {
        var g = new RoomGraph
        {
            width = BlockCPlayableLayout.Width,
            height = BlockCPlayableLayout.Height,
        };
        g.component = new int[g.width, g.height];
        for (int x = 0; x < g.width; x++)
            for (int y = 0; y < g.height; y++)
                g.component[x, y] = -1;

        g.FloodRooms(grid);
        g.LinkDoors(grid);
        return g;
    }

    private static readonly Vector2Int[] Dirs =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private void FloodRooms(GameGrid grid)
    {
        var queue = new Queue<Vector2Int>();
        for (int sx = 0; sx < width; sx++)
        {
            for (int sy = 0; sy < height; sy++)
            {
                if (component[sx, sy] != -1 || !IsInterior(grid.GetTileType(sx, sy))) continue;

                int id = Rooms.Count;
                var room = new Room
                {
                    Id = id,
                    Min = new Vector2Int(sx, sy),
                    Max = new Vector2Int(sx, sy),
                };
                Rooms.Add(room);

                component[sx, sy] = id;
                queue.Enqueue(new Vector2Int(sx, sy));
                long sumX = 0, sumY = 0;

                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    room.Cells.Add(c);
                    sumX += c.x;
                    sumY += c.y;
                    room.Min = new Vector2Int(Mathf.Min(room.Min.x, c.x), Mathf.Min(room.Min.y, c.y));
                    room.Max = new Vector2Int(Mathf.Max(room.Max.x, c.x), Mathf.Max(room.Max.y, c.y));

                    foreach (Vector2Int d in Dirs)
                    {
                        int nx = c.x + d.x, ny = c.y + d.y;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        if (component[nx, ny] != -1) continue;
                        if (!IsInterior(grid.GetTileType(nx, ny))) continue;
                        component[nx, ny] = id;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                int n = room.Cells.Count;
                room.Centroid = new Vector2Int(Mathf.RoundToInt((float)sumX / n), Mathf.RoundToInt((float)sumY / n));
                room.Floor = room.Min.y >= BlockCPlayableLayout.Floor2OffsetY ? 2 : 1;
            }
        }
    }

    private void LinkDoors(GameGrid grid)
    {
        var seenPairs = new HashSet<long>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid.GetTileType(x, y) != TileType.Door) continue;

                int left = InteriorComponent(grid, x - 1, y);
                int right = InteriorComponent(grid, x + 1, y);
                int up = InteriorComponent(grid, x, y + 1);
                int down = InteriorComponent(grid, x, y - 1);

                var door = new Vector2Int(x, y);
                // Ребро — только если дверь соединяет ДВЕ РАЗНЫЕ комнаты по
                // противоположным сторонам проёма (как обрамление двери в
                // LayoutIntegrityTests: проходимо с двух противоположных сторон).
                bool linked = TryPair(left, right, door, seenPairs)
                            | TryPair(up, down, door, seenPairs);
                if (linked) continue;

                // Связи не вышло. Дальше — диагностика формы проёма:
                //  • противоположные стороны проходимы (та же комната) — внутренний
                //    проём, это валидно и не нарушение;
                //  • пол хотя бы с одной стороны, но не «дверь насквозь» — тупик
                //    (sealed promise) или кривой угловой проём;
                //  • пола нет вообще — «дверь в никуда» (сирота).
                bool throughShape = (left >= 0 && right >= 0) || (up >= 0 && down >= 0);
                if (throughShape) continue;

                int side = left >= 0 ? left : right >= 0 ? right : up >= 0 ? up : down;
                if (side >= 0) Rooms[side].DeadEndDoors.Add(door);
                else OrphanDoors.Add(door);
            }
        }
    }

    private int InteriorComponent(GameGrid grid, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return -1;
        return IsInterior(grid.GetTileType(x, y)) ? component[x, y] : -1;
    }

    private bool TryPair(int a, int b, Vector2Int door, HashSet<long> seenPairs)
    {
        if (a < 0 || b < 0 || a == b) return false;
        Rooms[a].Neighbors.Add(b);
        Rooms[b].Neighbors.Add(a);
        long key = a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
        if (seenPairs.Add(key)) Edges.Add(new Edge(a, b, door));
        return true;
    }
}
