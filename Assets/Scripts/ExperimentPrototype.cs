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

public enum RescueOutcome
{
    NotEncountered,
    Ignored,
    Abandoned,
    Failed,
    Saved,
}

/// <summary>
/// Runtime-built obstacle-course prototype. It intentionally uses simple
/// shapes so playtests focus on decisions and timing instead of presentation.
/// </summary>
public class ExperimentPrototype : MonoBehaviour
{
    private const float TrackHalfWidth = 5.5f;
    private const float StartY = 0f;
    private const float FinishY = 60f;
    private const float CellSize = 1f;
    private static readonly Vector2Int RescuePitCell = new(-3, 39);

    [Header("Prototype Balance")]
    [SerializeField] private float raceDuration = 180f;
    [SerializeField] private float playerSpeed = 5.5f;
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashCooldown = 1.2f;

    private readonly List<Rect> pits = new();
    private readonly List<FallingRock> rocks = new();

    private ExperimentRunner player;
    private ExperimentRunner programmer;
    private ExperimentRunner competitor;
    private Transform guard;
    private Camera cam;

    private ExperimentPhase phase = ExperimentPhase.Intro;
    private RescueOutcome rescueOutcome = RescueOutcome.NotEncountered;
    private float remainingTime;
    private float rockTimer;
    private float dashReadyAt;
    private float introPage;
    private bool programmerHanging;
    private bool programmerFinished;
    private bool competitorFinished;
    private bool playerFinished;
    private bool implantAccepted;
    private bool qteActive;
    private int qteHits;
    private float qteAngle;
    private float qtePenalty;
    private bool playerHazardActive;
    private int playerHazardHits;
    private Vector3 playerHazardReturnPosition;
    private string playerHazardName = "";
    private string executionText = "";
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;

    public ExperimentPhase Phase => phase;
    public RescueOutcome RescueResult => rescueOutcome;

    private void Awake()
    {
        // Existing scenes may still contain the first prototype's serialized speed.
        playerSpeed = Mathf.Max(playerSpeed, 5.5f);
        BuildWorld();
        remainingTime = raceDuration;
    }

