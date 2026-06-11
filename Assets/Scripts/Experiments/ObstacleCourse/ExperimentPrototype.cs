using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ExperimentPhase
{
    Intro,
    Running,
    Execution,
    ImplantChoice,
    ImplantTest,
    Failed,
}

/// <summary>
/// Эксперимент 01: гонка на выживание (категория «каждый сам за себя»).
///
/// Бежит N заключённых-ботов (число берётся из состояния игры,
/// <see cref="RunState.RaceParticipants"/>). Боты несовершенно объезжают препятствия и
/// ЕСТЕСТВЕННО, недетерминированно падают в разные ямы (случайный дрейф полос + конечная
/// реакция уклонения), застревая там. Игрок может спасти ЛЮБОГО застрявшего бота по
/// желанию (мини-игра «вытащить из ямы», маш A/D), нет привязки к конкретному NPC.
///
/// Боты получают расположение из отношений (фундамент репутации): враждебные мешают,
/// дружелюбные помогают. Толчок (Space) сбивает бота и злит его. Результат и изменения
/// отношений уходят через общий контракт (RunState).
/// </summary>
public class ExperimentPrototype : MonoBehaviour
{
    private const string ExperimentId = "experiment.obstacle-course";
    private const float TrackHalfWidth = 5.5f;
    private const float StartY = 0f;
    private const float FinishY = 120f;
    private const float CellSize = 1f;
    private const float HazardRadius = 0.6f;

    private const float PlayerPushRange = 1.3f;
    private const float PlayerPushKnockback = 1.7f;
    private const float PlayerPushCooldown = 0.9f;

    private static readonly Color[] GenericColors =
    {
        new(0.70f, 0.45f, 0.85f),
        new(0.40f, 0.75f, 0.80f),
        new(0.80f, 0.70f, 0.35f),
        new(0.55f, 0.80f, 0.45f),
    };

    [Header("Prototype Balance")]
    [SerializeField] private float raceDuration = 180f;
    [SerializeField] private float playerSpeed = 5.5f;
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashCooldown = 1.2f;
    [Tooltip("Рост отношений к спасённому NPC.")]
    [SerializeField] private int gratitudeReward = 2;

    private readonly List<RaceObstacle> obstacles = new();
    private readonly List<ExperimentRunner> bots = new();

    private ExperimentRunner player;
    private Transform guard;
    private Camera cam;
    private ExperimentContext context;

    private ExperimentPhase phase = ExperimentPhase.Intro;
    private float remainingTime;
    private float rockTimer;
    private float dashReadyAt;
    private float introPage;
    private bool playerFinished;
    private bool implantAccepted;
    private bool resultSubmitted;
    private float playerPushReadyAt;

    // Мини-игра спасения «вытащить из ямы».
    private bool ropeActive;
    private float ropeMeter;
    private int ropeLastKey = -1;          // 0 = A, 1 = D
    private float ropeFlashUntil;
    private ExperimentRunner rescueTarget;
    private ExperimentRunner adjacentStuckBot;

    private string executionText = "";
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;

    public ExperimentPhase Phase => phase;

    private void Awake()
    {
        playerSpeed = Mathf.Max(playerSpeed, 5.5f);
        context = RunState.BuildContext(ExperimentId);
        implantAccepted = context.HasImplant(ImplantId.ReactiveFeet);
        BuildWorld();
        remainingTime = raceDuration;
    }

    private void Update()
    {
        HandleGlobalInput();

        if (phase == ExperimentPhase.Running) UpdateRace();
        else if (phase == ExperimentPhase.ImplantTest) UpdateImplantTest();

        FollowPlayer();
    }

