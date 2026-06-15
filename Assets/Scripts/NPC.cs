using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC - простое автономное движение по гриду
/// </summary>
public class NPC : MonoBehaviour
{
    [Header("Sprite (оставь пустым если используешь Animator)")]
    [SerializeField] private Sprite npcSprite;

    [Header("Visual Settings")]
    [SerializeField] private Color npcColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private float moveSpeed = 8f;

    [Header("AI Settings")]
    [SerializeField] private bool enableMovement = false;
    [SerializeField] private float moveInterval = 0.6f;

    [Header("Interaction Hint")]
    [SerializeField] private float hintHeight = 0.9f;
    [SerializeField] private float hintRange = 1.6f;
    [SerializeField] private string hintText = "E";

    // Текущая позиция на гриде
    private int gridX;
    private int gridY;

    // Целевая позиция для плавного движения
    private Vector3 targetPosition;
    private bool isMoving;

    // Направление движения (для анимаций)
    private Vector2 facingDirection = Vector2.right;

    // Ссылка на грид
    private GameGrid grid;

    // Компоненты
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private TextMesh hintLabel;
    private Player player;

    private float moveTimer;
    private string spriteResourceName = "inmate_c1752";

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    /// <summary>
    /// Инициализация NPC
    /// </summary>
    public void Initialize(GameGrid gameGrid, int startX, int startY)
    {
        grid = gameGrid;
        gridX = startX;
        gridY = startY;

        CreateVisual();
        CreateHint();

        targetPosition = grid.GridToWorld(gridX, gridY);
        transform.position = targetPosition;

        UpdateSortingOrder();
        moveTimer = moveInterval;
    }

    public void SetSpriteResource(string resourceName)
    {
        if (!string.IsNullOrEmpty(resourceName)) spriteResourceName = resourceName;
    }

    private void CreateVisual()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            transform.localScale = Vector3.one * WorldMetrics.CharacterScale;
            return;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // Пиксель-арт по умолчанию из Resources/Sprites, если не задан в инспекторе.
        if (npcSprite == null)
        {
            npcSprite = Resources.Load<Sprite>($"Sprites/{spriteResourceName}");
        }

        if (npcSprite != null)
        {
            spriteRenderer.sprite = SpriteWalkAnimator.FeetAnchored(npcSprite);
            spriteRenderer.color = Color.white;
            float spriteSize = Mathf.Max(npcSprite.bounds.size.x, npcSprite.bounds.size.y);
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale / spriteSize;
            // У C-1752 пока нет walk-кадров → аниматор не подключится, останется
            // статичная стойка (NPC всё равно стоит на месте у входа в эксперимент).
            SpriteWalkAnimator.TryAttach(gameObject, spriteResourceName);
        }
        else
        {
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = npcColor;
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale;
        }

        CharacterGroundShadow.Attach(gameObject);
        CharacterScreenFacing.Attach(gameObject);
    }

    private void CreateHint()
    {
        var hintObject = new GameObject("Hint");
        hintObject.transform.SetParent(transform);
        hintObject.transform.localPosition = new Vector3(0f, hintHeight, 0f);

        hintLabel = hintObject.AddComponent<TextMesh>();
        hintLabel.text = hintText;
        hintLabel.fontSize = 44;
        hintLabel.characterSize = 0.06f;
        hintLabel.anchor = TextAnchor.MiddleCenter;
        hintLabel.color = Color.white;
        hintLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintLabel.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Entity(transform.position.y) + 5;

        hintObject.SetActive(false);
    }

    private Sprite CreateCircleSprite()
    {
        int size = 64;
        var texture = new Texture2D(size, size);
        var pixels = new Color[size * size];

        float center = size / 2f;
        float radius = size / 2f - 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void Update()
    {
        UpdateMovement();
        UpdateAnimation();
        if (enableMovement)
        {
            HandleAI();
        }
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (hintLabel == null) return;

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.transform.position);
        bool shouldShow = distance <= hintRange;
        if (hintLabel.gameObject.activeSelf != shouldShow)
        {
            hintLabel.gameObject.SetActive(shouldShow);
        }
    }

    private void HandleAI()
    {
        if (grid == null) return;
        if (isMoving) return;

        moveTimer -= Time.deltaTime;
        if (moveTimer > 0f) return;
        moveTimer = moveInterval;

        int startIndex = Random.Range(0, Directions.Length);
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2Int dir = Directions[(startIndex + i) % Directions.Length];
            if (TryMove(dir.x, dir.y))
            {
                break;
            }
        }
    }

    private bool TryMove(int dx, int dy)
    {
        int newX = gridX + dx;
        int newY = gridY + dy;

        if (dx != 0) facingDirection = new Vector2(dx, 0);

        if (grid.IsWalkable(newX, newY))
        {
            gridX = newX;
            gridY = newY;
            targetPosition = grid.GridToWorld(gridX, gridY);
            isMoving = true;
            return true;
        }

        return false;
    }

    private void UpdateMovement()
    {
        if (!isMoving) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        UpdateSortingOrder();

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        }
        if (hintLabel != null)
        {
            hintLabel.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Entity(transform.position.y) + 5;
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        animator.SetInteger("AnimState", isMoving ? 2 : 0);

        if (facingDirection.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(WorldMetrics.CharacterScale), WorldMetrics.CharacterScale, 1);
        }
        else if (facingDirection.x < 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(WorldMetrics.CharacterScale), WorldMetrics.CharacterScale, 1);
        }
    }

    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

    public virtual void Interact()
    {
        // Выбираем эксперимент из пула (если он собран в Resources), иначе —
        // дефолт на полосу препятствий. Сам выбор — в ExperimentSelector.
        var pool = Resources.Load<ExperimentPool>("ExperimentPool");
        if (pool != null)
        {
            var played = new HashSet<string>(RunState.PlayedExperiments);
            ExperimentDefinition def = ExperimentSelector.Select(
                pool.Experiments, RunState.Day, RunState.ParticipantCount, played, new System.Random());
            if (def != null)
            {
                RunState.EnterExperiment(def);
                return;
            }
        }

        RunState.EnterExperiment();
    }
}

