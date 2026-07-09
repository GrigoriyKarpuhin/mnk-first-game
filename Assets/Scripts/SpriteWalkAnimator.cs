using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Лёгкая покадровая анимация ходьбы без Animator: следит за смещением
/// transform и сам переключает кадры idle/walk. Кадры берутся из
/// Resources/Sprites: "&lt;base&gt;", "&lt;base&gt;_walk_1", "&lt;base&gt;_walk_2".
/// Дополнительно поддерживает ракурсы "&lt;base&gt;_side" (профиль, смотрит
/// влево; вправо — через flipX) и "&lt;base&gt;_up" (спина) с теми же
/// суффиксами кадров. Если ракурсов нет в Resources, поведение прежнее:
/// фронтальный сет с отражением по X.
/// </summary>
public class SpriteWalkAnimator : MonoBehaviour
{
    private const float FrameTime = 0.12f;
    private const float MoveGrace = 0.1f; // сколько держать "идёт" после последнего сдвига

    private const int DirDown = 0;
    private const int DirSide = 1;
    private const int DirUp = 2;

    /// <summary>
    /// Одна one-shot анимация (подбор/драка/удушение/бросок): кадры по направлениям +
    /// текущее состояние проигрывания. Суффикс имени класса-обёртки — ключ в
    /// Resources ("&lt;base&gt;_&lt;suffix&gt;", "&lt;base&gt;_side_&lt;suffix&gt;", ...).
    /// </summary>
    private class OneShotAnim
    {
        public readonly Sprite[][] framesByDir = new Sprite[3][];
        public float until = -1f;
        public float duration;
        // pickup: присел -> дотянулся -> присел (симметрично);
        // fight/choke/throw: замах -> действие, без возврата к первому кадру.
        public bool threePhase;
    }

    private SpriteRenderer spriteRenderer;
    private readonly Sprite[] idleByDir = new Sprite[3];
    private readonly Sprite[][] cycleByDir = new Sprite[3][];
    private readonly Dictionary<string, OneShotAnim> oneShots = new Dictionary<string, OneShotAnim>
    {
        { "pickup", new OneShotAnim { threePhase = true } },
        { "fight", new OneShotAnim() },
        { "choke", new OneShotAnim() },
        { "throw", new OneShotAnim() },
    };
    private OneShotAnim activeOneShot;
    private int dir = DirDown;
    private bool faceRight;
    private float timer;
    private int frame;
    private Vector3 lastPosition;
    private float movingUntil;

    /// <summary>Идёт ли сейчас one-shot анимация подбора.</summary>
    public bool IsPickingUp => IsPlaying("pickup");

    /// <summary>Идёт ли сейчас one-shot анимация с данным именем ("pickup"/"fight"/"choke"/"throw").</summary>
    public bool IsPlaying(string action)
    {
        return oneShots.TryGetValue(action, out OneShotAnim anim) && Time.time < anim.until;
    }

    /// <summary>
    /// Явно задаёт направление взгляда (поворот на месте, напр. охрана на концах
    /// патруля). Применяется к idle-кадру в LateUpdate; при движении ракурс
    /// перебивается фактическим смещением.
    /// </summary>
    public void SetFacing(Vector2Int facing)
    {
        if (facing.x != 0)
        {
            dir = DirSide;
            faceRight = facing.x > 0;
        }
        else if (facing.y != 0)
        {
            dir = facing.y > 0 ? DirUp : DirDown;
        }
        // На этом кадре ракурс задан явно — не давать дельте движения (в т.ч.
        // остаточной на кадре прибытия в точку патруля) перебить его обратно.
        facingSetThisFrame = true;
    }

    private bool facingSetThisFrame;

    /// <summary>
    /// Запускает one-shot анимацию с именем ("pickup"/"fight"/"choke"/"throw") в
    /// текущем ракурсе. Возвращает её длительность; 0, если кадров нет в
    /// Resources или имя не зарегистрировано.
    /// </summary>
    public float Play(string action, float duration)
    {
        if (!oneShots.TryGetValue(action, out OneShotAnim anim)) return 0f;
        if (anim.framesByDir[DirDown] == null && anim.framesByDir[dir] == null) return 0f;
        anim.duration = duration;
        anim.until = Time.time + duration;
        activeOneShot = anim;
        return duration;
    }

    /// <summary>Запускает анимацию подбора (присел — дотянулся — встал).</summary>
    public float PlayPickup(float duration = 0.45f) => Play("pickup", duration);

    /// <summary>
    /// Вешает аниматор на объект, если в Resources есть фронтальные кадры.
    /// Возвращает null, если кадров нет (объект остаётся со статичным спрайтом).
    /// </summary>
    public static SpriteWalkAnimator TryAttach(GameObject target, string spriteBase)
    {
        Sprite[] down = LoadSet(spriteBase);
        if (down == null) return null;

        var animator = target.GetComponent<SpriteWalkAnimator>();
        if (animator == null) animator = target.AddComponent<SpriteWalkAnimator>();
        animator.SetSpriteBase(spriteBase, down);
        return animator;
    }

    public bool SetSpriteBase(string spriteBase)
    {
        Sprite[] down = LoadSet(spriteBase);
        if (down == null) return false;
        SetSpriteBase(spriteBase, down);
        return true;
    }