    private void HandleGlobalInput()
    {
        if (Keyboard.current == null) return;

        if (phase == ExperimentPhase.Intro && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            introPage++;
            if (introPage >= 2)
            {
                phase = ExperimentPhase.Running;
                playerPushReadyAt = Time.time + 0.4f;
            }
        }

        if (phase == ExperimentPhase.ImplantTest && Keyboard.current.eKey.wasPressedThisFrame)
            RunState.ReturnToPrison();

        if ((phase == ExperimentPhase.Failed || phase == ExperimentPhase.ImplantTest) &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private void UpdateRace()
    {
        remainingTime -= Time.deltaTime;

        UpdatePlayerMovement(implantAccepted);
        HandlePush();
        foreach (ExperimentRunner bot in bots) UpdateBot(bot);
        SpawnRocks();
        TickObstacles();
        CheckHazardContacts();
        UpdateRescues();
        UpdateStuckRecovery();
        UpdateFinishStates();

        // Завершаемся по таймеру (опоздавшие гибнут) или когда ВСЕ уже финишировали.
        // Финиш игрока сам по себе никого не убивает — у ботов есть время до конца таймера.
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            if (!playerFinished)
            {
                phase = ExperimentPhase.Failed;
                SubmitResult(survived: false, won: false, offeredImplant: false);
                return;
            }
            StartCoroutine(ResolveRace());
        }
        else if (playerFinished && AllBotsFinished())
        {
            StartCoroutine(ResolveRace());
        }
    }

    private bool AllBotsFinished()
    {
        foreach (ExperimentRunner bot in bots)
        {
            if (bot != null && bot.gameObject.activeSelf && !bot.Finished) return false;
        }
        return true;
    }

    // ---- Игрок ----

    private void UpdatePlayerMovement(bool allowDash)
    {
        if (player == null || playerFinished || player.IsStunned || ropeActive) return;
        if (Keyboard.current == null) return;

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 pos = player.transform.position;
        Vector3 delta = (Vector3)(input * playerSpeed * Time.deltaTime);
        player.transform.position = SlideMove(pos, delta);

        if (allowDash && implantAccepted && Keyboard.current.qKey.wasPressedThisFrame && Time.time >= dashReadyAt)
        {
            Vector2 direction = input.sqrMagnitude > 0.1f ? input.normalized : Vector2.up;
            player.transform.position = SlideMove(player.transform.position, (Vector3)(direction * dashDistance));
            dashReadyAt = Time.time + dashCooldown;
        }
    }

    /// <summary>Сдвиг игрока со скольжением вдоль ям (игрок их не падает, а обходит).</summary>
    private Vector3 SlideMove(Vector3 pos, Vector3 delta)
    {
        Vector3 full = ClampToTrack(pos + delta);
        if (!IsBlocked(full)) return full;

        Vector3 onlyX = ClampToTrack(pos + new Vector3(delta.x, 0f, 0f));
        if (!Mathf.Approximately(delta.x, 0f) && !IsBlocked(onlyX)) return onlyX;

        Vector3 onlyY = ClampToTrack(pos + new Vector3(0f, delta.y, 0f));
        if (!Mathf.Approximately(delta.y, 0f) && !IsBlocked(onlyY)) return onlyY;

        return pos;
    }

    // ---- Боты ----

    private void UpdateBot(ExperimentRunner bot)
    {
        if (bot == null || !bot.gameObject.activeSelf || bot.Finished || bot.IsStunned || bot.Stuck) return;

        Vector3 pos = bot.transform.position;
        bool aggressive = bot.Disposition == NpcDisposition.Hostile || bot.Aggro > 0.4f;
        bool friendly = bot.IsNamed && bot.Disposition == NpcDisposition.Friendly && bot.Aggro <= 0.4f;

        // Случайный дрейф полосы — источник недетерминированности.
        if (Time.time >= bot.WanderUntil)
        {
            bot.WanderX = Random.Range(-TrackHalfWidth + 0.6f, TrackHalfWidth - 0.6f);
            bot.WanderUntil = Time.time + Random.Range(1.2f, 3f);
        }

        float targetX = bot.WanderX;
        if (player != null && !playerFinished)
        {
            float dy = player.transform.position.y - pos.y;
            if (aggressive && dy > -1f && dy < 8f)
                targetX = player.transform.position.x;
            else if (friendly && Mathf.Abs(dy) < 3f && Mathf.Abs(player.transform.position.x - pos.x) < 1.5f)
                targetX = pos.x >= player.transform.position.x ? pos.x + 2.5f : pos.x - 2.5f;
        }

        // Стиринг (boids): к финишу + к выбранной полосе + избегание препятствий + сепарация.
        Vector2 steer = Vector2.up;
        steer += Vector2.right * Mathf.Clamp(targetX - pos.x, -1.2f, 1.2f);
        steer += AvoidanceField(pos) * (1.6f + bot.Traits.Caution);
        steer += SeparateFromBots(bot, pos) * 0.9f;

        Vector2 dir = steer.sqrMagnitude > 0.0001f ? steer.normalized : Vector2.up;
        Vector3 next = ClampToTrack(pos + (Vector3)(dir * bot.Speed * Time.deltaTime));

        if (IsBlocked(next))
        {
            // Правило (яма) + характеристика (skill·caution) + общий анлак: поймает ли себя.
            if (Luck.Roll(bot.Traits.Skill * (0.5f + 0.5f * bot.Traits.Caution)))
            {
                Vector2 away = AvoidanceField(pos);
                if (away.sqrMagnitude > 0.0001f)
                    next = ClampToTrack(pos + (Vector3)(away.normalized * bot.Speed * Time.deltaTime));
                if (IsBlocked(next)) next = pos;     // и сбоку яма — переждать кадр
            }
            else
            {
                BeginBotFall(bot, WorldToCell(next));
                return;
            }
        }

        bot.transform.position = next;
        BotInteract(bot, aggressive, friendly);
    }

    /// <summary>Суммарное отталкивание от препятствий впереди (формальные правила в данных).</summary>
    private Vector2 AvoidanceField(Vector3 pos)
    {
        Vector2 force = Vector2.zero;
        Vector3 probe = pos + Vector3.up * 0.7f;
        foreach (RaceObstacle obstacle in obstacles) force += obstacle.AvoidanceForce(probe, 2.4f);
        return force;
    }

    /// <summary>Сепарация от других ботов, чтобы не слипались (boids).</summary>
    private Vector2 SeparateFromBots(ExperimentRunner self, Vector3 pos)
    {
        Vector2 force = Vector2.zero;
        foreach (ExperimentRunner other in bots)
        {
            if (other == self || other == null || !other.gameObject.activeSelf || other.Stuck) continue;
            Vector2 d = (Vector2)(pos - other.transform.position);
            float dist = d.magnitude;
            if (dist > 0.01f && dist < 1.2f) force += d / dist * (1.2f - dist);
        }
        return force;
    }

    private void BeginBotFall(ExperimentRunner bot, Vector2Int cell)
    {
        bot.Stuck = true;
        bot.StuckCell = cell;
        bot.SelfFreeAt = Time.time + Random.Range(5f, 8f); // сам выберется, если не помочь раньше
        bot.transform.position = CellCenter(cell);
        bot.SetColor(new Color(1f, 0.55f, 0.12f));
    }

    /// <summary>Застрявшие боты со временем сами выбираются и продолжают гонку.</summary>
    private void UpdateStuckRecovery()
    {
        foreach (ExperimentRunner bot in bots)
        {
            if (bot == null || !bot.Stuck) continue;
            if (ropeActive && bot == rescueTarget) continue;
            if (Time.time >= bot.SelfFreeAt)
            {
                bot.Stuck = false;
                bot.transform.position += Vector3.up * 1.1f;
                bot.RestoreColor();
            }
        }
    }

    /// <summary>Враждебный бот толкает игрока; дружелюбный отгоняет от него агрессоров.</summary>
    private void BotInteract(ExperimentRunner bot, bool aggressive, bool friendly)
    {
        if (player == null || Time.time < bot.PushReadyAt) return;

        if (aggressive && !playerFinished && !player.IsStunned && !ropeActive &&
            Vector2.Distance(bot.transform.position, player.transform.position) <= 1.1f)
        {
            PushEntity(player, bot.transform.position, 1.3f);
            player.Stun(0.45f);
            bot.PushReadyAt = Time.time + 1.6f;
            return;
        }

        if (!friendly) return;
        foreach (ExperimentRunner other in bots)
        {
            if (other == bot || other == null || !other.gameObject.activeSelf || other.Finished || other.Stuck)
                continue;
            bool otherAggressive = other.Disposition == NpcDisposition.Hostile || other.Aggro > 0.4f;
            if (otherAggressive &&
                Vector2.Distance(other.transform.position, player.transform.position) < 1.4f &&
                Vector2.Distance(bot.transform.position, other.transform.position) < 1.7f)
            {
                PushEntity(other, bot.transform.position, 1.4f);
                other.Stun(0.4f);
                bot.PushReadyAt = Time.time + 2f;
                return;
            }
        }
    }

    // ---- Толчки игрока ----

    private void HandlePush()
    {
        if (player == null || playerFinished || player.IsStunned || ropeActive) return;
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;
        if (Time.time < playerPushReadyAt) return;

        playerPushReadyAt = Time.time + PlayerPushCooldown;
        foreach (ExperimentRunner bot in bots) TryPushBot(bot);
    }

    private void TryPushBot(ExperimentRunner bot)
    {
        if (bot == null || !bot.gameObject.activeSelf || bot.Finished || bot.Stuck) return;
        if (Vector2.Distance(bot.transform.position, player.transform.position) > PlayerPushRange) return;

        PushEntity(bot, player.transform.position, PlayerPushKnockback);
        bot.Stun(0.6f);
        bot.Aggro = Mathf.Min(1f, bot.Aggro + 0.6f);
        bot.PushedByPlayer = true;
    }

    private void PushEntity(ExperimentRunner target, Vector3 from, float distance)
    {
        Vector3 dir = target.transform.position - from;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
        dir.Normalize();
        Vector3 dest = ClampToTrack(target.transform.position + dir * distance);
        if (!IsBlocked(dest)) target.transform.position = dest;
        target.Flash(new Color(0.95f, 0.55f, 0.2f), 0.35f);
    }

    // ---- Спасение любого застрявшего бота ----

    private void UpdateRescues()
    {
        adjacentStuckBot = null;

        if (ropeActive)
        {
            if (rescueTarget == null || !rescueTarget.Stuck)
            {
                ropeActive = false;
                return;
            }
            if (!IsCardinallyAdjacent(WorldToCell(player.transform.position), rescueTarget.StuckCell))
            {
                ropeActive = false; // отошёл — бросил
                return;
            }

            ropeMeter = Mathf.Max(0f, ropeMeter - 0.18f * Time.deltaTime);

            int pressed = -1;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.wasPressedThisFrame) pressed = 0;
                else if (Keyboard.current.dKey.wasPressedThisFrame) pressed = 1;
            }
            if (pressed >= 0 && pressed != ropeLastKey)
            {
                ropeMeter += 0.12f;
                ropeLastKey = pressed;
                ropeFlashUntil = Time.time + 0.12f;
            }

            if (ropeMeter >= 1f)
            {
                rescueTarget.Stuck = false;
                rescueTarget.RescuedByPlayer = true;
                rescueTarget.SetColor(new Color(0.3f, 0.85f, 0.45f));
                rescueTarget.transform.position += Vector3.up * 1.1f;
                ropeActive = false;
            }
            return;
        }