public sealed class ProgrammerNPC : NPC
{
    public override void Interact()
    {
        switch (RunState.ProgrammerQuest)
        {
            case ProgrammerQuestStage.NotStarted:
                ShowIntroduction();
                break;
            case ProgrammerQuestStage.Ignored:
                ShowSecondChance();
                break;
            case ProgrammerQuestStage.Accepted:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Передатчик должен быть в инженерной зоне. Отвёртка откроет повреждённую решётку в туалете.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.TransmitterAcquired:
                ShowCompletionStart();
                break;
            case ProgrammerQuestStage.Completed:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Я ещё разбираюсь с системой. И... спасибо, что вернулся.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.Rejected:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Понял. Не буду тебе мешать.",
                    "npc_programmer");
                break;
        }
    }

    private static void ShowIntroduction()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Ты новенький? Здесь людей заставляют участвовать в экспериментах. Я могу объяснить правила... и нам обоим пригодился бы друг.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Расспросить его о тюрьме", ShowDetails),
            new DialogueUI.DialogueChoice("Согласиться помогать друг другу", AcceptQuest),
            new DialogueUI.DialogueChoice("Отказаться помогать", Reject),
            new DialogueUI.DialogueChoice("Не разговаривать", Ignore));
    }

    private static void ShowSecondChance()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Извини, что снова лезу. Но одному здесь долго не протянуть.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Теперь выслушать его", ShowDetails),
            new DialogueUI.DialogueChoice("Согласиться помочь", AcceptQuest),
            new DialogueUI.DialogueChoice("Снова уйти", () =>
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Ладно... Я не буду тебя задерживать.",
                    "npc_programmer")));
    }

    private static void ShowDetails()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Система подбирает испытания под заключённых. Если достать передатчик из инженерной зоны, я попробую получать данные заранее. Камеры и скрытые механизмы можно увидеть только с глазным имплантом.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Договорились. Я помогу", AcceptQuest),
            new DialogueUI.DialogueChoice("Это слишком опасно", Reject),
            new DialogueUI.DialogueChoice("Мне нужно подумать", Ignore));
    }

    private static void AcceptQuest()
    {
        RunState.AcceptProgrammerQuest();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Возьми эту отвёртку. Ей можно открыть повреждённую решётку в туалете. Через вентиляцию ты попадёшь в служебную часть.\n\n<color=#75D99A>Отношения улучшились. Получена отвёртка.</color>",
            "npc_programmer");
    }

    private static void Ignore()
    {
        RunState.IgnoreProgrammer();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Ладно... Извини, что помешал.\n\n<color=#E0A070>Отношения немного ухудшились.</color>",
            "npc_programmer");
    }

    private static void Reject()
    {
        RunState.RejectProgrammerQuest();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Понимаю. Тогда забудь, что я это говорил.\n\n<color=#D66D63>Отношения ухудшились.</color>",
            "npc_programmer");
    }

    private static void ShowCompletionStart()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Ты вернулся... И передатчик у тебя. Честно говоря, я не был уверен, что снова тебя увижу.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Передать ему передатчик", CompleteQuest),
            new DialogueUI.DialogueChoice("Ты знал, что дверь заблокируется?", AskAboutLockedDoor),
            new DialogueUI.DialogueChoice("Сначала объясни, что теперь будет", AskAboutReward));
    }

    private static void AskAboutLockedDoor()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Я... знал, что инженерный отсек может закрыться. Но думал, что там должен быть аварийный выход. Если бы я сказал всё, ты мог отказаться. Прости.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Больше ничего от меня не скрывай", CompleteQuest),
            new DialogueUI.DialogueChoice("Ты мной воспользовался", CompleteQuestAngrily),
            new DialogueUI.DialogueChoice("Что ты сделаешь с передатчиком?", AskAboutReward));
    }

    private static void AskAboutReward()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Попробую подключиться к системе подбора экспериментов. Точных правил она не выдаст, но мы сможем заранее узнавать тип испытания, участников или главный риск.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Передать передатчик", CompleteQuest),
            new DialogueUI.DialogueChoice("Вернуться к вопросу о запертой двери", AskAboutLockedDoor));
    }

    private static void CompleteQuest()
    {
        RunState.CompleteProgrammerQuest();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Спасибо. Мне понадобится время, чтобы разобраться с защитой системы. Когда закончу, ты узнаешь первым.\n\n<color=#75D99A>Квест завершён. Отношения улучшились.</color>",
            "npc_programmer");
    }

    private static void CompleteQuestAngrily()
    {
        RunState.CompleteProgrammerQuest();
        RunState.AdjustRelationship(NpcId.Programmer, -1);
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Да. Ты прав. Я использовал тебя. Но передатчик всё равно поможет нам обоим. Я постараюсь это исправить.\n\n<color=#D6B06D>Квест завершён. Программист запомнил вашу реакцию.</color>",
            "npc_programmer");
    }
}