    private void Update()
    {
        HandleGlobalInput();

        if (phase == ExperimentPhase.Running)
        {
            UpdateRace();
        }
        else if (phase == ExperimentPhase.ImplantTest)
        {
            UpdateImplantTest();
        }

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
            }
        }

        if (phase == ExperimentPhase.ImplantTest && Keyboard.current.eKey.wasPressedThisFrame)
        {
            RunState.ReturnToPrison();
        }

        if ((phase == ExperimentPhase.Failed || phase == ExperimentPhase.ImplantTest) &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }

    private void UpdateRace()
    {
        remainingTime -= Time.deltaTime;
        UpdatePlayerMovement(RunState.HasReactiveFeet);
        UpdateNpc(programmer, 4.7f);
        UpdateNpc(competitor, 5f);
        UpdateRocks();
        UpdatePlayerHazard();
        UpdateRescue();
        UpdateFinishStates();

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            if (!playerFinished)
            {
                phase = ExperimentPhase.Failed;
                return;
            }

            StartCoroutine(ResolveRace());
        }
        else if (playerFinished && (competitorFinished || !competitor.gameObject.activeSelf) &&
                 (programmerFinished || programmer == null || !programmer.gameObject.activeSelf))
        {
            StartCoroutine(ResolveRace());
        }
    }

    private void UpdatePlayerMovement(bool allowDash)
    {
        if (player == null || playerFinished || player.IsStunned || playerHazardActive) return;
        if (Keyboard.current == null) return;

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;

        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 oldPosition = player.transform.position;
        Vector3 next = oldPosition + (Vector3)(input * playerSpeed * Time.deltaTime);
        next.x = Mathf.Clamp(next.x, -TrackHalfWidth + 0.4f, TrackHalfWidth - 0.4f);
        next.y = Mathf.Clamp(next.y, StartY, FinishY + 6f);

        if (!IsInsidePit(next))
        {
            player.transform.position = next;
        }
        else
        {
            player.transform.position = CellCenter(WorldToCell(next));
            BeginPlayerHazard("Падение в яму", oldPosition);
        }

        if (allowDash && implantAccepted && Keyboard.current.qKey.wasPressedThisFrame &&
            Time.time >= dashReadyAt)
        {
            Vector2 direction = input.sqrMagnitude > 0.1f ? input.normalized : Vector2.up;
            Vector3 dashTarget = player.transform.position + (Vector3)(direction * dashDistance);
            dashTarget.x = Mathf.Clamp(dashTarget.x, -TrackHalfWidth + 0.4f, TrackHalfWidth - 0.4f);
            dashTarget.y = Mathf.Clamp(dashTarget.y, StartY, FinishY + 6f);
            if (!IsInsidePit(dashTarget)) player.transform.position = dashTarget;
            dashReadyAt = Time.time + dashCooldown;
        }
    }

    private void UpdateNpc(ExperimentRunner npc, float speed)
    {
        if (npc == null || npc.Finished || npc.IsStunned) return;
        if (npc == programmer && programmerHanging) return;
        if (npc.IsInHazard)
        {
            if (npc.TryResolveHazard(out bool survived) && !survived)
            {
                npc.gameObject.SetActive(false);
            }
            return;
        }

        Vector3 oldPosition = npc.transform.position;
        Vector3 position = oldPosition;
        float targetX = npc == programmer ? -2f : 2f;
        position.x = Mathf.MoveTowards(position.x, targetX, speed * 0.25f * Time.deltaTime);
        position.y += speed * Time.deltaTime;
        if (IsInsidePit(position))
        {
            npc.transform.position = CellCenter(WorldToCell(position));
            npc.BeginHazard(FindSafeAdjacent(oldPosition), 0.7f);
        }
        else
        {
            npc.transform.position = position;
        }

        if (npc == programmer && !programmerHanging && rescueOutcome == RescueOutcome.NotEncountered &&
            position.y >= 38f)
        {
            StartProgrammerFall();
        }
    }

    private void StartProgrammerFall()
    {
        programmerHanging = true;
        programmer.transform.position = CellCenter(RescuePitCell);
        programmer.SetColor(new Color(1f, 0.65f, 0.15f));
    }

    private void UpdateRescue()
    {
        if (!programmerHanging || programmer == null || player == null || playerHazardActive) return;

        bool isOnAdjacentCell = IsCardinallyAdjacent(WorldToCell(player.transform.position), RescuePitCell);
        if (!qteActive && isOnAdjacentCell && Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            qteActive = true;
            qteHits = 0;
            qtePenalty = 0f;
        }

        if (!qteActive)
        {
            if (player.transform.position.y > programmer.transform.position.y + 4f)
            {
                rescueOutcome = RescueOutcome.Ignored;
                programmerHanging = false;
                programmer.gameObject.SetActive(false);
            }
            return;
        }

        if (!isOnAdjacentCell)
        {
            qteActive = false;
            rescueOutcome = RescueOutcome.Abandoned;
            return;
        }

        qteAngle = Mathf.Repeat(qteAngle + Time.deltaTime * 150f, 180f);
        qtePenalty += Time.deltaTime;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            bool hit = qteAngle >= 65f && qteAngle <= 105f;
            if (hit)
            {
                qteHits++;
                if (qteHits >= 2)
                {
                    qteActive = false;
                    programmerHanging = false;
                    rescueOutcome = RescueOutcome.Saved;
                    programmer.SetColor(new Color(0.3f, 0.85f, 0.45f));
                    programmer.transform.position += Vector3.up * 0.8f;
                }
            }
            else
            {
                qtePenalty += 1.5f;
                remainingTime = Mathf.Max(0f, remainingTime - 1.5f);
            }
        }

        if (qtePenalty >= 10f)
        {
            qteActive = false;
            programmerHanging = false;
            rescueOutcome = RescueOutcome.Failed;
            programmer.gameObject.SetActive(false);
        }
    }

    private void UpdateRocks()
    {
        rockTimer -= Time.deltaTime;
        if (rockTimer <= 0f)
        {
            rockTimer = qteActive ? 1.2f : 2.5f;
            Vector2Int targetCell = qteActive
                ? WorldToCell(player.transform.position)
                : new Vector2Int(
                    Random.Range(-5, 6),
                    Mathf.RoundToInt(Mathf.Clamp(player.transform.position.y + Random.Range(3f, 9f), 5f,
                        FinishY - 2f))
                );
            SpawnRock(targetCell);
        }

        for (int i = rocks.Count - 1; i >= 0; i--)
        {
            FallingRock rock = rocks[i];
            rock.Rock.transform.position += Vector3.down * (rock.FallSpeed * Time.deltaTime);

            if (!rock.HitPlayer && !playerFinished && !playerHazardActive &&
                Vector2.Distance(player.transform.position, rock.Rock.transform.position) <= 0.8f)
            {
                rock.HitPlayer = true;
                BeginPlayerHazard("Удар камнем", player.transform.position);
            }

            TryHitNpcWithRock(rock, programmer, ref rock.HitProgrammer);
            TryHitNpcWithRock(rock, competitor, ref rock.HitCompetitor);

            if (rock.Rock.transform.position.y < StartY - 3f)
            {
                Destroy(rock.Rock);
                rocks.RemoveAt(i);
            }
        }
    }

    private void TryHitNpcWithRock(FallingRock rock, ExperimentRunner npc, ref bool alreadyHit)
    {
        if (alreadyHit || npc == null || !npc.gameObject.activeSelf || npc.Finished || npc.IsInHazard) return;
        if (Vector2.Distance(npc.transform.position, rock.Rock.transform.position) > 0.8f) return;

        alreadyHit = true;
        npc.BeginHazard(npc.transform.position, 0.7f);
    }

    private Vector3 FindSafeAdjacent(Vector3 origin)
    {
        Vector3[] candidates =
        {
            origin + Vector3.right,
            origin + Vector3.left,
            origin + Vector3.down,
            origin + Vector3.up,
        };
        foreach (Vector3 candidate in candidates)
        {
            if (!IsInsidePit(candidate) && Mathf.Abs(candidate.x) < TrackHalfWidth)
            {
                return candidate;
            }
        }
        return origin;
    }

    private void BeginPlayerHazard(string hazardName, Vector3 returnPosition)
    {
        if (playerHazardActive) return;
        if (qteActive)
        {
            qteActive = false;
            rescueOutcome = RescueOutcome.Abandoned;
        }
        playerHazardActive = true;
        playerHazardHits = 0;
        playerHazardName = hazardName;
        playerHazardReturnPosition = returnPosition;
        player.SetColor(new Color(0.12f, 0.25f, 0.45f));
    }

    private void UpdatePlayerHazard()
    {
        if (!playerHazardActive || Keyboard.current == null) return;

        qteAngle = Mathf.Repeat(qteAngle + Time.deltaTime * 150f, 180f);
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;
        if (qteAngle < 65f || qteAngle > 105f) return;

        playerHazardHits++;
        if (playerHazardHits < 2) return;

        playerHazardActive = false;
        player.transform.position = playerHazardReturnPosition;
        player.SetColor(new Color(0.2f, 0.65f, 1f));
    }

    private void UpdateFinishStates()
    {
        if (!playerFinished && player.transform.position.y >= FinishY)
        {
            playerFinished = true;
            player.MarkFinished(raceDuration - remainingTime);
            player.SetColor(new Color(0.25f, 0.9f, 1f));
        }

        if (!programmerFinished && programmer != null && programmer.gameObject.activeSelf &&
            programmer.transform.position.y >= FinishY)
        {
            programmerFinished = true;
            programmer.MarkFinished(raceDuration - remainingTime);
        }

        if (!competitorFinished && competitor.transform.position.y >= FinishY)
        {
            competitorFinished = true;
            competitor.MarkFinished(raceDuration - remainingTime);
        }
    }

    private IEnumerator ResolveRace()
    {
        if (phase != ExperimentPhase.Running) yield break;
        phase = ExperimentPhase.Execution;

        ExperimentRunner victim = null;
        if (!programmerFinished && programmer != null) victim = programmer;
        else if (!competitorFinished) victim = competitor;

        if (victim != null)
        {
            victim.gameObject.SetActive(true);
            victim.transform.position = new Vector3(0f, FinishY - 3f, 0f);
            executionText = $"Надзиратель: Заключённый {victim.DisplayName} не прошёл испытание.";
            guard.gameObject.SetActive(true);
            guard.position = victim.transform.position + Vector3.right * 2f;
            yield return new WaitForSeconds(2f);
            executionText = "Выстрел.";
            victim.SetColor(new Color(0.25f, 0.05f, 0.05f));
            yield return new WaitForSeconds(1.5f);
        }

        if (!playerFinished)
        {
            phase = ExperimentPhase.Failed;
            yield break;
        }

        bool playerWon = player.transform.position.y >= FinishY &&
                         (!competitorFinished || player.FinishTime <= competitor.FinishTime) &&
                         (!programmerFinished || player.FinishTime <= programmer.FinishTime);
        phase = playerWon ? ExperimentPhase.ImplantChoice : ExperimentPhase.ImplantTest;
    }

    private void UpdateImplantTest()
    {
        playerFinished = false;
        UpdatePlayerMovement(true);
    }

    private void AcceptImplant(bool accepted)
    {
        if (accepted)
        {
            RunState.HasReactiveFeet = true;
        }
        implantAccepted = RunState.HasReactiveFeet;
        phase = ExperimentPhase.ImplantTest;
        player.transform.position = new Vector3(0f, FinishY + 2f, 0f);
    }

    private void FollowPlayer()
    {
        if (cam == null || player == null) return;
        Vector3 target = new Vector3(0f, Mathf.Clamp(player.transform.position.y + 3f, 6f, FinishY), -10f);
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
        CreateRect("Start", new Vector2(0f, StartY), new Vector2(12f, 0.25f), Color.white, -5);
        CreateRect("Finish", new Vector2(0f, FinishY), new Vector2(12f, 0.35f),
            new Color(0.2f, 1f, 0.35f), -5);

        AddPit(new Vector2Int(-4, 12));
        AddPit(new Vector2Int(-2, 15));
        AddPit(new Vector2Int(3, 19));
        AddPit(new Vector2Int(1, 25));
        AddPit(new Vector2Int(-5, 31));
        AddPit(new Vector2Int(4, 34));
        AddPit(RescuePitCell);
        AddPit(new Vector2Int(0, 45));
        AddPit(new Vector2Int(3, 49));
        AddPit(new Vector2Int(-2, 53));

        player = CreateRunner("Игрок", new Vector2(0f, 0.8f), new Color(0.2f, 0.65f, 1f));
        implantAccepted = RunState.HasReactiveFeet;
        programmer = CreateRunner("Программист", new Vector2(-2f, 0.8f), new Color(1f, 0.65f, 0.15f));
        competitor = CreateRunner("Заключённая 2", new Vector2(2f, 0.8f), new Color(0.9f, 0.25f, 0.65f));

        guard = CreateRunner("Надзиратель", new Vector2(0f, -10f), new Color(0.1f, 0.1f, 0.1f)).transform;
        guard.gameObject.SetActive(false);
    }

    private ExperimentRunner CreateRunner(string displayName, Vector2 position, Color color)
    {
        GameObject go = CreateCircle(displayName, position, color, 5);
        go.transform.localScale = Vector3.one * 0.8f;
        ExperimentRunner runner = go.AddComponent<ExperimentRunner>();
        runner.Initialize(displayName);
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
                CreateRect("Floor", new Vector2(x, y), Vector2.one * 0.97f, floorColor, -20);
            }

            CreateRect("Left Wall Top", new Vector2(-6f, y), Vector2.one * 0.97f, wallTop, -8);
            CreateRect("Right Wall Top", new Vector2(6f, y), Vector2.one * 0.97f, wallTop, -8);
            CreateRect("Left Wall Side", new Vector2(-5.72f, y - 0.18f), new Vector2(0.42f, 0.55f),
                wallSide, -7);
            CreateRect("Right Wall Side", new Vector2(5.72f, y - 0.18f), new Vector2(0.42f, 0.55f),
                wallSide, -7);
        }
    }

    private void AddPit(Vector2Int cell)
    {
        Rect rect = new(cell.x - CellSize * 0.5f, cell.y - CellSize * 0.5f, CellSize, CellSize);
        pits.Add(rect);
        CreateRect("Pit", CellCenter(cell), Vector2.one * CellSize * 0.92f,
            new Color(0.015f, 0.015f, 0.02f), -10);
    }

    private bool IsInsidePit(Vector3 point)
    {
        Vector2 p = point;
        foreach (Rect pit in pits)
        {
            if (pit.Contains(p)) return true;
        }
        return false;
    }

    private void SpawnRock(Vector2Int targetCell)
    {
        Vector2 start = new(targetCell.x, FinishY + 2f);
        GameObject rock = CreateCircle("Rolling Rock", start, new Color(0.24f, 0.16f, 0.10f), 8);
        rock.transform.localScale = Vector3.one * CellSize * 0.92f;
        rocks.Add(new FallingRock
        {
            Rock = rock,
            FallSpeed = 2.6f,
        });
    }

    private static Vector2Int WorldToCell(Vector3 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
    }

    private static Vector2 CellCenter(Vector2Int cell)
    {
        return new Vector2(cell.x, cell.y);
    }

    private static bool IsCardinallyAdjacent(Vector2Int first, Vector2Int second)
    {
        Vector2Int delta = first - second;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    private GameObject CreateRect(string objectName, Vector2 position, Vector2 size, Color color, int order)
    {
        GameObject go = new(objectName);
        go.transform.position = position;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PrototypeSprites.Square;
        renderer.color = color;
        renderer.sortingOrder = order;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go;
    }

    private GameObject CreateCircle(string objectName, Vector2 position, Color color, int order)
    {
        GameObject go = new(objectName);
        go.transform.position = position;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PrototypeSprites.Circle;
        renderer.color = color;
        renderer.sortingOrder = order;
        return go;
    }

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
            GUI.Box(new Rect(12, 12, 390, 78), "");
            GUI.Label(new Rect(28, 18, 350, 32), $"Время: {FormatTime(remainingTime)}", titleStyle);
            GUI.Label(new Rect(28, 52, 350, 26), "Финиш наверху", bodyStyle);
            if (programmerHanging)
            {
                GUI.Box(new Rect(Screen.width / 2f - 230, 100, 460, 72), "");
                GUI.Label(new Rect(Screen.width / 2f - 210, 112, 420, 52),
                    qteActive
                        ? "Оставайся на соседней клетке и попади в зону дважды!"
                        : "Программист: Встань на соседнюю клетку и нажми E!",
                    bodyStyle);
            }
            if (playerHazardActive)
            {
                GUI.Box(new Rect(Screen.width / 2f - 230, 100, 460, 72), "");
                GUI.Label(new Rect(Screen.width / 2f - 210, 112, 420, 52),
                    $"{playerHazardName}: пройди QTE, чтобы выбраться!", bodyStyle);
            }
            if (qteActive) DrawQte(qteHits, "Спасение");
            else if (playerHazardActive) DrawQte(playerHazardHits, "Выбраться");
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
            if (GUI.Button(new Rect(Screen.width / 2f - 210, Screen.height / 2f + 45, 190, 55),
                    "Принять", buttonStyle))
                AcceptImplant(true);
            if (GUI.Button(new Rect(Screen.width / 2f + 20, Screen.height / 2f + 45, 190, 55),
                    "Отказаться", buttonStyle))
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
        {
            DrawDialog("Игрок не успел. Забег завершён.", "R — начать заново");
        }
    }

    private void DrawQte(int hits, string label)
    {
        Rect box = new(Screen.width / 2f - 130, Screen.height - 230, 260, 170);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 35, box.y + 12, 200, 28), label, bodyStyle);
        GUI.Label(new Rect(box.x + 35, box.y + 40, 200, 30), $"Попадания: {hits}/2", bodyStyle);

        Vector2 center = new(box.center.x, box.yMax - 25f);
        float radius = 85f;
        for (int angle = 0; angle <= 180; angle += 5)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * radius;
            bool target = angle >= 65 && angle <= 105;
            GUI.color = target ? Color.green : Color.gray;
            GUI.DrawTexture(new Rect(point.x - 3, point.y - 3, 6, 6), Texture2D.whiteTexture);
        }

        float pointerRad = qteAngle * Mathf.Deg2Rad;
        Vector2 pointer = center + new Vector2(Mathf.Cos(pointerRad), -Mathf.Sin(pointerRad)) * radius;
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(pointer.x - 7, pointer.y - 7, 14, 14), Texture2D.whiteTexture);
        GUI.color = Color.white;
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

    private sealed class FallingRock
    {
        public GameObject Rock;
        public float FallSpeed;
        public bool HitPlayer;
        public bool HitProgrammer;
        public bool HitCompetitor;
    }
}