    private void SetSpriteBase(string spriteBase, Sprite[] down)
    {
        ClearDirections();
        SetDirection(DirDown, down);
        SetDirection(DirSide, LoadSet(spriteBase + "_side"));
        SetDirection(DirUp, LoadSet(spriteBase + "_up"));
        foreach (var entry in oneShots)
        {
            string suffix = entry.Key;
            OneShotAnim anim = entry.Value;
            anim.framesByDir[DirDown] = LoadPair(spriteBase + "_" + suffix);
            anim.framesByDir[DirSide] = LoadPair(spriteBase + "_side_" + suffix);
            anim.framesByDir[DirUp] = LoadPair(spriteBase + "_up_" + suffix);
        }

        timer = 0f;
        frame = 0;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = idleByDir[dir] ?? idleByDir[DirDown];
        }
        enabled = true;
    }

    private void ClearDirections()
    {
        activeOneShot = null;
        for (int i = 0; i < 3; i++)
        {
            idleByDir[i] = null;
            cycleByDir[i] = null;
        }
        foreach (OneShotAnim anim in oneShots.Values)
        {
            anim.until = -1f;
            for (int i = 0; i < 3; i++) anim.framesByDir[i] = null;
        }
    }

    private static Sprite[] LoadSet(string spriteBase)
    {
        Sprite idle = Resources.Load<Sprite>(SpriteCatalog.Resolve(spriteBase));
        Sprite walk1 = Resources.Load<Sprite>(SpriteCatalog.Resolve(spriteBase + "_walk_1"));
        Sprite walk2 = Resources.Load<Sprite>(SpriteCatalog.Resolve(spriteBase + "_walk_2"));
        if (idle == null || walk1 == null || walk2 == null) return null;
        return new[] { FeetAnchored(idle), FeetAnchored(walk1), FeetAnchored(walk2) };
    }

    private static Sprite[] LoadPair(string spriteBase)
    {
        Sprite s1 = Resources.Load<Sprite>(SpriteCatalog.Resolve(spriteBase + "_1"));
        Sprite s2 = Resources.Load<Sprite>(SpriteCatalog.Resolve(spriteBase + "_2"));
        if (s1 == null || s2 == null) return null;
        return new[] { FeetAnchored(s1), FeetAnchored(s2) };
    }

    private static readonly Dictionary<Sprite, Sprite> feetCache = new Dictionary<Sprite, Sprite>();

    /// <summary>
    /// Возвращает копию спрайта с пивотом в низ-центр (ступни). Персонаж
    /// позиционируется по клетке через transform.position = центр клетки, а
    /// исходный пивот по центру холста ронял ступни на ~0.7 клетки ниже. Пивот
    /// у низа ставит ступни в клетку, ракурсы/персонажи согласованы. Кэшируется.
    /// </summary>
    public static Sprite FeetAnchored(Sprite source)
    {
        if (source == null) return null;
        if (feetCache.TryGetValue(source, out Sprite cached)) return cached;
        Sprite feet = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0f),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        feetCache[source] = feet;
        return feet;
    }

    private void SetDirection(int direction, Sprite[] set)
    {
        if (set == null) return;
        idleByDir[direction] = set[0];
        cycleByDir[direction] = new[] { set[1], set[0], set[2], set[0] };
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
        }
        if (idleByDir[DirDown] == null) return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        if (!facingSetThisFrame && delta.sqrMagnitude > 0.0000001f)
        {
            movingUntil = Time.time + MoveGrace;
            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            if (horizontal && Mathf.Abs(delta.x) > 0.0001f)
            {
                dir = DirSide;
                faceRight = delta.x > 0f;
            }
            else if (!horizontal && Mathf.Abs(delta.y) > 0.0001f)
            {
                dir = delta.y > 0f ? DirUp : DirDown;
            }
        }
        facingSetThisFrame = false;

        // Откат на фронтальный сет, если для текущего ракурса нет арта.
        int useDir = cycleByDir[dir] != null ? dir : DirDown;
        // Профильный арт смотрит влево, вправо — через flipX. Если профильного
        // арта нет, сохраняем старое поведение: отражаем фронтальный кадр.
        spriteRenderer.flipX = dir == DirSide && faceRight;

        if (activeOneShot != null && Time.time < activeOneShot.until)
        {
            Sprite[] frames = activeOneShot.framesByDir[dir] ?? activeOneShot.framesByDir[DirDown];
            if (frames != null)
            {
                float t = 1f - (activeOneShot.until - Time.time) / activeOneShot.duration;
                int idx = activeOneShot.threePhase
                    ? (t < 0.3f ? 0 : t < 0.75f ? 1 : 0) // присел — дотянулся — выпрямляется
                    : (t < 0.5f ? 0 : 1);                // замах — удар/бросок
                spriteRenderer.sprite = frames[idx];
                timer = 0f;
                frame = 0;
                return;
            }
        }

        if (Time.time < movingUntil)
        {
            timer += Time.deltaTime;
            if (timer >= FrameTime)
            {
                timer = 0f;
                frame = (frame + 1) % 4;
            }
            spriteRenderer.sprite = cycleByDir[useDir][frame];
        }
        else
        {
            timer = 0f;
            frame = 0;
            spriteRenderer.sprite = idleByDir[useDir];
        }
    }
}
