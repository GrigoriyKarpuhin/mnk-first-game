using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Полноэкранный инвентарь: расходники, сюжетные предметы, материалы и три
/// быстрых слота. Экран строится через общую CRT UI-систему и останавливает
/// игровое время, пока игрок управляет снаряжением.
/// </summary>
public sealed class InventoryUI : MonoBehaviour
{
    private enum InventoryTab
    {
        Consumables,
        KeyItems,
        Materials,
        Implants,
    }

    private static readonly CraftedItemId[] Consumables =
    {
        CraftedItemId.Medkit,
        CraftedItemId.NoiseBeacon,
        CraftedItemId.SmokeBomb,
        CraftedItemId.EmpGrenade,
        CraftedItemId.HologramGrenade,
    };

    private static readonly PrisonItemId[] KeyItems =
    {
        PrisonItemId.Screwdriver,
        PrisonItemId.KitchenManifest,
        PrisonItemId.ServiceBadge,
        PrisonItemId.EyeImplant,
        PrisonItemId.Transmitter,
        PrisonItemId.ExperimentReports,
        PrisonItemId.DataSource,
        PrisonItemId.ComputeModule,
        PrisonItemId.SignalAmplifier,
        PrisonItemId.TechWingKey,
        PrisonItemId.ArchiveKey,
        PrisonItemId.EscapeArchiveFolder,
    };

    private static readonly CraftMaterialId[] Materials =
    {
        CraftMaterialId.Chemicals,
        CraftMaterialId.QualityChemicals,
        CraftMaterialId.ScrapMetal,
        CraftMaterialId.QualityScrapMetal,
        CraftMaterialId.Microchips,
        CraftMaterialId.QualityMicrochips,
    };

    private static readonly ImplantId[] Implants =
    {
        ImplantId.EyeImplant,
        ImplantId.MaskingImplant,
        ImplantId.ReactiveFeet,
    };

    private static InventoryUI instance;

    private Player player;
    private GameObject screenRoot;
    private RectTransform listContent;
    private Button[] tabButtons;
    private Button[] hotbarButtons;
    private Text[] hotbarLabels;
    private Text detailCategory;
    private Text detailTitle;
    private Text detailCount;
    private Text detailDescription;
    private Text detailHint;
    private Button primaryButton;
    private Text primaryButtonLabel;
    private Button clearButton;
    private Text statusLabel;
    private InventoryTab activeTab;
    private CraftedItemId selectedConsumable = CraftedItemId.Medkit;
    private PrisonItemId selectedKeyItem = PrisonItemId.None;
    private CraftMaterialId selectedMaterial = CraftMaterialId.Chemicals;
    private ImplantId selectedImplant = ImplantId.EyeImplant;
    private float previousTimeScale = 1f;
    private bool timeScaleCaptured;

    public static bool IsOpen =>
        instance != null &&
        instance.enabled &&
        instance.screenRoot != null &&
        instance.screenRoot.activeSelf;

    public static void Toggle(Player owner)
    {
        if (IsOpen) Close();
        else Open(owner);
    }

    public static void Open(Player owner)
    {
        if (owner == null) return;

        EnsureInstance();
        instance.player = owner;
        instance.activeTab = InventoryTab.Consumables;
        instance.SelectFirstAvailableConsumable();
        instance.CaptureTimeScale();
        instance.enabled = true;
        instance.screenRoot.SetActive(true);
        instance.RefreshAll();
    }

    /// <summary>Открыть инвентарь сразу на схеме имплантов.</summary>
    public static void OpenImplants(Player owner)
    {
        Open(owner);
        if (!IsOpen) return;
        instance.activeTab = InventoryTab.Implants;
        instance.SelectFirstInstalledImplant();
        instance.RefreshAll();
    }

