using UnityEngine;

/// <summary>
/// Общие фабрики для временной графики гонки (квадрат/круг на простых спрайтах).
/// </summary>
internal static class RaceVisuals
{
    public static GameObject Square(string objectName, Vector2 position, Vector2 size, Color color, int order)
    {
        var go = new GameObject(objectName);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrototypeSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go;
    }

    public static GameObject Circle(string objectName, Vector2 position, float scale, Color color, int order)
    {
        var go = new GameObject(objectName);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrototypeSprites.Circle;
        sr.color = color;
        sr.sortingOrder = order;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }
}

/// <summary>
/// Базовое препятствие гонки. Новые препятствия добавляются наследованием и
/// регистрацией в списке гонки — логику забега менять не нужно. Гонка лишь
/// тикает препятствия и задаёт им общие вопросы:
/// - <see cref="BlocksMovement"/>: статически непроходимо (ямы); и игрок, и боты обходят;
/// - <see cref="IsThreatNear"/>: подвижная опасность рядом/впереди (боты уклоняются);
/// - <see cref="HitsEntity"/>: контакт подвижной опасности с сущностью в этот кадр.
/// </summary>
public abstract class RaceObstacle
{
    protected GameObject visual;

    /// <summary>Препятствие отжило своё и должно быть удалено из гонки.</summary>
    public bool Expired { get; protected set; }

    public virtual void Tick(float dt) { }

    public virtual bool BlocksMovement(Vector3 worldPos) => false;

    public virtual bool IsThreatNear(Vector3 worldPos, float lookahead) => false;

    public virtual bool HitsEntity(Vector3 worldPos, float radius) => false;

    public void DestroyVisual()
    {
        if (visual != null) Object.Destroy(visual);
    }
}

/// <summary>Яма: непроходимая клетка. Обходится и игроком, и ботами.</summary>
public sealed class PitObstacle : RaceObstacle
{
    private readonly Rect rect;

    public PitObstacle(Vector2Int cell, float cellSize)
    {
        rect = new Rect(cell.x - cellSize * 0.5f, cell.y - cellSize * 0.5f, cellSize, cellSize);
        visual = RaceVisuals.Square("Pit", new Vector2(cell.x, cell.y),
            Vector2.one * cellSize * 0.92f, new Color(0.015f, 0.015f, 0.02f), -10);
    }

    public override bool BlocksMovement(Vector3 worldPos)
        => rect.Contains(new Vector2(worldPos.x, worldPos.y));
}

/// <summary>Камень: падает сверху вниз по колонке, сбивает с ног при контакте.</summary>
public sealed class RollingRockObstacle : RaceObstacle
{
    private readonly float fallSpeed;
    private readonly float despawnY;

    public RollingRockObstacle(Vector2 start, float fallSpeed, float despawnY, float size)
    {
        this.fallSpeed = fallSpeed;
        this.despawnY = despawnY;
        visual = RaceVisuals.Circle("Rolling Rock", start, size, new Color(0.24f, 0.16f, 0.10f), 8);
    }

    public override void Tick(float dt)
    {
        if (visual == null) { Expired = true; return; }
        visual.transform.position += Vector3.down * (fallSpeed * dt);
        if (visual.transform.position.y < despawnY) Expired = true;
    }

    public override bool HitsEntity(Vector3 worldPos, float radius)
        => visual != null && Vector2.Distance(worldPos, visual.transform.position) <= radius;

    public override bool IsThreatNear(Vector3 worldPos, float lookahead)
    {
        if (visual == null) return false;
        Vector3 rp = visual.transform.position;
        return Mathf.Abs(rp.x - worldPos.x) < 0.95f && rp.y >= worldPos.y - 0.6f && rp.y <= worldPos.y + lookahead;
    }
}

/// <summary>
/// Скользящая пила: ходит горизонтально по своей строке, сбивает с ног при контакте.
/// Пример нового препятствия, добавленного без изменения логики гонки.
/// </summary>
public sealed class SlidingSawObstacle : RaceObstacle
{
    private readonly float y;
    private readonly float minX;
    private readonly float maxX;
    private readonly float speed;
    private float x;
    private int dir = 1;

    public SlidingSawObstacle(float y, float minX, float maxX, float speed, float size)
    {
        this.y = y;
        this.minX = minX;
        this.maxX = maxX;
        this.speed = speed;
        x = minX;
        visual = RaceVisuals.Circle("Sliding Saw", new Vector2(x, y), size, new Color(0.72f, 0.74f, 0.80f), 8);
    }

    public override void Tick(float dt)
    {
        x += dir * speed * dt;
        if (x >= maxX) { x = maxX; dir = -1; }
        else if (x <= minX) { x = minX; dir = 1; }
        if (visual != null) visual.transform.position = new Vector3(x, y, 0f);
    }

    public override bool HitsEntity(Vector3 worldPos, float radius)
        => visual != null && Vector2.Distance(worldPos, visual.transform.position) <= radius;

    public override bool IsThreatNear(Vector3 worldPos, float lookahead)
        => Mathf.Abs(y - worldPos.y) < 1.3f && Mathf.Abs(x - worldPos.x) < 1.8f && worldPos.y <= y + 0.6f;
}
