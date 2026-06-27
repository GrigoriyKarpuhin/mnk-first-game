using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Инварианты геометрии Block C: каждая дверь — проём в стене (а не «висит» в полу),
/// каждая камера смотрит в комнату, а не вплотную в стену/дверь. Тесты строят грид из
/// тех же данных <see cref="BlockCPlayableLayout"/> / <see cref="PrisonDefaults"/>, что и рантайм.
/// </summary>
[TestFixture]
public class LayoutIntegrityTests
{
    private GameObject gridObject;
    private GameGrid grid;

    // Намеренно запечатанные проёмы-«обещания»: пол только с одной стороны, остальное — стена.
    // KIT-05 «странная дверь» в северный резерв (BLOCK_C_BLOCKOUT_V02.md).
    private static readonly HashSet<Vector2Int> SealedPromiseDoors = new()
    {
        new Vector2Int(72, 52),
    };

    private const int MinCameraVisibleFloorCells = 4;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("TestGrid");
        grid = gridObject.AddComponent<GameGrid>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void EveryDoor_IsFramedByWall()
    {
        var offenders = new List<string>();

        foreach (Vector2Int door in BlockCPlayableLayout.DoorCells)
        {
            if (SealedPromiseDoors.Contains(door)) continue;

            TileType left = grid.GetTileType(door.x - 1, door.y);
            TileType right = grid.GetTileType(door.x + 1, door.y);
            TileType up = grid.GetTileType(door.x, door.y + 1);
            TileType down = grid.GetTileType(door.x, door.y - 1);

            bool horizontalDoorway = IsPassable(left) && IsPassable(right) && IsJamb(up) && IsJamb(down);
            bool verticalDoorway = IsPassable(up) && IsPassable(down) && IsJamb(left) && IsJamb(right);

            if (!horizontalDoorway && !verticalDoorway)
            {
                offenders.Add($"({door.x},{door.y}) L={left} R={right} U={up} D={down}");
            }
        }

        Assert.IsEmpty(offenders,
            "Двери без обрамляющей стены (нужно: проходимо с двух противоположных сторон, стена/дверь с двух других):\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void EveryCamera_SeesIntoRoomNotWall()
    {
        var offenders = new List<string>();

        foreach (DefaultCamera cam in PrisonDefaults.Cameras())
        {
            Vector2Int front = cam.Cell + cam.Facing;
            if (grid.BlocksVision(front.x, front.y))
            {
                offenders.Add($"{cam.Name} @({cam.Cell.x},{cam.Cell.y}) смотрит в {grid.GetTileType(front.x, front.y)} на ({front.x},{front.y})");
                continue;
            }

            int visible = CountVisibleFloorCells(cam);
            if (visible < MinCameraVisibleFloorCells)
            {
                offenders.Add($"{cam.Name} @({cam.Cell.x},{cam.Cell.y}) видит лишь {visible} клеток пола");
            }
        }

        Assert.IsEmpty(offenders,
            "Камеры смотрят в стену или почти ничего не видят:\n" + string.Join("\n", offenders));
    }

    private int CountVisibleFloorCells(DefaultCamera cam)
    {
        int count = 0;
        for (int x = cam.Cell.x - cam.Range; x <= cam.Cell.x + cam.Range; x++)
        {
            for (int y = cam.Cell.y - cam.Range; y <= cam.Cell.y + cam.Range; y++)
            {
                var target = new Vector2Int(x, y);
                if (target == cam.Cell) continue;
                if (grid.GetTileType(x, y) != TileType.Floor) continue;
                if (VisionMath.CanSeeCell(grid, cam.Cell, cam.Facing, cam.Range, target)) count++;
            }
        }
        return count;
    }

    private static bool IsPassable(TileType type) => type == TileType.Floor;
    private static bool IsJamb(TileType type) => type == TileType.Wall || type == TileType.Door;
}