    public static void Close()
    {
        if (instance == null || !instance.enabled) return;

        instance.RestoreTimeScale();
        if (instance.screenRoot != null) instance.screenRoot.SetActive(false);
        instance.player = null;
        instance.enabled = false;
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        var go = new GameObject("InventoryUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InventoryUI>();
        instance.BuildUI();
        instance.screenRoot.SetActive(false);
        instance.enabled = false;
    }

    private void Update()
    {
        if (!enabled || Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame ||
            Keyboard.current.iKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void BuildUI()
    {
        Canvas canvas = UIKit.CreateRootCanvas(gameObject, UITheme.SortInventory, worldFacing: true);

        Image backdrop = UIKit.CreatePanel("Backdrop", canvas.transform, UITheme.Backdrop);
        UIKit.FullStretch(backdrop.rectTransform);
        screenRoot = backdrop.gameObject;

        Image panel = UIKit.CreateTerminalPanel(
            "InventoryTerminal",
            backdrop.transform,
            out RectTransform content,
            scanlines: true,
            brackets: true);
        UIKit.Anchor(
            panel.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1160f, 650f));

        BuildHeader(content);
        BuildHotbar(content);
        BuildTabs(content);
        BuildList(content);
        BuildDetails(content);
    }

    private void BuildHeader(RectTransform content)
    {
        Text eyebrow = UIKit.CreateStencilLabel("ЛИЧНОЕ СНАРЯЖЕНИЕ · C-4821", content);
        UIKit.TopRect(eyebrow.rectTransform, 0f, 0f, 250f, 22f);

        Text title = UIKit.CreateText(
            "Title",
            content,
            UITheme.TypeTitle,
            TextAnchor.MiddleLeft,
            UITheme.TextBright);
        title.text = "ИНВЕНТАРЬ";
        title.fontStyle = FontStyle.Bold;
        UIKit.TopRect(title.rectTransform, 0f, 18f, 250f, 40f);

        Button close = UIKit.CreateButton("ЗАКРЫТЬ  [I / ESC]", content, Close, out _);
        UIKit.Anchor(
            close.GetComponent<RectTransform>(),
            Vector2.one,
            Vector2.one,
            Vector2.one,
            new Vector2(0f, -12f),
            new Vector2(210f, 42f));
    }

    private void BuildHotbar(RectTransform content)
    {
        Image hotbar = UIKit.CreateScreen("QuickSlots", content);
        UIKit.TopRect(hotbar.rectTransform, 0f, 66f, 0f, 94f);
        UIKit.AddFrame(hotbar.rectTransform, UITheme.BorderDim, UITheme.Border, UITheme.BorderMed, UITheme.BorderThin);

        Text label = UIKit.CreateStencilLabel("БЫСТРЫЙ ДОСТУП · КЛАВИШИ 1–3", hotbar.transform);
        UIKit.TopRect(label.rectTransform, UITheme.Space3, UITheme.Space2, UITheme.Space3, 22f);

        hotbarButtons = new Button[Player.QuickSlotCount];
        hotbarLabels = new Text[Player.QuickSlotCount];
        for (int i = 0; i < Player.QuickSlotCount; i++)
        {
            int slot = i;
            Button button = UIKit.CreateButton("", hotbar.transform, () => SelectHotbarSlot(slot), out Text text);
            RectTransform rect = button.GetComponent<RectTransform>();
            float fraction = 1f / Player.QuickSlotCount;
            rect.anchorMin = new Vector2(i * fraction, 0f);
            rect.anchorMax = new Vector2((i + 1) * fraction, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(i == 0 ? UITheme.Space3 : UITheme.Space2, UITheme.Space2);
            rect.offsetMax = new Vector2(i == Player.QuickSlotCount - 1 ? -UITheme.Space3 : -UITheme.Space2, 48f);
            text.alignment = TextAnchor.MiddleLeft;
            hotbarButtons[i] = button;
            hotbarLabels[i] = text;
        }
    }

    private void BuildTabs(RectTransform content)
    {
        Image tabsHost = UIKit.CreatePanel("Tabs", content, Color.clear);
        tabsHost.raycastTarget = false;
        UIKit.TopRect(tabsHost.rectTransform, 0f, 172f, 0f, 44f);
        tabButtons = UIKit.CreateTabBar(
            tabsHost.transform,
            out _,
            "РАСХОДНИКИ",
            "КЛЮЧЕВЫЕ ПРЕДМЕТЫ",
            "МАТЕРИАЛЫ",
            "ИМПЛАНТЫ");

        for (int i = 0; i < tabButtons.Length; i++)
        {
            InventoryTab tab = (InventoryTab)i;
            tabButtons[i].onClick.AddListener(() => SetTab(tab));
        }
    }

    private void BuildList(RectTransform content)
    {
        Image listPanel = UIKit.CreateScreen("ItemList", content);
        RectTransform rect = listPanel.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0.48f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-UITheme.Space2, -228f);
        UIKit.AddFrame(rect, UITheme.BorderDim, UITheme.Border, UITheme.BorderMed, UITheme.BorderThin);

        listContent = UIKit.CreateScrollView("Scroll", listPanel.transform, out _);
        UIKit.Stretch(
            listContent.parent as RectTransform,
            UITheme.Space2,
            UITheme.Space2,
            UITheme.Space2,
            UITheme.Space2);
    }

    private void BuildDetails(RectTransform content)
    {
        Image details = UIKit.CreateScreen("ItemDetails", content);
        RectTransform rect = details.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(UITheme.Space2, 0f);
        rect.offsetMax = new Vector2(0f, -228f);
        UIKit.AddFrame(rect, UITheme.BorderDim, UITheme.Border, UITheme.BorderMed, UITheme.BorderThin);

        detailCategory = UIKit.CreateStencilLabel("", details.transform);
        UIKit.TopRect(detailCategory.rectTransform, UITheme.Space4, UITheme.Space3, UITheme.Space4, 22f);

        detailTitle = UIKit.CreateText(
            "ItemTitle",
            details.transform,
            UITheme.TypeHeader,
            TextAnchor.UpperLeft,
            UITheme.TextBright);
        detailTitle.fontStyle = FontStyle.Bold;
        detailTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.TopRect(detailTitle.rectTransform, UITheme.Space4, 38f, UITheme.Space4, 58f);

        detailCount = UIKit.CreateText(
            "ItemCount",
            details.transform,
            UITheme.TypeBody,
            TextAnchor.UpperLeft,
            UITheme.Success);
        UIKit.TopRect(detailCount.rectTransform, UITheme.Space4, 96f, UITheme.Space4, 28f);

        detailDescription = UIKit.CreateText(
            "ItemDescription",
            details.transform,
            UITheme.TypeBody,
            TextAnchor.UpperLeft,
            UITheme.TextPrimary);
        detailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.TopRect(detailDescription.rectTransform, UITheme.Space4, 134f, UITheme.Space4, 92f);

        detailHint = UIKit.CreateText(
            "ItemHint",
            details.transform,
            UITheme.TypeLabel,
            TextAnchor.UpperLeft,
            UITheme.TextStencil);
        detailHint.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.TopRect(detailHint.rectTransform, UITheme.Space4, 234f, UITheme.Space4, 64f);

        statusLabel = UIKit.CreateText(
            "Status",
            details.transform,
            UITheme.TypeLabel,
            TextAnchor.MiddleLeft,
            UITheme.Warning);
        statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Anchor(
            statusLabel.rectTransform,
            Vector2.zero,
            new Vector2(1f, 0f),
            Vector2.zero,
            new Vector2(UITheme.Space4, 92f),
            new Vector2(-UITheme.Space8, 44f));

        primaryButton = UIKit.CreateButton(
            "НАЗНАЧИТЬ",
            details.transform,
            AssignSelectedConsumable,
            out primaryButtonLabel);
        UIKit.Anchor(
            primaryButton.GetComponent<RectTransform>(),
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            new Vector2(UITheme.Space4, UITheme.Space4),
            new Vector2(250f, 56f));

        clearButton = UIKit.CreateButton(
            "ОЧИСТИТЬ СЛОТ",
            details.transform,
            ClearSelectedSlot,
            out _);
        UIKit.Anchor(
            clearButton.GetComponent<RectTransform>(),
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            new Vector2(282f, UITheme.Space4),
            new Vector2(220f, 56f));
    }

    private void SetTab(InventoryTab tab)
    {
        activeTab = tab;
        statusLabel.text = "";
        if (tab == InventoryTab.KeyItems) SelectFirstOwnedKeyItem();
        if (tab == InventoryTab.Implants) SelectFirstInstalledImplant();
        RefreshAll();
    }

    private void SelectHotbarSlot(int slot)
    {
        if (player == null) return;
        player.SelectQuickSlot(slot);
        statusLabel.text = $"Выбран быстрый слот {slot + 1}.";
        RefreshHotbar();
        RefreshDetails();
    }

    private void AssignSelectedConsumable()
    {
        if (player == null || activeTab != InventoryTab.Consumables) return;

        if (player.TrySetQuickSlot(player.SelectedQuickSlotIndex, selectedConsumable, out string message))
        {
            statusLabel.color = UITheme.Success;
        }
        else
        {
            statusLabel.color = UITheme.Warning;
        }
        statusLabel.text = message;
        RefreshHotbar();
        RefreshDetails();
    }

    private void ClearSelectedSlot()
    {
        if (player == null) return;

        player.TrySetQuickSlot(player.SelectedQuickSlotIndex, CraftedItemId.None, out string message);
        statusLabel.color = UITheme.Success;
        statusLabel.text = message;
        RefreshHotbar();
        RefreshDetails();
    }

    private void RefreshAll()
    {
        RefreshTabs();
        RebuildList();
        RefreshHotbar();
        RefreshDetails();
    }

    private void RefreshTabs()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            ColorBlock colors = tabButtons[i].colors;
            colors.normalColor = i == (int)activeTab ? UITheme.Selected : UITheme.ButtonNormal;
            tabButtons[i].colors = colors;
        }
    }

