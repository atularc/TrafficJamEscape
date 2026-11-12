using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Meta-game UI built from code: daily reward + level select map (chapters, stars, locked levels).
/// </summary>
public class MetaHud : MonoBehaviour
{
    static readonly Color Panel = new Color(0.07f, 0.12f, 0.22f, 0.96f);
    static readonly Color Accent = new Color(1f, 0.78f, 0.1f, 1f);
    static readonly Color Teal = new Color(0.2f, 0.75f, 0.7f, 1f);
    static readonly Color Locked = new Color(0.25f, 0.3f, 0.4f, 1f);

    GameManager gm;
    Canvas canvas;
    Font font;
    Button levelsButton;
    GameObject selectPanel;
    RectTransform content;
    GameObject dailyPanel;
    int pendingReward;

    void Start()
    {
        gm = GameManager.Instance;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
        BuildLevelsButton();
        Invoke("CheckDailyReward", 3f);
    }

    void BuildCanvas()
    {
        var go = new GameObject("MetaHud_Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            es.transform.SetParent(transform, false);
        }
    }

    // ------------------------------------------------------------ helpers

    Image MakeImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    Text MakeLabel(Transform parent, string text, int size, Color color, FontStyle style = FontStyle.Bold)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.raycastTarget = false;
        return t;
    }

    Button MakeButton(string name, Transform parent, string label, Color color, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
    {
        Image img = MakeImage(name, parent, color, anchor, anchor, pos, size);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        MakeLabel(img.transform, label, fontSize, new Color(0.1f, 0.08f, 0f, 1f));
        return b;
    }

    // ------------------------------------------------------------ levels button

    void BuildLevelsButton()
    {
        levelsButton = MakeButton("LevelsButton", canvas.transform, "LEVELS", Teal, new Vector2(0.5f, 0f), new Vector2(340f, 120f), new Vector2(320f, 110f), 44);
        levelsButton.onClick.AddListener(OpenSelect);
        levelsButton.gameObject.SetActive(false);
    }

    void Update()
    {
        if (gm == null || levelsButton == null)
            return;

        bool popup = (gm.level_complete_UI != null && gm.level_complete_UI.activeSelf)
                  || (gm.level_lose_UI != null && gm.level_lose_UI.activeSelf);
        bool logo = gm.logoScreen != null && gm.logoScreen.gameObject.activeSelf;
        bool show = gm.currentLevel > 0 && !popup && !logo && !(selectPanel != null && selectPanel.activeSelf)
                 && !(dailyPanel != null && dailyPanel.activeSelf);
        if (levelsButton.gameObject.activeSelf != show)
            levelsButton.gameObject.SetActive(show);
    }

    // ------------------------------------------------------------ level select

    void OpenSelect()
    {
        if (selectPanel == null)
            BuildSelect();

        RefreshSelect();
        selectPanel.SetActive(true);
    }

    void BuildSelect()
    {
        Image bg = MakeImage("LevelSelect", canvas.transform, Panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var brt = (RectTransform)bg.transform;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        selectPanel = bg.gameObject;

        Text title = MakeLabel(selectPanel.transform, "SELECT LEVEL", 80, Accent);
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -90f);
        trt.sizeDelta = new Vector2(0f, 120f);

        Button close = MakeButton("Close", selectPanel.transform, "CLOSE", Accent, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 120f), 54);
        close.onClick.AddListener(() => selectPanel.SetActive(false));

        // scroll area
        Image view = MakeImage("Viewport", selectPanel.transform, new Color(0, 0, 0, 0.01f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var vrt = (RectTransform)view.transform;
        vrt.offsetMin = new Vector2(40f, 260f);
        vrt.offsetMax = new Vector2(-40f, -240f);
        view.gameObject.AddComponent<RectMask2D>();

        var cgo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        cgo.transform.SetParent(view.transform, false);
        content = (RectTransform)cgo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var grid = cgo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170f, 170f);
        grid.spacing = new Vector2(20f, 20f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(10, 10, 10, 10);
        cgo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;
    }

    void RefreshSelect()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        int max = gm.MaxUnlockedLevel;
        int total = Mathf.Max(gm.TotalLevels, max);

        for (int n = 1; n <= total; n++)
        {
            bool open = n <= max;
            int stars = PlayerPrefs.GetInt("stars_" + n, 0);
            bool current = n == gm.currentLevel;

            Color c = !open ? Locked : current ? Accent : (stars >= 3 ? new Color(0.35f, 0.85f, 0.45f, 1f) : Teal);
            Image img = MakeImage("L" + n, content, c, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            string label = open ? n.ToString() + (stars > 0 ? "\n" + new string('*', stars) : "") : n.ToString();
            MakeLabel(img.transform, label, 48, open ? new Color(0.08f, 0.1f, 0.15f, 1f) : new Color(0.5f, 0.55f, 0.65f, 1f));

            if (open)
            {
                int level = n;
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() =>
                {
                    selectPanel.SetActive(false);
                    gm.JumpToLevel(level);
                });
            }
        }
    }

    // ------------------------------------------------------------ daily reward

    void CheckDailyReward()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        string last = PlayerPrefs.GetString("daily_date", "");
        if (last == today)
            return;

        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
        int streak = PlayerPrefs.GetInt("daily_streak", 0);
        streak = last == yesterday ? streak + 1 : 1;

        PlayerPrefs.SetString("daily_date", today);
        PlayerPrefs.SetInt("daily_streak", streak);

        pendingReward = 10 * Mathf.Min(streak, 7);
        ShowDaily(streak);
    }

    void ShowDaily(int streak)
    {
        Image bg = MakeImage("DailyReward", canvas.transform, new Color(0f, 0f, 0f, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ((RectTransform)bg.transform).offsetMin = ((RectTransform)bg.transform).offsetMax = Vector2.zero;
        dailyPanel = bg.gameObject;

        Image card = MakeImage("Card", dailyPanel.transform, Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 620f));
        Text head = MakeLabel(card.transform, "DAILY REWARD", 72, Accent);
        var hrt = (RectTransform)head.transform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -40f);
        hrt.sizeDelta = new Vector2(0f, 110f);

        Text body = MakeLabel(card.transform, "Day " + streak + " streak\n+" + pendingReward + " coins", 60, Color.white);
        ((RectTransform)body.transform).offsetMin = new Vector2(0f, 150f);
        ((RectTransform)body.transform).offsetMax = new Vector2(0f, -150f);

        Button claim = MakeButton("Claim", card.transform, "CLAIM", Accent, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 120f), 56);
        claim.onClick.AddListener(() =>
        {
            gm.AddCoins(pendingReward);
            dailyPanel.SetActive(false);
        });
    }
}
