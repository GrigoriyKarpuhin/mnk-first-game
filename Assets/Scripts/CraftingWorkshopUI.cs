using System;
using UnityEngine;

public enum CraftingWorkshopMode
{
    Crafting,
    Implants,
}

public sealed class CraftingWorkshopUI : MonoBehaviour
{
    private static readonly CraftMaterialId[] Materials =
    {
        CraftMaterialId.Chemicals,
        CraftMaterialId.QualityChemicals,
        CraftMaterialId.ScrapMetal,
        CraftMaterialId.QualityScrapMetal,
        CraftMaterialId.Microchips,
        CraftMaterialId.QualityMicrochips,
    };

    private static readonly CraftedItemId[] CraftedItems =
    {
        CraftedItemId.Medkit,
        CraftedItemId.NoiseBeacon,
        CraftedItemId.SmokeBomb,
        CraftedItemId.EmpGrenade,
        CraftedItemId.HologramGrenade,
    };

    private static readonly ImplantId[] UpgradeableImplants =
    {
        ImplantId.EyeImplant,
        ImplantId.MaskingImplant,
        ImplantId.ReactiveFeet,
    };

    private static CraftingWorkshopUI instance;

    private CraftingWorkshopMode mode;
    private float previousTimeScale = 1f;
    private string statusMessage;
    private Vector2 recipeScroll;
    private Vector2 inventoryScroll;

    public static bool IsOpen => instance != null && instance.enabled;

    public static void Open(CraftingWorkshopMode requestedMode)
    {
        EnsureInstance();
        instance.mode = requestedMode;
        instance.statusMessage = null;
        instance.recipeScroll = Vector2.zero;
        instance.inventoryScroll = Vector2.zero;
        instance.previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        instance.enabled = true;
    }

    public static void Close()
    {
        if (instance == null || !instance.enabled) return;
        Time.timeScale = instance.previousTimeScale;
        instance.enabled = false;
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        var go = new GameObject("CraftingWorkshopUI");
        instance = go.AddComponent<CraftingWorkshopUI>();
        DontDestroyOnLoad(go);
        instance.enabled = false;
    }