        foreach (ExperimentRunner bot in bots)
        {
            if (bot == null || !bot.Stuck) continue;
            if (IsCardinallyAdjacent(WorldToCell(player.transform.position), bot.StuckCell))
            {
                adjacentStuckBot = bot;
                bot.RescueEncountered = true;
                break;
            }
        }

        if (adjacentStuckBot != null && !playerFinished && !player.IsStunned &&
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            ropeActive = true;
            ropeMeter = 0f;
            ropeLastKey = -1;
            rescueTarget = adjacentStuckBot;
        }
    }

    // ---- Препятствия ----

    private void SpawnRocks()
    {
        rockTimer -= Time.deltaTime;
        if (rockTimer > 0f) return;
        float t = Mathf.Clamp01(player.transform.position.y / FinishY);
        rockTimer = ropeActive ? 1.4f : Mathf.Lerp(2.8f, 1.1f, t); // выше — камни чаще

        int column = Random.Range(-5, 6);
        obstacles.Add(new RollingRockObstacle(
            new Vector2(column, FinishY + 2f), 2.6f, StartY - 3f, CellSize * 0.92f));
    }

    private void TickObstacles()
    {
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            obstacles[i].Tick(Time.deltaTime);
            if (obstacles[i].Expired)
            {
                obstacles[i].DestroyVisual();
                obstacles.RemoveAt(i);
            }
        }
    }

    private void CheckHazardContacts()
    {
        StunIfHit(player);
        foreach (ExperimentRunner bot in bots)
        {
            if (!bot.Stuck) StunIfHit(bot);
        }
    }

    private void StunIfHit(ExperimentRunner runner)
    {
        if (runner == null || !runner.gameObject.activeSelf || runner.Finished || runner.IsStunned) return;
        foreach (RaceObstacle obstacle in obstacles)
        {
            if (obstacle.HitsEntity(runner.transform.position, HazardRadius))
            {
                runner.Stun(0.7f);
                runner.Flash(new Color(0.18f, 0.12f, 0.08f), 0.7f);
                break;
            }
        }
    }

    private bool IsBlocked(Vector3 worldPos)
    {
        foreach (RaceObstacle obstacle in obstacles)
        {
            if (obstacle.BlocksMovement(worldPos)) return true;
        }
        return false;
    }

    private Vector3 ClampToTrack(Vector3 p)
    {
        p.x = Mathf.Clamp(p.x, -TrackHalfWidth + 0.4f, TrackHalfWidth - 0.4f);
        p.y = Mathf.Clamp(p.y, StartY, FinishY + 6f);
        return p;
    }

    // ---- Финиш / завершение ----

    private void UpdateFinishStates()
    {
        if (!playerFinished && player.transform.position.y >= FinishY)
        {
            playerFinished = true;
            player.MarkFinished(raceDuration - remainingTime);
            player.SetColor(new Color(0.25f, 0.9f, 1f));
        }

        foreach (ExperimentRunner bot in bots)
        {
            if (bot == null || !bot.gameObject.activeSelf || bot.Finished || bot.Stuck) continue;
            if (bot.transform.position.y >= FinishY) bot.MarkFinished(raceDuration - remainingTime);
        }
    }

    private IEnumerator ResolveRace()
    {
        if (phase != ExperimentPhase.Running) yield break;
        phase = ExperimentPhase.Execution;

        int losers = 0;
        ExperimentRunner show = null;
        foreach (ExperimentRunner bot in bots)
        {
            if (bot == null || !bot.gameObject.activeSelf || bot.Finished) continue;
            bot.Stuck = false;
            losers++;
            if (show == null) show = bot;
        }

        if (show != null)
        {
            show.transform.position = new Vector3(0f, FinishY - 3f, 0f);
            executionText = losers > 1
                ? $"Надзиратель: {losers} заключённых не прошли испытание."
                : $"Надзиратель: Заключённый {show.DisplayName} не прошёл испытание.";
            guard.gameObject.SetActive(true);
            guard.position = show.transform.position + Vector3.right * 2f;
            yield return new WaitForSeconds(2f);
            executionText = "Выстрел.";
            show.SetColor(new Color(0.25f, 0.05f, 0.05f));
            yield return new WaitForSeconds(1.5f);
        }

        if (!playerFinished)
        {
            phase = ExperimentPhase.Failed;
            SubmitResult(survived: false, won: false, offeredImplant: false);
            yield break;
        }

        bool playerWon = true;
        foreach (ExperimentRunner bot in bots)
        {
            if (bot != null && bot.Finished && bot.FinishTime < player.FinishTime) playerWon = false;
        }
        phase = playerWon ? ExperimentPhase.ImplantChoice : ExperimentPhase.ImplantTest;
        if (!playerWon) SubmitResult(survived: true, won: false, offeredImplant: false);
    }

    private void UpdateImplantTest()
    {
        playerFinished = false;
        UpdatePlayerMovement(true);
    }

    private void AcceptImplant(bool accepted)
    {
        if (accepted) RunState.HasReactiveFeet = true;
        implantAccepted = RunState.HasReactiveFeet;
        phase = ExperimentPhase.ImplantTest;
        player.transform.position = new Vector3(0f, FinishY + 2f, 0f);
        SubmitResult(survived: true, won: true, offeredImplant: true);
    }

    private void SubmitResult(bool survived, bool won, bool offeredImplant)
    {
        if (resultSubmitted) return;
        resultSubmitted = true;

        var result = new ExperimentResult
        {
            ExperimentId = ExperimentId,
            PlayerSurvived = survived,
            PlayerWon = won,
            ImplantAccepted = offeredImplant && RunState.HasReactiveFeet,
            OfferedImplant = offeredImplant ? ImplantId.ReactiveFeet : (ImplantId?)null,
        };

        bool helpedAnyone = false;
        foreach (ExperimentRunner bot in bots)
        {
            if (bot == null) continue;
            bool botSurvived = bot.Finished || bot.RescuedByPlayer;
            NpcAction action = bot.RescuedByPlayer ? NpcAction.Helped
                : bot.PushedByPlayer ? NpcAction.Harmed
                : bot.RescueEncountered ? NpcAction.Ignored
                : NpcAction.None;
            if (bot.RescuedByPlayer) helpedAnyone = true;

            if (bot.IsNamed)
            {
                result.Record(bot.Id, botSurvived, action);
                if (bot.RescuedByPlayer)
                    result.RelationshipDeltas[bot.Id] = gratitudeReward;
                else if (bot.PushedByPlayer)
                    result.RelationshipDeltas[bot.Id] =
                        (result.RelationshipDeltas.TryGetValue(bot.Id, out int d) ? d : 0) - 2;
            }
        }
        if (helpedAnyone) result.Flags.Add("obstacle-course.helped-someone");

        RunState.SubmitResult(result);
    }

    // ---- Мир ----

    private void FollowPlayer()
    {
        if (cam == null || player == null) return;
        Vector3 target = new(0f, Mathf.Clamp(player.transform.position.y + 3f, 6f, FinishY), -10f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime * 4f);
    }

    private void BuildWorld()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cam = cameraObject.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.19f, 0.30f, 0.47f);

        CreateCourseVisuals();
        RaceVisuals.Square("Start", new Vector2(0f, StartY), new Vector2(12f, 0.25f), Color.white, -5);
        RaceVisuals.Square("Finish", new Vector2(0f, FinishY), new Vector2(12f, 0.35f),
            new Color(0.2f, 1f, 0.35f), -5);

        GenerateCourse();

        player = CreateRunner("Игрок", new Vector2(0f, 0.8f), new Color(0.2f, 0.65f, 1f));

        SpawnBots();

        guard = CreateRunner("Надзиратель", new Vector2(0f, -10f), new Color(0.1f, 0.1f, 0.1f)).transform;
        guard.gameObject.SetActive(false);
    }

    private void SpawnBots()
    {
        int count = Mathf.Clamp(RunState.RaceParticipants, 1, 6);
        for (int i = 0; i < count; i++)
        {
            float startX = Random.Range(-TrackHalfWidth + 1f, TrackHalfWidth - 1f);
            ExperimentRunner bot;
            if (i == 0)
            {
                bot = CreateRunner("Программист", new Vector2(startX, 0.8f), new Color(1f, 0.65f, 0.15f));
                bot.SetSocial(NpcId.Programmer, Disposition.For(context.RelationshipTo(NpcId.Programmer)), true);
            }
            else if (i == 1)
            {
                bot = CreateRunner("Заключённая 2", new Vector2(startX, 0.8f), new Color(0.9f, 0.25f, 0.65f));
                bot.SetSocial(NpcId.Competitor, Disposition.For(context.RelationshipTo(NpcId.Competitor)), true);
            }
            else
            {
                Color c = GenericColors[(i - 2) % GenericColors.Length];
                bot = CreateRunner($"Заключённый {i + 1}", new Vector2(startX, 0.8f), c);
                bot.SetSocial(default, NpcDisposition.Neutral, false);
            }

            bot.Speed = Random.Range(4.6f, 5.3f);
            bot.Traits = BotTraits.Randomized();
            bot.WanderX = startX;
            bots.Add(bot);
        }
    }

    private ExperimentRunner CreateRunner(string displayName, Vector2 position, Color color)
    {
        GameObject go = RaceVisuals.Circle(displayName, position, 0.8f, color, 5);
        ExperimentRunner runner = go.AddComponent<ExperimentRunner>();
        runner.Initialize(displayName, color);
        return runner;
    }

    private void CreateCourseVisuals()
    {
        Color floorA = new(0.60f, 0.50f, 0.40f);
        Color floorB = new(0.56f, 0.46f, 0.36f);
        Color wallTop = new(0.50f, 0.35f, 0.20f);
        Color wallSide = new(0.35f, 0.25f, 0.15f);

        for (int y = -1; y <= FinishY + 2; y++)
        {
            for (int x = -5; x <= 5; x++)
            {
                Color floorColor = (x + y) % 2 == 0 ? floorA : floorB;
                RaceVisuals.Square("Floor", new Vector2(x, y), Vector2.one * 0.97f, floorColor, -20);
            }

            RaceVisuals.Square("Left Wall Top", new Vector2(-6f, y), Vector2.one * 0.97f, wallTop, -8);
            RaceVisuals.Square("Right Wall Top", new Vector2(6f, y), Vector2.one * 0.97f, wallTop, -8);
            RaceVisuals.Square("Left Wall Side", new Vector2(-5.72f, y - 0.18f), new Vector2(0.42f, 0.55f),
                wallSide, -7);
            RaceVisuals.Square("Right Wall Side", new Vector2(5.72f, y - 0.18f), new Vector2(0.42f, 0.55f),
                wallSide, -7);
        }
    }

    private void AddPit(Vector2Int cell) => obstacles.Add(new PitObstacle(cell, CellSize));

    /// <summary>
    /// Процедурная трасса с постепенным усложнением: чем выше к финишу, тем плотнее ямы
    /// и чаще/быстрее скользящие пилы. Старт и финиш оставлены чистыми.
    /// </summary>
    private void GenerateCourse()
    {
        float y = 10f;
        while (y < FinishY - 5f)
        {
            float t = y / FinishY; // 0 у старта → 1 у финиша
            AddPit(new Vector2Int(Random.Range(-5, 6), Mathf.RoundToInt(y)));
            if (Random.value < t) // во второй половине чаще встречается вторая яма в ряду
                AddPit(new Vector2Int(Random.Range(-5, 6), Mathf.RoundToInt(y)));
            y += Mathf.Lerp(6.5f, 2.8f, t); // промежуток сокращается к финишу
        }

        for (float sy = 18f; sy < FinishY - 5f; sy += Random.Range(9f, 16f))
        {
            float t = sy / FinishY;
            if (Random.value < 0.35f + t * 0.5f)
                obstacles.Add(new SlidingSawObstacle(sy, -4.5f, 4.5f, Mathf.Lerp(4f, 7.5f, t), CellSize * 0.95f));
        }
    }

    private static Vector2Int WorldToCell(Vector3 position)
        => new(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));

    private static Vector2 CellCenter(Vector2Int cell) => new(cell.x, cell.y);

    private static bool IsCardinallyAdjacent(Vector2Int first, Vector2Int second)
    {
        Vector2Int delta = first - second;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 1;
    }

    // ---- UI ----

    private void OnGUI()
    {
        EnsureGuiStyles();

        if (phase == ExperimentPhase.Intro)
        {
            string text = introPage < 1
                ? "Программист: Я не справлюсь один. Если что-то случится... я рассчитываю на тебя."
                : "Администрация: Финишируйте до окончания таймера. Опоздавшие будут ликвидированы.";
            DrawDialog(text, "SPACE — продолжить");
            return;
        }

        if (phase == ExperimentPhase.Running)
        {
            GUI.Box(new Rect(12, 12, 410, 78), "");
            GUI.Label(new Rect(28, 18, 380, 32), $"Время: {FormatTime(remainingTime)}", titleStyle);
            GUI.Label(new Rect(28, 52, 380, 26), "Финиш наверху · Space — толкнуть", bodyStyle);

            if (ropeActive && rescueTarget != null)
            {
                GUI.Box(new Rect(Screen.width / 2f - 240, 100, 480, 64), "");
                GUI.Label(new Rect(Screen.width / 2f - 220, 112, 440, 44),
                    $"Тяни {rescueTarget.DisplayName}! Жми A и D по очереди!", bodyStyle);
                DrawRopeMeter();
            }
            else if (adjacentStuckBot != null)
            {
                GUI.Box(new Rect(Screen.width / 2f - 240, 100, 480, 56), "");
                GUI.Label(new Rect(Screen.width / 2f - 220, 112, 440, 36),
                    $"{adjacentStuckBot.DisplayName} застрял в яме! E — спасти.", bodyStyle);
            }
            return;
        }

        if (phase == ExperimentPhase.Execution)
        {
            DrawDialog(executionText, "");
            return;
        }

        if (phase == ExperimentPhase.ImplantChoice)
        {
            GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height / 2f - 150, 520, 300), "");
            GUI.Label(new Rect(Screen.width / 2f - 225, Screen.height / 2f - 120, 450, 50),
                "Награда: реактивные стопы", titleStyle);
            GUI.Label(new Rect(Screen.width / 2f - 225, Screen.height / 2f - 65, 450, 90),
                "Имплант даёт рывок по Q.\nПрограммист осуждает сотрудничество с администрацией.", bodyStyle);
            if (GUI.Button(new Rect(Screen.width / 2f - 210, Screen.height / 2f + 45, 190, 55), "Принять", buttonStyle))
                AcceptImplant(true);
            if (GUI.Button(new Rect(Screen.width / 2f + 20, Screen.height / 2f + 45, 190, 55), "Отказаться", buttonStyle))
                AcceptImplant(false);
            return;
        }

        if (phase == ExperimentPhase.ImplantTest)
        {
            string implant = implantAccepted ? "Q — использовать реактивные стопы" : "Имплант отклонён";
            GUI.Label(new Rect(20, 15, 600, 45), implant, titleStyle);
            GUI.Label(new Rect(20, 60, 700, 35), "E — вернуться в тюрьму, R — повторить эксперимент", bodyStyle);
            return;
        }

        if (phase == ExperimentPhase.Failed)
            DrawDialog("Игрок не успел. Забег завершён.", "R — начать заново");
    }

    private void DrawRopeMeter()
    {
        Rect box = new(Screen.width / 2f - 150, Screen.height - 150, 300, 90);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 20, box.y + 12, 260, 26), "Вытащить из ямы", bodyStyle);

        Color prev = GUI.color;
        GUI.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        GUI.DrawTexture(new Rect(box.x + 20, box.y + 48, 260, 22), Texture2D.whiteTexture);
        GUI.color = Time.time < ropeFlashUntil ? Color.green : new Color(0.95f, 0.75f, 0.2f);
        GUI.DrawTexture(new Rect(box.x + 20, box.y + 48, 260 * Mathf.Clamp01(ropeMeter), 22), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private void DrawDialog(string text, string hint)
    {
        Rect box = new(40, Screen.height - 180, Screen.width - 80, 140);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 25, box.y + 20, box.width - 50, 70), text, bodyStyle);
        GUI.Label(new Rect(box.x + 25, box.y + 95, box.width - 50, 30), hint, bodyStyle);
    }

    private void EnsureGuiStyles()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        bodyStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 20 };
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}

