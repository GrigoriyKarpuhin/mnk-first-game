using UnityEngine;
using UnityEngine.InputSystem;

public sealed class AdminPanelUI : MonoBehaviour
{
    private static AdminPanelUI instance;

    private Player player;
    private string status = "";

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
        instance.status = "";
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
        var go = new GameObject("AdminPanelUI");
        instance = go.AddComponent<AdminPanelUI>();
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

        ImguiTheme.Apply();
        GUI.depth = -70;
        ImguiTheme.Fill(new Rect(0f, 0f, Screen.width, Screen.height), UITheme.Backdrop);

        float width = Mathf.Min(560f, Screen.width - 48f);
        float height = 320f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        ImguiTheme.Panel(panel);

        GUI.Label(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, 34f), "АДМИН-ПАНЕЛЬ", ImguiTheme.Title);
        GUI.Label(new Rect(panel.x + 24f, panel.y + 58f, panel.width - 48f, 44f),
            "Сервисные действия для проверки прототипа.", ImguiTheme.Body);

        Rect action = new Rect(panel.x + 24f, panel.y + 116f, panel.width - 48f, 44f);
        if (GUI.Button(action, "+1 ЭМИ-граната в инвентарь", ImguiTheme.Button))
        {
            RunState.AddCraftedItem(CraftedItemId.EmpGrenade, 1);
            status = $"Добавлено: {RunState.CraftedItemName(CraftedItemId.EmpGrenade)} x1. Всего: {RunState.CraftedItemCount(CraftedItemId.EmpGrenade)}.";
        }

        Rect throwableAction = new Rect(panel.x + 24f, panel.y + 170f, panel.width - 48f, 44f);
        if (GUI.Button(throwableAction, "+1 Шумовой маячок и в активный слот", ImguiTheme.Button))
        {
            RunState.AddCraftedItem(CraftedItemId.NoiseBeacon, 1);
            player.SetQuickSlot(player.SelectedQuickSlotIndex, CraftedItemId.NoiseBeacon);
            status = $"Добавлено: {RunState.CraftedItemName(CraftedItemId.NoiseBeacon)} x1. Активный слот готов к броску.";
        }

        if (!string.IsNullOrEmpty(status))
        {
            GUI.Label(new Rect(panel.x + 24f, panel.y + 228f, panel.width - 48f, 42f), status, ImguiTheme.Hint);
        }

        GUI.Label(new Rect(panel.x + 24f, panel.yMax - 42f, panel.width - 160f, 22f),
            "Esc, ё или \\ — закрыть", ImguiTheme.Hint);
        if (GUI.Button(new Rect(panel.xMax - 112f, panel.yMax - 48f, 88f, 30f), "Закрыть", ImguiTheme.Button))
        {
            Close();
        }
    }
}