    private void Update()
    {
        if (!enabled) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnGUI()
    {
        if (!enabled) return;

        GUI.depth = -50;
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

        float margin = 36f;
        float headerHeight = 76f;
        Rect header = new Rect(margin, 20f, Screen.width - margin * 2f, headerHeight);
        DrawHeader(header);

        Rect materials = new Rect(margin, header.yMax + 14f, 330f, Screen.height - header.yMax - 42f);
        Rect content = new Rect(materials.xMax + 18f, materials.y, Screen.width - materials.xMax - margin - 18f, materials.height);
        DrawMaterials(materials);

        if (mode == CraftingWorkshopMode.Crafting) DrawCrafting(content);
        else DrawImplants(content);
    }

    private void DrawHeader(Rect rect)
    {
        GUI.Box(rect, "");
        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 12f, rect.width * 0.45f, 28f),
            "МАСТЕРСКАЯ МЕДИКА-МЕХАНИКА",
            HeaderStyle(28));
        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 44f, rect.width * 0.5f, 22f),
            $"Отношение: {RunState.RelationshipTo(NpcId.MedicMechanic)} / 100 ({RelationshipLevels.Label(RunState.RelationshipTo(NpcId.MedicMechanic))})",
            BodyStyle(16));

        if (GUI.Button(new Rect(rect.xMax - 390f, rect.y + 18f, 120f, 40f), "Крафт"))
        {
            mode = CraftingWorkshopMode.Crafting;
            statusMessage = null;
        }

        if (GUI.Button(new Rect(rect.xMax - 260f, rect.y + 18f, 150f, 40f), "Импланты"))
        {
            mode = CraftingWorkshopMode.Implants;
            statusMessage = null;
        }

        if (GUI.Button(new Rect(rect.xMax - 100f, rect.y + 18f, 82f, 40f), "Esc"))
        {
            Close();
        }
    }

    private void DrawMaterials(Rect rect)
    {
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 18f, rect.y + 16f, rect.width - 36f, 28f), "РЕСУРСЫ", HeaderStyle(24));

        inventoryScroll = GUI.BeginScrollView(
            new Rect(rect.x + 14f, rect.y + 54f, rect.width - 28f, rect.height - 72f),
            inventoryScroll,
            new Rect(0f, 0f, rect.width - 48f, 320f));

        float y = 0f;
        foreach (CraftMaterialId material in Materials)
        {
            GUI.Label(new Rect(4f, y, rect.width - 60f, 24f), $"{RunState.MaterialName(material)}: {RunState.MaterialCount(material)}", BodyStyle(16));
            y += 34f;
        }

        y += 8f;
        GUI.Label(new Rect(4f, y, rect.width - 60f, 24f), "Созданные предметы", HeaderStyle(18));
        y += 32f;
        foreach (CraftedItemId item in CraftedItems)
        {
            GUI.Label(new Rect(4f, y, rect.width - 60f, 24f), $"{RunState.CraftedItemName(item)}: {RunState.CraftedItemCount(item)}", BodyStyle(16));
            y += 30f;
        }

        GUI.EndScrollView();
    }

    private void DrawCrafting(Rect rect)
    {
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 18f, rect.y + 16f, rect.width - 36f, 28f), "КРАФТ РАСХОДНИКОВ", HeaderStyle(24));
        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 48f, rect.width - 36f, 42f),
            "Рецепты открываются по мере роста отношений. Продвинутые предметы требуют качественные материалы.",
            BodyStyle(16));

        Rect scrollRect = new Rect(rect.x + 16f, rect.y + 96f, rect.width - 32f, rect.height - 146f);
        recipeScroll = GUI.BeginScrollView(scrollRect, recipeScroll, new Rect(0f, 0f, scrollRect.width - 24f, CraftedItems.Length * 126f));

        float y = 0f;
        foreach (CraftedItemId item in CraftedItems)
        {
            DrawCraftRecipe(new Rect(0f, y, scrollRect.width - 30f, 112f), item);
            y += 126f;
        }

        GUI.EndScrollView();
        DrawStatus(new Rect(rect.x + 18f, rect.yMax - 42f, rect.width - 36f, 28f));
    }

    private void DrawCraftRecipe(Rect rect, CraftedItemId item)
    {
        GUI.Box(rect, "");
        bool unlocked = RunState.IsCraftRecipeUnlocked(item);
        GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 160f, 26f), RunState.CraftedItemName(item), HeaderStyle(20));
        GUI.Label(new Rect(rect.x + 14f, rect.y + 38f, rect.width - 170f, 24f), RunState.CraftedItemDescription(item), BodyStyle(15));
        GUI.Label(new Rect(rect.x + 14f, rect.y + 68f, rect.width - 170f, 24f), $"Стоимость: {RunState.FormatCost(RunState.CraftRecipeCost(item))}", BodyStyle(15));
        GUI.Label(new Rect(rect.x + 14f, rect.y + 90f, rect.width - 170f, 20f), unlocked ? "Рецепт открыт" : "Рецепт закрыт отношениями", unlocked ? PositiveStyle(14) : WarningStyle(14));

        GUI.enabled = unlocked;
        if (GUI.Button(new Rect(rect.xMax - 132f, rect.y + 34f, 112f, 42f), "Создать"))
        {
            RunState.TryCraft(item, out statusMessage);
        }
        GUI.enabled = true;
    }

    private void DrawImplants(Rect rect)
    {
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 18f, rect.y + 16f, rect.width - 36f, 28f), "УЛУЧШЕНИЕ ИМПЛАНТОВ", HeaderStyle(24));
        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 48f, rect.width - 36f, 42f),
            "Пока функционально влияет только маскировочный имплант: дольше работает и быстрее перезаряжается.",
            BodyStyle(16));

        Rect scrollRect = new Rect(rect.x + 16f, rect.y + 96f, rect.width - 32f, rect.height - 146f);
        recipeScroll = GUI.BeginScrollView(scrollRect, recipeScroll, new Rect(0f, 0f, scrollRect.width - 24f, UpgradeableImplants.Length * 136f));

        float y = 0f;
        foreach (ImplantId implant in UpgradeableImplants)
        {
            DrawImplantUpgrade(new Rect(0f, y, scrollRect.width - 30f, 122f), implant);
            y += 136f;
        }

        GUI.EndScrollView();
        DrawStatus(new Rect(rect.x + 18f, rect.yMax - 42f, rect.width - 36f, 28f));
    }

    private void DrawImplantUpgrade(Rect rect, ImplantId implant)
    {
        GUI.Box(rect, "");
        int level = RunState.ImplantUpgradeLevel(implant);
        bool installed = RunState.HasImplant(implant);
        bool maxed = level >= 2;
        bool unlocked = RunState.IsImplantUpgradeUnlocked(implant) && !maxed;
        int requiredRelationship = RunState.ImplantUpgradeRequiredRelationship(implant);
        GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 170f, 26f), $"{RunState.ImplantName(implant)} · уровень {level}/2", HeaderStyle(20));
        GUI.Label(new Rect(rect.x + 14f, rect.y + 38f, rect.width - 170f, 24f), RunState.ImplantUpgradeDescription(implant), BodyStyle(15));
        GUI.Label(new Rect(rect.x + 14f, rect.y + 66f, rect.width - 170f, 24f), installed ? $"Следующее улучшение: {RunState.FormatCost(RunState.ImplantUpgradeCost(implant))}" : "Имплант ещё не установлен.", BodyStyle(15));
        string lockText = maxed ? "Максимум" : unlocked ? "Доступно" : $"Нужно отношение {requiredRelationship}+ и установленный имплант";
        GUI.Label(new Rect(rect.x + 14f, rect.y + 94f, rect.width - 170f, 20f), lockText, unlocked || maxed ? PositiveStyle(14) : WarningStyle(14));

        GUI.enabled = unlocked;
        if (GUI.Button(new Rect(rect.xMax - 132f, rect.y + 40f, 112f, 42f), "Улучшить"))
        {
            RunState.TryUpgradeImplant(implant, out statusMessage);
        }
        GUI.enabled = true;
    }

    private void DrawStatus(Rect rect)
    {
        if (string.IsNullOrEmpty(statusMessage)) return;
        GUI.Label(rect, statusMessage, BodyStyle(16));
    }

    private static GUIStyle HeaderStyle(int fontSize) => new(GUI.skin.label)
    {
        fontSize = fontSize,
        fontStyle = FontStyle.Bold,
        normal = { textColor = new Color(0.75f, 1f, 0.8f) },
        wordWrap = true,
    };

    private static GUIStyle BodyStyle(int fontSize) => new(GUI.skin.label)
    {
        fontSize = fontSize,
        normal = { textColor = Color.white },
        wordWrap = true,
    };

    private static GUIStyle PositiveStyle(int fontSize) => new(GUI.skin.label)
    {
        fontSize = fontSize,
        normal = { textColor = new Color(0.55f, 1f, 0.65f) },
        wordWrap = true,
    };

    private static GUIStyle WarningStyle(int fontSize) => new(GUI.skin.label)
    {
        fontSize = fontSize,
        normal = { textColor = new Color(1f, 0.68f, 0.45f) },
        wordWrap = true,
    };

    private void OnDisable()
    {
        if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
    }

    private void OnDestroy()
    {
        if (enabled) Time.timeScale = previousTimeScale;
    }
}
