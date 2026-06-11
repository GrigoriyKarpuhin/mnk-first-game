using UnityEngine;

/// <summary>
/// Лёгкая покадровая анимация ходьбы без Animator: следит за смещением
/// transform и сам переключает кадры idle/walk. Кадры берутся из
/// Resources/Sprites: "&lt;base&gt;", "&lt;base&gt;_walk_1", "&lt;base&gt;_walk_2".
/// Горизонтальное направление отражается через SpriteRenderer.flipX.
/// </summary>
public class SpriteWalkAnimator : MonoBehaviour
{
    private const float FrameTime = 0.12f;
    private const float MoveGrace = 0.1f; // сколько держать "идёт" после последнего сдвига

    private SpriteRenderer spriteRenderer;
    private Sprite idle;
    private Sprite[] cycle;
    private float timer;
    private int frame;
    private Vector3 lastPosition;
    private float movingUntil;

    /// <summary>
    /// Вешает аниматор на объект, если в Resources есть все кадры.
    /// Возвращает null, если кадров нет (объект остаётся со статичным спрайтом).
    /// </summary>
    public static SpriteWalkAnimator TryAttach(GameObject target, string spriteBase)
    {
        Sprite idleFrame = Resources.Load<Sprite>("Sprites/" + spriteBase);
        Sprite walk1 = Resources.Load<Sprite>("Sprites/" + spriteBase + "_walk_1");
        Sprite walk2 = Resources.Load<Sprite>("Sprites/" + spriteBase + "_walk_2");
        if (idleFrame == null || walk1 == null || walk2 == null) return null;

        var animator = target.GetComponent<SpriteWalkAnimator>();
        if (animator == null) animator = target.AddComponent<SpriteWalkAnimator>();
        animator.idle = idleFrame;
        animator.cycle = new[] { walk1, idleFrame, walk2, idleFrame };
        return animator;
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
        if (idle == null || cycle == null) return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        if (delta.sqrMagnitude > 0.0000001f)
        {
            movingUntil = Time.time + MoveGrace;
            if (Mathf.Abs(delta.x) > 0.0001f) spriteRenderer.flipX = delta.x > 0f;
        }

        if (Time.time < movingUntil)
        {
            timer += Time.deltaTime;
            if (timer >= FrameTime)
            {
                timer = 0f;
                frame = (frame + 1) % cycle.Length;
            }
            spriteRenderer.sprite = cycle[frame];
        }
        else
        {
            timer = 0f;
            frame = 0;
            spriteRenderer.sprite = idle;
        }
    }
}
