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
        CharacterGroundShadow.Attach(go);
        return go;
    }

    /// <summary>
    /// Прямоугольный объект со спрайтом из Resources/Sprites. Если спрайта нет —
    /// фолбэк на тонированный квадрат, чтобы прототипы работали без ассетов.
    /// </summary>
    public static GameObject Art(string objectName, string spriteName, Vector2 position,
        Vector2 worldSize, Color tint, int order)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/" + spriteName);
        if (sprite == null) return Square(objectName, position, worldSize, tint, order);

        var go = new GameObject(objectName);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = order;
        Vector2 bounds = sprite.bounds.size;
        go.transform.localScale = new Vector3(worldSize.x / bounds.x, worldSize.y / bounds.y, 1f);
        return go;
    }

    /// <summary>
    /// Персонаж со спрайтом и анимацией ходьбы из Resources/Sprites.
    /// Фолбэк — тонированный круг (старый вид прототипа).
    /// </summary>
    public static GameObject Character(string objectName, string spriteBase, Vector2 position,
        float scale, Color tint, int order)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/" + spriteBase);
        if (sprite == null) return Circle(objectName, position, scale, tint, order);

        var go = new GameObject(objectName);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = order;
        go.transform.localScale = Vector3.one * scale /
            Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        SpriteWalkAnimator.TryAttach(go, spriteBase);
        CharacterGroundShadow.Attach(go);
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

    /// <summary>
    /// Направление отброса сущности при попадании подвижной опасности (камень, пила).
    /// Нулевой вектор — без отброса (по умолчанию). Нормализуется на стороне гонки.
    /// </summary>
    public virtual Vector2 HitPush(Vector3 worldPos) => Vector2.zero;

    /// <summary>
    /// Вектор «отталкивания» от препятствия для стиринга ботов (boids-избегание).
    /// Возвращает нулевой вектор, если препятствие далеко. Горизонтально смещён,
    /// чтобы боты объезжали вбок, а не пятились назад.
    /// </summary>
    public virtual Vector2 AvoidanceForce(Vector3 worldPos, float radius) => Vector2.zero;

    protected static Vector2 PushAway(Vector2 from, Vector2 obstaclePos, float radius)
    {
        Vector2 d = from - obstaclePos;
        float dist = d.magnitude;
        if (dist >= radius) return Vector2.zero;
        float strength = 1f - dist / radius;
        if (dist < 0.25f) // почти на препятствии — толкаем к более открытой стороне трассы
            return new Vector2(obstaclePos.x >= 0f ? -1f : 1f, 0f) * strength;
        return new Vector2(d.x, d.y * 0.2f).normalized * strength; // сильнее вбок, чем назад
    }

    public void DestroyVisual()
    {
        if (visual != null) Object.Destroy(visual);
    }
}

/// <summary>Яма: непроходимая клетка. Обходится и игроком, и ботами.</summary>
public sealed class PitObstacle : RaceObstacle
{
    private readonly Rect rect;
    private readonly Vector2 center;

    public PitObstacle(Vector2Int cell, float cellSize)
    {
        rect = new Rect(cell.x - cellSize * 0.5f, cell.y - cellSize * 0.5f, cellSize, cellSize);
        center = new Vector2(cell.x, cell.y);
        visual = RaceVisuals.Art("Pit", "pit", new Vector2(cell.x, cell.y),
            Vector2.one * cellSize * 0.92f, Color.white, -10);
    }

    public override bool BlocksMovement(Vector3 worldPos)
        => rect.Contains(new Vector2(worldPos.x, worldPos.y));

    public override Vector2 AvoidanceForce(Vector3 worldPos, float radius)
        => PushAway(new Vector2(worldPos.x, worldPos.y), center, radius);
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
        visual = RaceVisuals.Art("Rolling Rock", "rock", start,
            Vector2.one * size, Color.white, 8);
    }

    public override void Tick(float dt)
    {
        if (visual == null) { Expired = true; return; }
        visual.transform.position += Vector3.down * (fallSpeed * dt);
        visual.transform.Rotate(0f, 0f, 90f * dt); // камень слегка вращается при падении
        if (visual.transform.position.y < despawnY) Expired = true;
    }

    public override bool HitsEntity(Vector3 worldPos, float radius)
        => visual != null && Vector2.Distance(worldPos, visual.transform.position) <= radius;

    /// <summary>Камень падает сверху: отбрасывает вниз и в более открытую сторону трассы.</summary>
    public override Vector2 HitPush(Vector3 worldPos)
    {
        if (visual == null) return Vector2.down;
        float dx = worldPos.x - visual.transform.position.x;
        float side = Mathf.Abs(dx) < 0.1f ? (visual.transform.position.x >= 0f ? -1f : 1f) : Mathf.Sign(dx);
        return new Vector2(side * 0.6f, -1f);
    }

    public override bool IsThreatNear(Vector3 worldPos, float lookahead)
    {
        if (visual == null) return false;
        Vector3 rp = visual.transform.position;
        return Mathf.Abs(rp.x - worldPos.x) < 0.95f && rp.y >= worldPos.y - 0.6f && rp.y <= worldPos.y + lookahead;
    }

    public override Vector2 AvoidanceForce(Vector3 worldPos, float radius)
    {
        if (visual == null) return Vector2.zero;
        return PushAway(new Vector2(worldPos.x, worldPos.y), visual.transform.position, radius);
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
        visual = RaceVisuals.Art("Sliding Saw", "saw", new Vector2(x, y),
            Vector2.one * size, Color.white, 8);
    }

    public override void Tick(float dt)
    {
        x += dir * speed * dt;
        if (x >= maxX) { x = maxX; dir = -1; }
        else if (x <= minX) { x = minX; dir = 1; }
        if (visual != null)
        {
            visual.transform.position = new Vector3(x, y, 0f);
            visual.transform.Rotate(0f, 0f, -dir * 540f * dt); // пила крутится
        }
    }

    public override bool HitsEntity(Vector3 worldPos, float radius)
        => visual != null && Vector2.Distance(worldPos, visual.transform.position) <= radius;

    /// <summary>Пила идёт горизонтально: отбрасывает вбок по ходу своего движения.</summary>
    public override Vector2 HitPush(Vector3 worldPos)
    {
        float dx = worldPos.x - x;
        float side = Mathf.Abs(dx) < 0.1f ? dir : Mathf.Sign(dx);
        return new Vector2(side, -0.25f);
    }

    public override bool IsThreatNear(Vector3 worldPos, float lookahead)
        => Mathf.Abs(y - worldPos.y) < 1.3f && Mathf.Abs(x - worldPos.x) < 1.8f && worldPos.y <= y + 0.6f;

    public override Vector2 AvoidanceForce(Vector3 worldPos, float radius)
        => PushAway(new Vector2(worldPos.x, worldPos.y), new Vector2(x, y), radius);
}
