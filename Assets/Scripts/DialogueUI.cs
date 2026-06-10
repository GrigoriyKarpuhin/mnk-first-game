using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Простой диалоговый UI, создаётся автоматически при первом использовании
/// </summary>
public class DialogueUI : MonoBehaviour
{
    private static DialogueUI instance;

    private Canvas canvas;
    private GameObject panel;
    private Text label;
    private float hideAt;

    public static DialogueUI Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("DialogueUI");
                instance = go.AddComponent<DialogueUI>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvas.transform, false);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.7f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 40f);
        panelRect.sizeDelta = new Vector2(420f, 90f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(panel.transform, false);
        label = textObj.AddComponent<Text>();
        label.text = "";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 28;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var textRect = label.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -10f);

        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel != null && panel.activeSelf && Time.time >= hideAt)
        {
            panel.SetActive(false);
        }
    }

    public void Show(string message, float duration = 1.6f)
    {
        if (panel == null || label == null)
        {
            BuildUI();
        }

        label.text = message;
        panel.SetActive(true);
        hideAt = Time.time + Mathf.Max(0.1f, duration);
    }
}