public class ExperimentRunner : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float stunnedUntil;
    private float hazardResolveAt;
    private bool hazardWillSurvive;
    private Vector3 hazardReturnPosition;

    public string DisplayName { get; private set; }
    public bool Finished { get; set; }
    public float FinishTime { get; private set; } = float.MaxValue;
    public bool IsStunned => Time.time < stunnedUntil;
    public bool IsInHazard { get; private set; }

    public void Initialize(string displayName)
    {
        DisplayName = displayName;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Stun(float duration)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
    }

    public void MarkFinished(float finishTime)
    {
        Finished = true;
        FinishTime = finishTime;
    }

    public void BeginHazard(Vector3 returnPosition, float survivalChance)
    {
        if (IsInHazard || Finished) return;
        IsInHazard = true;
        hazardResolveAt = Time.time + 1.8f;
        hazardWillSurvive = Random.value <= survivalChance;
        hazardReturnPosition = returnPosition;
        SetColor(new Color(0.18f, 0.12f, 0.08f));
    }

    public bool TryResolveHazard(out bool survived)
    {
        survived = false;
        if (!IsInHazard || Time.time < hazardResolveAt) return false;

        IsInHazard = false;
        survived = hazardWillSurvive;
        if (survived)
        {
            transform.position = hazardReturnPosition;
            SetColor(DisplayName == "Программист"
                ? new Color(1f, 0.65f, 0.15f)
                : new Color(0.9f, 0.25f, 0.65f));
        }
        return true;
    }

    public void SetColor(Color color)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = color;
    }
}

internal static class PrototypeSprites
{
    private static Sprite square;
    private static Sprite circle;

    public static Sprite Square => square != null ? square : square = BuildSquare();
    public static Sprite Circle => circle != null ? circle : circle = BuildCircle();

    private static Sprite BuildSquare()
    {
        Texture2D texture = new(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }

    private static Sprite BuildCircle()
    {
        const int size = 32;
        Texture2D texture = new(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = Vector2.Distance(new Vector2(x, y), center) <= size * 0.45f
                    ? Color.white
                    : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
