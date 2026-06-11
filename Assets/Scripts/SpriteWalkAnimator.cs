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

    private SpriteRenderer spriteRenderer;
    private readonly Sprite[] idleByDir = new Sprite[3];
    private readonly Sprite[][] cycleByDir = new Sprite[3][];
    private readonly Sprite[][] pickupByDir = new Sprite[3][];
    private int dir = DirDown;
    private bool faceRight;
    private float timer;
    private int frame;
    private Vector3 lastPosition;
    private float movingUntil;
    private float pickupUntil = -1f;
    private float pickupDuration;

    /// <summary>Идёт ли сейчас one-shot анимация подбора.</summary>
    public bool IsPickingUp => Time.time < pickupUntil;

    /// <summary>
    /// Запускает анимацию подбора (присел — дотянулся — встал) в текущем
    /// ракурсе. Возвращает её длительность; 0, если кадров нет в Resources.
    /// </summary>
    public float PlayPickup(float duration = 0.45f)
    {
        if (pickupByDir[DirDown] == null && pickupByDir[dir] == null) return 0f;
        pickupDuration = duration;
        pickupUntil = Time.time + duration;
        return duration;
    }

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
        animator.SetDirection(DirDown, down);
        animator.SetDirection(DirSide, LoadSet(spriteBase + "_side"));
        animator.SetDirection(DirUp, LoadSet(spriteBase + "_up"));
        animator.pickupByDir[DirDown] = LoadPair(spriteBase + "_pickup");
        animator.pickupByDir[DirSide] = LoadPair(spriteBase + "_side_pickup");
        animator.pickupByDir[DirUp] = LoadPair(spriteBase + "_up_pickup");
        return animator;
    }

    private static Sprite[] LoadSet(string spriteBase)
    {
        Sprite idle = Resources.Load<Sprite>("Sprites/" + spriteBase);
        Sprite walk1 = Resources.Load<Sprite>("Sprites/" + spriteBase + "_walk_1");
        Sprite walk2 = Resources.Load<Sprite>("Sprites/" + spriteBase + "_walk_2");
        if (idle == null || walk1 == null || walk2 == null) return null;
        return new[] { idle, walk1, walk2 };
    }

    private static Sprite[] LoadPair(string spriteBase)
    {
        Sprite s1 = Resources.Load<Sprite>("Sprites/" + spriteBase + "_1");
        Sprite s2 = Resources.Load<Sprite>("Sprites/" + spriteBase + "_2");
        if (s1 == null || s2 == null) return null;
        return new[] { s1, s2 };
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

        if (delta.sqrMagnitude > 0.0000001f)
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

        // Откат на фронтальный сет, если для текущего ракурса нет арта.
        int useDir = cycleByDir[dir] != null ? dir : DirDown;
        // Профильный арт смотрит влево, вправо — через flipX. Если профильного
        // арта нет, сохраняем старое поведение: отражаем фронтальный кадр.
        spriteRenderer.flipX = dir == DirSide && faceRight;

        if (IsPickingUp)
        {
            Sprite[] pickup = pickupByDir[dir] ?? pickupByDir[DirDown];
            if (pickup != null)
            {
                // присел (1) — дотянулся (2) — выпрямляется (1)
                float t = 1f - (pickupUntil - Time.time) / pickupDuration;
                spriteRenderer.sprite = t < 0.3f ? pickup[0]
                    : t < 0.75f ? pickup[1] : pickup[0];
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
