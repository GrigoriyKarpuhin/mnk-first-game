using UnityEngine;

/// <summary>
/// Константы для сортировки спрайтов по слоям.
/// 
/// Порядок слоёв (снизу вверх):
/// 1. Floor      - пол, всегда под всем
/// 2. Walls      - стены, сортируются по Y
/// 3. Entities   - игрок, NPC, предметы - сортируются по Y
/// 4. Foreground - люстры, декор поверх всего
/// </summary>
public static class SortingLayers
{
    // Базовые значения для каждого слоя
    public const int FloorBase = -10000;
    // Плоская стена (top-down): фиксированно НАД полом, но ПОД сущностями —
    // персонажи и пропы всегда поверх неё, ничего не перекрывается.
    public const int WallFlatBase = -5000;
    // Конусы обзора (фонарики охраны, зоны камер): над стенами, но под всеми сущностями.
    public const int VisionConeBase = -3000;
    public const int WallsBase = 0;      // Легаси Y-sort стен (больше не используется)
    public const int EntitiesBase = 0;   // Игрок, NPC, пропы — сортируются по Y
    public const int ForegroundBase = 10000;

    /// <summary>
    /// Вычисляет sorting order для объекта на основе его Y позиции.
    /// Чем ниже объект на экране - тем больше order (рисуется поверх).
    /// </summary>
    public static int GetOrderByY(float worldY, int layerBase)
    {
        // Умножаем на 100 для точности, инвертируем (ниже = больше)
        return layerBase + Mathf.RoundToInt(-worldY * 100);
    }

    /// <summary>
    /// Order для пола (всегда внизу, не зависит от Y)
    /// </summary>
    public static int Floor => FloorBase;

    /// <summary>
    /// Order для плоской стены (top-down): фиксированный, над полом, под сущностями.
    /// </summary>
    public static int WallFlat => WallFlatBase;

    /// <summary>
    /// Order для конуса обзора: над стенами, под сущностями.
    /// </summary>
    public static int VisionCone => VisionConeBase;

    /// <summary>
    /// Легаси: Y-sort стены. Не используется в плоском top-down, оставлено для совместимости.
    /// </summary>
    public static int Wall(float worldY) => GetOrderByY(worldY, WallsBase);

    /// <summary>
    /// Order для сущности (игрок, NPC) по Y позиции
    /// </summary>
    public static int Entity(float worldY) => GetOrderByY(worldY, EntitiesBase);

    /// <summary>
    /// Order для переднего плана (люстры, декор) по Y позиции
    /// </summary>
    public static int Foreground(float worldY) => GetOrderByY(worldY, ForegroundBase);
}
