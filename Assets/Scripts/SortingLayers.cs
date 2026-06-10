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
    // Стены и Entities на ОДНОМ уровне - сортируются между собой по Y
    public const int FloorBase = -10000;
    public const int WallsBase = 0;      // Стены и игрок конкурируют по Y
    public const int EntitiesBase = 0;   // На том же уровне что и стены
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
    /// Order для стены по Y позиции
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