    private void RefreshHotbar()
    {
        if (player == null || hotbarButtons == null) return;

        for (int i = 0; i < hotbarButtons.Length; i++)
        {
            bool selected = player.SelectedQuickSlotIndex == i;
            CraftedItemId item = player.GetQuickSlotItem(i);
            hotbarLabels[i].text =
                $"{(selected ? "▶" : " ")}  [{i + 1}]  {player.GetQuickSlotLabel(i)}";

            ColorBlock colors = hotbarButtons[i].colors;
            colors.normalColor = selected ? UITheme.Selected : UITheme.ButtonNormal;
            hotbarButtons[i].colors = colors;
            hotbarLabels[i].color = selected ? UITheme.TextBright : UITheme.TextPrimary;

            if (item != CraftedItemId.None && RunState.CraftedItemCount(item) <= 0)
            {
                hotbarLabels[i].color = UITheme.TextDisabled;
            }
        }
    }

    private void RebuildList()
    {
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }

        float y = 0f;
        switch (activeTab)
        {
            case InventoryTab.Consumables:
                foreach (CraftedItemId item in Consumables)
                {
                    CraftedItemId captured = item;
                    bool selected = item == selectedConsumable;
                    string count = RunState.CraftedItemCount(item).ToString();
                    AddListRow(
                        $"{RunState.CraftedItemName(item)}\n<size=13>В НАЛИЧИИ: {count}</size>",
                        selected,
                        () =>
                        {
                            selectedConsumable = captured;
                            statusLabel.text = "";
                            RefreshAll();
                        },
                        ref y);
                }
                break;

            case InventoryTab.KeyItems:
                int owned = 0;
                foreach (PrisonItemId item in KeyItems)
                {
                    if (!RunState.HasPrisonItem(item)) continue;
                    owned++;
                    PrisonItemId captured = item;
                    AddListRow(
                        DisplayName(item),
                        item == selectedKeyItem,
                        () =>
                        {
                            selectedKeyItem = captured;
                            statusLabel.text = "";
                            RefreshAll();
                        },
                        ref y);
                }
                if (owned == 0)
                {
                    AddEmptyState("Ключевых предметов пока нет.", ref y);
                }
                break;

            case InventoryTab.Materials:
                foreach (CraftMaterialId material in Materials)
                {
                    CraftMaterialId captured = material;
                    AddListRow(
                        $"{RunState.MaterialName(material)}\n<size=13>ЗАПАС: {RunState.MaterialCount(material)}</size>",
                        material == selectedMaterial,
                        () =>
                        {
                            selectedMaterial = captured;
                            statusLabel.text = "";
                            RefreshAll();
                        },
                        ref y);
                }
                break;

            case InventoryTab.Implants:
                BuildImplantDiagram(ref y);
                break;
        }