/// <summary>Бегун гонки: позиция, финиш, оглушение, социальное состояние, падение в яму.</summary>
public class ExperimentRunner : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Color originalColor = Color.white;
    private float stunnedUntil;
    private float flashUntil;
    private Color flashColor;

    public string DisplayName { get; private set; }
    public bool Finished { get; private set; }
    public float FinishTime { get; private set; } = float.MaxValue;
    public bool IsStunned => Time.time < stunnedUntil;

    // Социальное состояние.
    public NpcId Id { get; private set; }
    public NpcDisposition Disposition { get; private set; }
    public bool IsNamed { get; private set; }
    public float Aggro;
    public float PushReadyAt;
    public bool PushedByPlayer;

    // Бег и падение.
    public float Speed = 5f;
    public BotTraits Traits;
    public float WanderX;
    public float WanderUntil;
    public bool Stuck;
    public Vector2Int StuckCell;
    public float SelfFreeAt;
    public bool RescuedByPlayer;
    public bool RescueEncountered;

    public void Initialize(string displayName, Color color)
    {
        DisplayName = displayName;
        baseColor = color;
        originalColor = color;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void RestoreColor()
    {
        baseColor = originalColor;
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
    }

    public void SetSocial(NpcId id, NpcDisposition disposition, bool isNamed)
    {
        Id = id;
        Disposition = disposition;
        IsNamed = isNamed;
    }

    private void Update()
    {
        if (Aggro > 0f) Aggro = Mathf.Max(0f, Aggro - Time.deltaTime * 0.12f);
        if (spriteRenderer == null) return;
        spriteRenderer.color = Time.time < flashUntil ? flashColor : baseColor;
    }

    public void Stun(float duration) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);

    public void Flash(Color color, float duration)
    {
        flashColor = color;
        flashUntil = Time.time + duration;
    }

    public void MarkFinished(float finishTime)
    {
        Finished = true;
        FinishTime = finishTime;
    }

    public void SetColor(Color color)
    {
        baseColor = color;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.color = color;
    }
}
