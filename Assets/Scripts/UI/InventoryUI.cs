using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InventoryUI : MonoBehaviour
{
    private static readonly CraftedItemId[] Items =
    {
        CraftedItemId.NoiseBeacon,
        CraftedItemId.Medkit,
        CraftedItemId.SmokeBomb,
        CraftedItemId.EmpGrenade,
        CraftedItemId.HologramGrenade,
    };

    private static InventoryUI instance;

    private Player player;
    private Vector2 scroll;

    public static bool IsOpen => instance != null && instance.enabled;

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
        instance.scroll = Vector2.zero;
        instance.enabled = true;
    }

    public static void Close()
    {
        if (instance == null) return;
        instance.enabled = false;
        instance.player = null;
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        var go = new GameObject("InventoryUI");
        instance = go.AddComponent<InventoryUI>();
        DontDestroyOnLoad(go);
        instance.enabled = false;
    }

    private void Update()
    {
        if (!enabled || Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void OnGUI()
    {
        if (!enabled || player == null) return;

        GUI.depth = -55;
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

        float width = Mathf.Min(860f, Screen.width - 72f);
        float height = Mathf.Min(560f, Screen.height - 72f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, "");

        GUI.Label(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, 34f), "ИНВЕНТАРЬ И БЫСТРЫЙ ДОСТУП", HeaderStyle(26));
        GUI.Label(new Rect(panel.x + 24f, panel.y + 56f, panel.width - 48f, 42f),
            "Выберите предмет и назначьте его в слот 1-3. Активный слот используется левой кнопкой мыши.",
            BodyStyle(16));

        DrawHotbar(new Rect(panel.x + 24f, panel.y + 106f, panel.width - 48f, 86f));
        DrawItems(new Rect(panel.x + 24f, panel.y + 206f, panel.width - 48f, panel.height - 250f));

        if (GUI.Button(new Rect(panel.xMax - 116f, panel.yMax - 44f, 92f, 30f), "Закрыть"))
        {
            Close();
        }
    }

    private void DrawHotbar(Rect rect)
    {
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 22f), "Быстрые слоты", HeaderStyle(18));

        float slotWidth = (rect.width - 56f) / Player.QuickSlotCount;
        for (int i = 0; i < Player.QuickSlotCount; i++)
        {
            Rect slot = new Rect(rect.x + 14f + i * slotWidth, rect.y + 38f, slotWidth - 10f, 36f);
            bool selected = player.SelectedQuickSlotIndex == i;
            GUI.Box(slot, selected ? $"[{i + 1}] {player.GetQuickSlotLabel(i)}" : $"{i + 1}. {player.GetQuickSlotLabel(i)}");
        }
    }

    private void DrawItems(Rect rect)
    {
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 22f), "Расходники", HeaderStyle(18));

        Rect view = new Rect(rect.x + 12f, rect.y + 40f, rect.width - 24f, rect.height - 52f);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, view.width - 22f, Items.Length * 112f));

        float y = 0f;
        foreach (CraftedItemId item in Items)
        {
            DrawItemRow(new Rect(0f, y, view.width - 28f, 98f), item);
            y += 112f;
        }

        GUI.EndScrollView();
    }

    private void DrawItemRow(Rect rect, CraftedItemId item)
    {
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 250f, 24f),
            $"{RunState.CraftedItemName(item)} x{RunState.CraftedItemCount(item)}",
            HeaderStyle(18));
        GUI.Label(new Rect(rect.x + 14f, rect.y + 38f, rect.width - 250f, 44f),
            RunState.CraftedItemDescription(item),
            BodyStyle(14));

        for (int i = 0; i < Player.QuickSlotCount; i++)
        {
            if (GUI.Button(new Rect(rect.xMax - 224f + i * 70f, rect.y + 30f, 62f, 34f), $"В {i + 1}"))
            {
                player.SetQuickSlot(i, item);
            }
        }
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
}