        UIKit.SetScrollContentHeight(listContent, Mathf.Max(y, 1f));
    }

    private void AddListRow(string label, bool selected, Action onClick, ref float y)
    {
        Button row = UIKit.CreateListRow(label, listContent, () => onClick(), out Image bg, out _);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -y);
        rect.sizeDelta = new Vector2(0f, 66f);
        bg.color = selected ? UITheme.Selected : UITheme.RowNormal;
        y += 74f;
    }

    private void AddEmptyState(string message, ref float y)
    {
        Text empty = UIKit.CreateText(
            "Empty",
            listContent,
            UITheme.TypeBody,
            TextAnchor.UpperLeft,
            UITheme.TextMuted);
        empty.text = message;
        empty.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform rect = empty.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -UITheme.Space4);
        rect.sizeDelta = new Vector2(0f, 72f);
        y = 96f;
    }

    private void BuildImplantDiagram(ref float y)
    {
        Text title = UIKit.CreateStencilLabel("СИЛУЭТ ГЕРОЯ · ИМПЛАНТЫ", listContent, TextAnchor.MiddleCenter);
        UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(0f, 24f));

        Image silhouette = UIKit.CreatePanel("PlayerSilhouette", listContent, UITheme.Panel);
        silhouette.sprite = Resources.Load<Sprite>(SpriteCatalog.Resolve("player"));
        silhouette.type = Image.Type.Simple;
        silhouette.preserveAspect = true;
        silhouette.color = UITheme.TextMuted;
        silhouette.raycastTarget = false;
        UIKit.Anchor(silhouette.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(300f, 300f));

        CreateImplantSlot(ImplantId.EyeImplant, new Vector2(0f, -76f), new Vector2(76f, 28f), "ГЛАЗ");
        CreateImplantSlot(ImplantId.MaskingImplant, new Vector2(0f, -146f), new Vector2(114f, 32f), "МАСКИРОВКА");
        CreateImplantSlot(ImplantId.ReactiveFeet, new Vector2(0f, -282f), new Vector2(142f, 32f), "РЕАКТИВНЫЕ СТОПЫ");

        Text hint = UIKit.CreateText("Hint", listContent, UITheme.TypeLabel, TextAnchor.MiddleCenter, UITheme.TextStencil);
        hint.text = "Наведите на подсвеченный модуль, чтобы прочитать свойство.";
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Anchor(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(420f, 32f));
        y = 390f;
    }

    private void CreateImplantSlot(ImplantId implant, Vector2 position, Vector2 size, string label)
    {
        bool installed = RunState.HasImplant(implant);
        Button slot = UIKit.CreateButton(installed ? label : "—", listContent, () => SelectImplant(implant), out Text text, UITheme.TypeCaption);
        UIKit.Anchor(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), position, size);
        slot.interactable = installed;
        text.color = installed ? UITheme.TextBright : UITheme.TextDisabled;
        if (!installed) return;

        var trigger = slot.gameObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => SelectImplant(implant));
        trigger.triggers.Add(enter);
    }

    private void SelectImplant(ImplantId implant)
    {
        selectedImplant = implant;
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (player == null) return;

        clearButton.interactable = activeTab == InventoryTab.Consumables &&
            player.GetQuickSlotItem(player.SelectedQuickSlotIndex) != CraftedItemId.None;
        clearButton.gameObject.SetActive(activeTab == InventoryTab.Consumables);

        switch (activeTab)
        {
            case InventoryTab.Consumables:
                int count = RunState.CraftedItemCount(selectedConsumable);
                detailCategory.text = "РАСХОДНИК";
                detailTitle.text = RunState.CraftedItemName(selectedConsumable).ToUpperInvariant();
                detailCount.text = $"В НАЛИЧИИ: {count}";
                detailDescription.text = RunState.CraftedItemDescription(selectedConsumable);
                detailHint.text = ConsumableHint(selectedConsumable);
                primaryButtonLabel.text = $"В СЛОТ {player.SelectedQuickSlotIndex + 1}";
                primaryButton.interactable = count > 0;
                break;

            case InventoryTab.KeyItems:
                detailCategory.text = "КЛЮЧЕВОЙ ПРЕДМЕТ";
                if (selectedKeyItem == PrisonItemId.None ||
                    !RunState.HasPrisonItem(selectedKeyItem))
                {
                    detailTitle.text = "НЕТ ПРЕДМЕТОВ";
                    detailCount.text = "";
                    detailDescription.text = "Исследуйте блок C и выполняйте задания, чтобы находить сюжетные предметы.";
                }
                else
                {
                    detailTitle.text = DisplayName(selectedKeyItem).ToUpperInvariant();
                    detailCount.text = "ПОЛУЧЕНО";
                    detailDescription.text = KeyItemDescription(selectedKeyItem);
                }
                detailHint.text = "Ключевые предметы применяются автоматически в нужном месте.";
                primaryButtonLabel.text = "АВТОМАТИЧЕСКИ";
                primaryButton.interactable = false;
                break;

            case InventoryTab.Materials:
                detailCategory.text = "МАТЕРИАЛ ДЛЯ КРАФТА";
                detailTitle.text = RunState.MaterialName(selectedMaterial).ToUpperInvariant();
                detailCount.text = $"ЗАПАС: {RunState.MaterialCount(selectedMaterial)}";
                detailDescription.text = MaterialDescription(selectedMaterial);
                detailHint.text = "Используется в мастерской медика-механика.";
                primaryButtonLabel.text = "ДЛЯ КРАФТА";
                primaryButton.interactable = false;
                break;

            case InventoryTab.Implants:
                bool installed = RunState.HasImplant(selectedImplant);
                detailCategory.text = "НЕЙРОИМПЛАНТ";
                detailTitle.text = RunState.ImplantName(selectedImplant).ToUpperInvariant();
                detailCount.text = installed
                    ? $"УСТАНОВЛЕН · УРОВЕНЬ {RunState.ImplantUpgradeLevel(selectedImplant)}/2"
                    : "МОДУЛЬ НЕ УСТАНОВЛЕН";
                detailDescription.text = ImplantDescription(selectedImplant);
                detailHint.text = installed
                    ? ImplantStatusHint(selectedImplant)
                    : "Модуль появится на силуэте после получения.";
                primaryButtonLabel.text = installed ? "УСТАНОВЛЕН" : "НЕДОСТУПЕН";
                primaryButton.interactable = false;
                break;
        }
    }

    private void SelectFirstAvailableConsumable()
    {
        foreach (CraftedItemId item in Consumables)
        {
            if (RunState.CraftedItemCount(item) <= 0) continue;
            selectedConsumable = item;
            return;
        }
        selectedConsumable = Consumables[0];
    }

    private void SelectFirstOwnedKeyItem()
    {
        if (selectedKeyItem != PrisonItemId.None && RunState.HasPrisonItem(selectedKeyItem)) return;
        selectedKeyItem = PrisonItemId.None;
        foreach (PrisonItemId item in KeyItems)
        {
            if (!RunState.HasPrisonItem(item)) continue;
            selectedKeyItem = item;
            return;
        }
    }

    private void SelectFirstInstalledImplant()
    {
        if (RunState.HasImplant(selectedImplant)) return;
        foreach (ImplantId implant in Implants)
        {
            if (!RunState.HasImplant(implant)) continue;
            selectedImplant = implant;
            return;
        }
        selectedImplant = Implants[0];
    }

    private void CaptureTimeScale()
    {
        if (timeScaleCaptured) return;
        previousTimeScale = Time.timeScale;
        timeScaleCaptured = true;
        Time.timeScale = 0f;
    }

    private void RestoreTimeScale()
    {
        if (!timeScaleCaptured) return;
        Time.timeScale = previousTimeScale;
        timeScaleCaptured = false;
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        RestoreTimeScale();
        if (instance == this) instance = null;
    }

    private static string DisplayName(PrisonItemId item)
    {
        string value = Player.GetItemName(item);
        if (string.IsNullOrEmpty(value)) return item.ToString();
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string ConsumableHint(CraftedItemId item) => item switch
    {
        CraftedItemId.Medkit => "Назначьте в быстрый слот и нажмите ЛКМ, чтобы восстановить здоровье.",
        CraftedItemId.NoiseBeacon => "Назначьте в быстрый слот, зажмите ЛКМ для прицеливания и отпустите для броска.",
        _ => "Предмет можно назначить в быстрый слот, но его активный эффект ещё в разработке.",
    };

    private static string MaterialDescription(CraftMaterialId material) => material switch
    {
        CraftMaterialId.Chemicals => "Базовый реагент для аптечек и дымовых шашек.",
        CraftMaterialId.QualityChemicals => "Редкие очищенные реагенты для продвинутых рецептов.",
        CraftMaterialId.ScrapMetal => "Детали и крепёж для простых устройств.",
        CraftMaterialId.QualityScrapMetal => "Исправные механические компоненты для сложных устройств.",
        CraftMaterialId.Microchips => "Электроника для маячков и ЭМИ-устройств.",
        CraftMaterialId.QualityMicrochips => "Редкая электроника для самых сложных рецептов.",
        _ => "",
    };

    private static string ImplantDescription(ImplantId implant) => implant switch
    {
        ImplantId.EyeImplant => "Показывает скрытые системы, камеры и зоны сканирования. Включается и выключается клавишей R.",
        ImplantId.MaskingImplant => "На время маскирует игрока под надзирателя: охрана и камеры перестают его распознавать. Активируется клавишей T.",
        ImplantId.ReactiveFeet => "Позволяет сделать короткий рывок по направлению взгляда. Активируется клавишей Q.",
        _ => "",
    };

    private static string ImplantStatusHint(ImplantId implant) => implant switch
    {
        ImplantId.EyeImplant => RunState.EyeImplantActive ? "Состояние: активен." : "Состояние: отключён. Нажмите R для активации.",
        ImplantId.MaskingImplant => RunState.MaskingImplantActive
            ? $"Состояние: маскировка активна ещё {Mathf.CeilToInt(RunState.MaskingImplantRemaining)} сек."
            : RunState.MaskingImplantCooldownRemaining > 0f
                ? $"Перезарядка: {Mathf.CeilToInt(RunState.MaskingImplantCooldownRemaining)} сек."
                : "Состояние: готов. Нажмите T для маскировки.",
        ImplantId.ReactiveFeet => "Состояние: готов. Нажмите Q для рывка.",
        _ => "",
    };

    private static string KeyItemDescription(PrisonItemId item) => item switch
    {
        PrisonItemId.Screwdriver => "Самодельный инструмент для вентиляционных решёток и простых креплений.",
        PrisonItemId.KitchenManifest => "Служебная накладная с данными, которые помогают открыть склад.",
        PrisonItemId.ServiceBadge => "Пропуск персонала для служебных маршрутов.",
        PrisonItemId.EyeImplant => "Имплант, позволяющий анализировать скрытые системы комплекса.",
        PrisonItemId.Transmitter => "Тюремный передатчик, нужный программисту для анализа сети.",
        PrisonItemId.ExperimentReports => "Документы о скрытой цели проводимых над заключёнными экспериментов.",
        PrisonItemId.DataSource => "Носитель с данными внутренней сети.",
        PrisonItemId.ComputeModule => "Модуль доступа к вычислительной инфраструктуре тюрьмы.",
        PrisonItemId.SignalAmplifier => "Усилитель для завершения системы предсказания экспериментов.",
        PrisonItemId.TechWingKey => "Ключ, открывающий технологическое крыло.",
        PrisonItemId.ArchiveKey => "Комплект ключей от тюремного архива.",
        PrisonItemId.EscapeArchiveFolder => "Архивное дело о заключённом, которому удалось сбежать.",
        _ => "",
    };
}
