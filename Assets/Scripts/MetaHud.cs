using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MetaHud : MonoBehaviour
{
    public const float GameplayTopInset = 184f;
    public const float GameplayBottomInset = 282f;

    static readonly Vector2 Top = new Vector2(.5f, 1);
    static readonly Vector2 Bottom = new Vector2(.5f, 0);
    static readonly Vector2 Center = new Vector2(.5f, .5f);
    static readonly Color Sky = new Color32(193, 230, 250, 255);
    static readonly float[] MapOffsets = { -184, 168, -258, 184, -115, 28, 210, -128, 140, -122 };

    GameManager gm;
    LevelHud gameplay;
    RectTransform safe, hud, modal, splash, tip;
    RectTransform carHint;
    CanvasGroup splashGroup;
    readonly Image[] loadingChevrons = new Image[3];
    Text levelNumber, moves, coins, undoLabel, tipLabel;
    Button undo, hint, dailyButton;
    Image rewardMarker;
    GameObject page;
    int chapterPage;
    Rect lastSafeArea;
    Vector2Int lastScreen;
    GraphicRaycaster raycaster;
    readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    PointerEventData pointer;
    int pointerFrame = -1;

    public bool PointerHitsUi()
    {
        if (pointerFrame == Time.frameCount)
            return pointerHits.Count > 0;
        pointerFrame = Time.frameCount;
        pointerHits.Clear();
        if (EventSystem.current == null || raycaster == null)
            return false;
        if (pointer == null)
            pointer = new PointerEventData(EventSystem.current);
        pointer.position = Input.mousePosition;
        raycaster.Raycast(pointer, pointerHits);
        return pointerHits.Count > 0;
    }

    public bool BlocksTraffic => (modal != null && modal.gameObject.activeSelf) ||
        (splash != null && splash.gameObject.activeSelf);

    void Start()
    {
        gm = GetComponent<GameManager>();
        gameplay = GetComponent<LevelHud>();
        var root = new GameObject("Traffic Jam Escape UI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        raycaster = root.GetComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

        safe = Layer(root.transform, "Safe area");
        UpdateSafeArea();
        hud = Layer(safe, "Driving HUD");
        BuildHud();
        modal = Layer(root.transform, "Dialog");
        modal.gameObject.SetActive(false);
        BuildSplash(root.transform);

        gm.LevelLoaded += RefreshLevel;
        gm.OnCarEscape += RefreshTraffic;
        gm.OnCarClick += RefreshMoves;
        gm.CoinsAdded += RefreshCoins;
        gm.OnLevelFinished += ShowWin;
        gm.OnLose += ShowLoss;
    }

    void OnDestroy()
    {
        if (gm == null)
            return;
        gm.LevelLoaded -= RefreshLevel;
        gm.OnCarEscape -= RefreshTraffic;
        gm.OnCarClick -= RefreshMoves;
        gm.CoinsAdded -= RefreshCoins;
        gm.OnLevelFinished -= ShowWin;
        gm.OnLose -= ShowLoss;
    }

    void UpdateSafeArea()
    {
        Rect area = Screen.safeArea;
        var resolution = new Vector2Int(Screen.width, Screen.height);
        if (area == lastSafeArea && resolution == lastScreen)
            return;
        lastSafeArea = area;
        lastScreen = resolution;
        if (resolution.x <= 0 || resolution.y <= 0)
            return;
        if (area.width <= 0 || area.height <= 0)
            area = new Rect(0, 0, resolution.x, resolution.y);
        ApplySafeArea(safe, area, resolution);

        Camera camera = Camera.main;
        if (camera != null && gm.CurrentLevelInfo != null)
        {
            camera.fieldOfView = gm.CurrentLevelInfo.cameraFieldOfView;
            LevelInfo.FitCarsAboveControls(camera, gm.LevelCars);
        }
    }

    static void ApplySafeArea(RectTransform rect, Rect area, Vector2Int resolution)
    {
        rect.anchorMin = new Vector2(area.xMin / resolution.x, area.yMin / resolution.y);
        rect.anchorMax = new Vector2(area.xMax / resolution.x, area.yMax / resolution.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void BuildHud()
    {
        var level = Surface(hud, "Level pill", Top, -356, -108, 304, 106);
        levelNumber = Label(level, "", 51, TrafficUi.Ink, Center, 0, 5, 290, 88);
        levelNumber.resizeTextForBestFit = true;
        levelNumber.resizeTextMinSize = 32;
        levelNumber.resizeTextMaxSize = 51;
        var movePill = Surface(hud, "Moves pill", Top, 0, -108, 312, 106);
        moves = Label(movePill, "", 51, TrafficUi.Ink, Center, 0, 5, 300, 88);
        moves.resizeTextForBestFit = true;
        moves.resizeTextMinSize = 32;
        moves.resizeTextMaxSize = 51;

        var wallet = TrafficUi.Place(hud, "Daily reward wallet", Top, 354, -108, 290, 106);
        dailyButton = TrafficUi.Button(wallet, TrafficUi.Paper, CheckDaily);
        dailyButton.transition = Selectable.Transition.None;
        rewardMarker = TrafficUi.Paint(TrafficUi.Place(wallet, "Reward available", Center, 125, 38, 18, 18), TrafficUi.Blue);
        Art(wallet, "airy_coin", Center, -83, 5, 76, Color.white);
        coins = Label(wallet, "", 50, TrafficUi.Ink, Center, 37, 5, 178, 88);
        coins.resizeTextForBestFit = true;
        coins.resizeTextMinSize = 22;
        coins.resizeTextMaxSize = 50;

        undo = HudButton("Undo", "undo", -347, TrafficUi.Paper, gameplay.Undo, out undoLabel);
        hint = HudButton("Hint", "hint", 0, TrafficUi.Blue, gameplay.UseHint, out _);
        HudButton("Levels", "map", 347, TrafficUi.Paper, OpenMap, out _);

        tip = Surface(hud, "Driving tip", Bottom, 0, 43, 950, 68);
        tipLabel = Label(tip, "", 27, TrafficUi.Ink, Center, 0, 3, 910, 58);
        tip.gameObject.SetActive(false);
        hud.gameObject.SetActive(false);
    }

    Button HudButton(string title, string icon, float x, Color color, UnityEngine.Events.UnityAction click, out Text label)
    {
        var rect = TrafficUi.Place(hud, icon, Bottom, x, 176, 252, 200);
        var button = TrafficUi.Button(rect, color, click);
        Color text = color == TrafficUi.Blue ? TrafficUi.Paper : TrafficUi.Ink;
        Art(rect, icon == "hint" ? "airy_hint" : icon == "map" ? "airy_levels" : icon, Center, 0, 38, 88, text);
        label = Label(rect, title, 44, text, Center, 0, -42, 228, 72);
        return button;
    }

    void BuildSplash(Transform root)
    {
        splash = Layer(root, "Loading");
        TrafficUi.Paint(splash, new Color32(12, 39, 41, 255), null).raycastTarget = true;
        splashGroup = splash.gameObject.AddComponent<CanvasGroup>();
        var backdrop = TrafficUi.Paint(Layer(splash, "Loading backdrop"), Color.white, "toy_garage");
        backdrop.preserveAspect = false;
        var title = TrafficUi.Place(splash, "Loading artwork", Center, 0, 0, 0, 0);
        title.anchorMin = new Vector2(.19f, .66f);
        title.anchorMax = new Vector2(.81f, .98f);
        TrafficUi.Paint(title, Color.white, "traffic_title");

        var rail = TrafficUi.Place(splash, "Loading rail", Bottom, 0, 225, 850, 104);
        TrafficUi.Paint(rail, new Color32(62, 207, 190, 255), "airy_white").pixelsPerUnitMultiplier = 1.4f;
        var lane = TrafficUi.Place(rail, "Loading track", Center, 0, 3, 818, 76);
        TrafficUi.Paint(lane, new Color32(10, 53, 59, 255), "airy_white").pixelsPerUnitMultiplier = 2f;
        var fill = TrafficUi.Place(lane, "Amber loading strip", Center, -126, 2, 544, 61);
        TrafficUi.Paint(fill, new Color32(255, 179, 32, 255), "airy_white").pixelsPerUnitMultiplier = 2.3f;
        Label(splash, "Loading…", 48, TrafficUi.Paper, Bottom, 0, 132, 630, 84);
        for (int i = 0; i < loadingChevrons.Length; i++)
            loadingChevrons[i] = Art(fill, "next", Center, 108 + i * 49, 1, 48, TrafficUi.Paper);
    }

    void Update()
    {
        UpdateSafeArea();
        bool loading = gm.logoScreen != null && gm.logoScreen.gameObject.activeSelf;
        splash.gameObject.SetActive(loading);
        if (loading)
        {
            splashGroup.alpha = gm.logoScreen.alpha;
            for (int i = 0; i < loadingChevrons.Length; i++)
            {
                float phase = .5f + .5f * Mathf.Cos(Time.unscaledTime * 8f - i * 1.25f);
                loadingChevrons[i].color = Color.Lerp(new Color32(255, 210, 91, 255), TrafficUi.Paper, phase);
            }
        }
        hud.gameObject.SetActive(gm.CurrentLevelInstance != null && !loading);
        undo.interactable = gm.CanUndo;
        hint.interactable = gm.CanPlay && gm.CarsInLevel.Count > 0;
    }

    void RefreshLevel()
    {
        CloseDialog();
        ShowTip(null);
        levelNumber.text = "Level " + gm.currentLevel;
        RefreshMoves();
        RefreshTraffic();
        RefreshCoins();
        RefreshDailyMarker();
    }

    void RefreshMoves() => moves.text = "Moves " + gm.MovesTaken;

    void RefreshTraffic()
    {
        undoLabel.text = gm.UndosLeft < gm.maxUndosPerLevel ? "Undo " + gm.UndosLeft : "Undo";
    }

    void RefreshCoins() => coins.text = gm.playerCoins.ToString();

    public void ShowTip(string message)
    {
        tipLabel.text = message ?? "";
        tip.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public bool ShowCarHint(Car car)
    {
        var camera = Camera.main;
        if (camera == null)
            return false;

        var roof = car.transform.Find(CartoonModels.CarModelName + "/Roof sign");
        Vector3 screen = camera.WorldToScreenPoint(roof != null ? roof.position : car.transform.position);
        if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, screen, null, out Vector2 point))
            return false;

        ClearCarHint();
        float side = point.x < hud.rect.center.x ? 1 : -1;
        float x = Mathf.Clamp(point.x + side * 72, hud.rect.xMin + 53, hud.rect.xMax - 53);
        float y = Mathf.Clamp(point.y + 34, hud.rect.yMin + GameplayBottomInset + 45,
            hud.rect.yMax - GameplayTopInset - 45);
        carHint = TrafficUi.Place(hud, "Hint marker", Center, x, y, 94, 94);
        TrafficUi.Paint(carHint, TrafficUi.Gold, "airy_white");
        Shadow(carHint, .45f, -5);
        var pointer = Art(carHint, side > 0 ? "previous" : "next", Center, -side * 43, -21, 44, TrafficUi.Gold);
        pointer.transform.SetAsFirstSibling();
        Art(carHint, "airy_hint", Center, 0, 3, 60, TrafficUi.Ink);
        carHint.localScale = Vector3.one * .82f;
        carHint.DOScale(1.08f, .38f).SetEase(Ease.InOutSine).SetLoops(8, LoopType.Yoyo)
            .SetUpdate(true).SetLink(carHint.gameObject);
        Destroy(carHint.gameObject, 3.2f);
        return true;
    }

    public void ClearCarHint()
    {
        if (carHint == null)
            return;
        carHint.gameObject.SetActive(false);
        Destroy(carHint.gameObject);
        carHint = null;
    }

    RectTransform OpenPage(string name, Color background)
    {
        CloseDialog();
        var layer = Layer(modal, name);
        page = layer.gameObject;
        TrafficUi.Paint(layer, background, null).raycastTarget = true;
        modal.gameObject.SetActive(true);
        return layer;
    }

    RectTransform OpenCard(string name, float height)
    {
        var layer = OpenPage(name, new Color32(46, 62, 100, 108));
        var card = TrafficUi.Place(layer, "Card", Center, 0, 0, 932, height);
        TrafficUi.Paint(card, Color.white, "airy_card");
        card.localScale = Vector3.one * .95f;
        card.DOScale(1f, .22f).SetEase(Ease.OutBack, 1.1f).SetUpdate(true).SetLink(card.gameObject);
        return card;
    }

    void CloseDialog()
    {
        if (page != null)
        {
            page.SetActive(false);
            Destroy(page);
            page = null;
        }
        modal.gameObject.SetActive(false);
    }

    void OpenMap()
    {
        chapterPage = gm.ChapterOf(gm.currentLevel);
        BuildMap();
    }

    void BuildMap()
    {
        int lastLevel = Mathf.Max(gm.TotalLevels, Mathf.Max(gm.currentLevel, gm.MaxUnlockedLevel));
        int maxChapter = gm.ChapterOf(lastLevel);
        int pageSize = Mathf.Max(1, gm.chapterSize);
        int first = (chapterPage - 1) * pageSize + 1;
        int count = Mathf.Min(pageSize, lastLevel - first + 1);
        var layer = OpenPage("Level map", Sky);
        var background = TrafficUi.Paint(Layer(layer, "Illustrated park"), Color.white, "airy_map");
        background.preserveAspect = false;
        var inner = Layer(layer, "Map safe area");
        ApplySafeArea(inner, lastSafeArea.width > 0 ? lastSafeArea : new Rect(0, 0, Screen.width, Screen.height), lastScreen);
        Label(inner, "Choose a level", 95, TrafficUi.Ink, Top, 0, -148, 960, 130);
        Label(inner, ChapterArt.Name(chapterPage), 38, TrafficUi.Ink, Top, 0, -246, 560, 54);
        IconButton(inner, "Previous district", "previous", Top, -387, -240,
            () => { chapterPage--; BuildMap(); }).interactable = chapterPage > 1;
        IconButton(inner, "Next district", "next", Top, 387, -240,
            () => { chapterPage++; BuildMap(); }).interactable = chapterPage < maxChapter;

        var viewport = Layer(inner, "Level path viewport");
        viewport.offsetMin = new Vector2(25, 225);
        viewport.offsetMax = new Vector2(-25, -304);
        TrafficUi.Paint(viewport, Color.clear, null).raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        const float spacing = 187;
        const float topPadding = 120;
        float contentHeight = topPadding * 2 + Mathf.Max(0, count - 1) * spacing;
        var content = TrafficUi.Place(viewport, "Level path content", Top, 0, 0, 940, contentHeight);
        content.pivot = Top;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 80;

        var points = new List<Vector2> { new Vector2(MapOffsets[0] + 37, -12) };
        for (int index = 0; index < count; index++)
            points.Add(new Vector2(MapOffsets[index % MapOffsets.Length], -topPadding - index * spacing));
        points.Add(new Vector2(MapOffsets[(count - 1) % MapOffsets.Length] - 100, -contentHeight + 4));
        AddPath(content, points, new Color32(145, 177, 205, 140), -9, 41);
        AddPath(content, points, TrafficUi.Paper, 0, 37);

        for (int index = 0; index < count; index++)
            BuildNode(content, first + index, points[index + 1]);

        bool fromWin = gm.level_complete_UI.activeSelf;
        bool fromLoss = gm.level_lose_UI.activeSelf;
        ActionButton(inner, "Back", TrafficUi.Paper, Bottom, 0, 132, 388, 130, () =>
        {
            if (fromWin)
                ShowWin();
            else if (fromLoss)
                ShowLoss();
            else
                CloseDialog();
        }, "previous");
        IconButton(inner, "Restart level", "restart", Bottom, -372, 132, () =>
        {
            CloseDialog();
            gm.RestartLevel();
        });
        Image sound = null;
        var soundButton = IconButton(inner, "Sound", gm.isSoundOn ? "sound" : "mute", Bottom, 372, 132, () =>
        {
            UIManager.Instance.SoundClick();
            TrafficUi.SetArt(sound, gm.isSoundOn ? "sound" : "mute");
        });
        sound = soundButton.transform.GetChild(0).GetComponent<Image>();

        Canvas.ForceUpdateCanvases();
        float scrollable = Mathf.Max(0, contentHeight - viewport.rect.height);
        if (scrollable > 0 && chapterPage == gm.ChapterOf(gm.currentLevel))
            scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((gm.currentLevel - first - 3) * spacing / scrollable);
        else
            scroll.verticalNormalizedPosition = 1;
    }

    static void AddPath(RectTransform parent, List<Vector2> points, Color color, float y, float width)
    {
        var rect = Layer(parent, "Winding path");
        rect.pivot = Top;
        rect.anchoredPosition = new Vector2(0, y);
        var trail = rect.gameObject.AddComponent<LevelMapTrail>();
        trail.points = points;
        trail.width = width;
        trail.color = color;
        trail.raycastTarget = false;
        trail.SetVerticesDirty();
    }

    void BuildNode(RectTransform parent, int level, Vector2 position)
    {
        bool current = level == gm.currentLevel;
        bool open = level <= gm.MaxUnlockedLevel || current;
        float size = current ? 238 : 204;
        if (current)
        {
            var ring = TrafficUi.Place(parent, "Current level ring", Top, position.x, position.y - 2, size + 12, size + 12);
            TrafficUi.Paint(ring, TrafficUi.Paper, "airy_locked");
        }

        var tile = TrafficUi.Place(parent, "Level " + level, Top, position.x, position.y, size, size);
        if (open)
        {
            TrafficUi.Button(tile, TrafficUi.Blue, () =>
            {
                CloseDialog();
                gm.JumpToLevel(level);
            }, current ? "airy_current" : "airy_node");
        }
        else
        {
            TrafficUi.Paint(tile, Color.white, "airy_locked");
            Shadow(tile, .15f, -9);
        }
        Color text = current ? TrafficUi.Paper : open ? TrafficUi.Ink : new Color32(84, 91, 117, 255);
        var number = Label(tile, level.ToString(), current ? 91 : 82, text, Center, 0, 35, size - 18, 130);
        number.verticalOverflow = VerticalWrapMode.Overflow;
        if (current)
            Art(tile, "play", Center, 0, -44, 56, TrafficUi.Paper);
        else if (open)
            Stars(tile, PlayerPrefs.GetInt("stars_" + level), -43, 39, 40);
        else
            Art(tile, "lock", Center, 0, -39, 48, text);
    }

    void ShowWin()
    {
        var card = OpenCard("Victory", 1050);
        Art(card, "cheer", Top, 0, -99, 111, TrafficUi.Coral);
        Label(card, gm.lastLevelEndedChapter ? "District cleared!" : "Nicely done!", 96, TrafficUi.Ink,
            Top, 0, -206, 870, 138);
        for (int i = 0; i < 3; i++)
        {
            bool middle = i == 1;
            var star = Art(card, i < gm.lastLevelStars ? "airy_star" : "airy_empty_star", Center,
                (i - 1) * 218, middle ? 126 : 87, middle ? 278 : 210, Color.white);
            star.transform.localScale = Vector3.one * .5f;
            star.transform.DOScale(1, .32f).SetDelay(.09f + i * .085f)
                .SetEase(Ease.OutBack).SetUpdate(true).SetLink(star.gameObject);
        }
        Art(card, "airy_coin", Center, -198, -86, 122, Color.white);
        Label(card, "+" + gm.coinsEarnedInCurrentLevel + " coins", 66, TrafficUi.Ink, Center, 84, -86, 465, 107);
        ActionButton(card, "Next level", TrafficUi.Blue, Bottom, 0, 264, 706, 151, gm.SkipLevel);
        TextButton(card, "Level map", Bottom, 0, 116, OpenMap);
    }

    void ShowLoss()
    {
        var card = OpenCard("Retry", 870);
        Art(card, "warning", Top, 0, -132, 128, TrafficUi.Coral);
        Label(card, "Watch the crosswalk", 69, TrafficUi.Ink, Top, 0, -276, 870, 114);
        Label(card, "Wait for pedestrians to reach the curb.", 44, TrafficUi.Ink, Center, 0, 10, 760, 132);
        ActionButton(card, "Try again", TrafficUi.Blue, Bottom, 0, 246, 698, 138, gm.RestartLevel);
        TextButton(card, "Level map", Bottom, 0, 108, OpenMap);
    }

    void RefreshDailyMarker()
    {
        string today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        bool ready = string.CompareOrdinal(PlayerPrefs.GetString("daily_date"), today) < 0;
        rewardMarker.enabled = ready;
        dailyButton.interactable = ready;
        dailyButton.targetGraphic.raycastTarget = ready;
    }

    void CheckDaily()
    {
        string today = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        string last = PlayerPrefs.GetString("daily_date");
        if (string.CompareOrdinal(last, today) >= 0)
            return;
        string yesterday = DateTime.Today.AddDays(-1).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        int streak = last == yesterday ? Mathf.Clamp(PlayerPrefs.GetInt("daily_streak"), 0, 6) + 1 : 1;
        int reward = streak * 10;
        var card = OpenCard("Daily reward", 930);
        Label(card, "Daily reward", 83, TrafficUi.Ink, Top, 0, -130, 840, 120);
        Art(card, "airy_coin", Center, 0, 115, 195, Color.white);
        Label(card, "+" + reward + " coins", 66, TrafficUi.Ink, Center, 0, -45, 770, 94);
        for (int day = 1; day <= 7; day++)
        {
            Color color = day == streak ? TrafficUi.Blue : day < streak ? TrafficUi.Paper : TrafficUi.Muted;
            var stamp = Surface(card, "Day " + day, Center, (day - 4) * 109, -162, 94, 90, color);
            if (day < streak)
                Art(stamp, "check", Center, 0, 4, 46, TrafficUi.Ink);
            else
                Label(stamp, day.ToString(), 40, day == streak ? TrafficUi.Paper : TrafficUi.Ink, Center, 0, 5, 88, 65);
        }
        ActionButton(card, "Collect", TrafficUi.Blue, Bottom, 0, 126, 700, 138, () =>
        {
            PlayerPrefs.SetString("daily_date", today);
            PlayerPrefs.SetInt("daily_streak", streak);
            gm.AddCoins(reward);
            PlayerPrefs.Save();
            RefreshDailyMarker();
            CloseDialog();
        });
    }

    static RectTransform Layer(Transform parent, string name)
    {
        var rect = TrafficUi.Place(parent, name, Center, 0, 0, 0, 0);
        TrafficUi.Stretch(rect);
        return rect;
    }

    static RectTransform Surface(Transform parent, string name, Vector2 anchor, float x, float y, float w, float h,
        Color? finish = null)
    {
        var rect = TrafficUi.Place(parent, name, anchor, x, y, w, h);
        TrafficUi.Paint(rect, Color.white, TrafficUi.Finish(finish ?? TrafficUi.Paper));
        return rect;
    }

    static void Shadow(RectTransform rect, float alpha, float offset)
    {
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.16f, .21f, .35f, alpha);
        shadow.effectDistance = new Vector2(0, offset);
    }

    static Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, float x, float y, float w, float h) =>
        TrafficUi.Label(TrafficUi.Place(parent, "Label", anchor, x, y, w, h), text, size, color);

    static Image Art(Transform parent, string name, Vector2 anchor, float x, float y, float size, Color color) =>
        TrafficUi.Paint(TrafficUi.Place(parent, name, anchor, x, y, size, size), color, name);

    static Button ActionButton(Transform parent, string text, Color color, Vector2 anchor,
        float x, float y, float w, float h, UnityEngine.Events.UnityAction click, string icon = null)
    {
        var rect = TrafficUi.Place(parent, text, anchor, x, y, w, h);
        Button button = TrafficUi.Button(rect, color, click);
        Color foreground = color == TrafficUi.Blue ? TrafficUi.Paper : TrafficUi.Ink;
        if (icon != null)
            Art(rect, icon, Center, -w * .26f, 5, 64, foreground);
        Label(rect, text, 65, foreground, Center, icon == null ? 0 : w * .10f, 7, icon == null ? w - 36 : w * .66f, h - 18);
        return button;
    }

    static Button IconButton(Transform parent, string name, string icon, Vector2 anchor, float x, float y,
        UnityEngine.Events.UnityAction click)
    {
        var rect = TrafficUi.Place(parent, name, anchor, x, y, 98, 98);
        Button button = TrafficUi.Button(rect, TrafficUi.Paper, click);
        Art(rect, icon, Center, 0, 5, 48, TrafficUi.Ink);
        return button;
    }

    static void TextButton(Transform parent, string text, Vector2 anchor, float x, float y,
        UnityEngine.Events.UnityAction click)
    {
        var rect = TrafficUi.Place(parent, text, anchor, x, y, 510, 102);
        var area = TrafficUi.Paint(rect, Color.clear, null);
        area.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = area;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(click);
        Label(rect, text, 52, TrafficUi.Ink, Center, 0, 0, 490, 100);
    }

    static void Stars(Transform parent, int count, float y, float size, float spacing)
    {
        for (int i = 0; i < 3; i++)
            Art(parent, i < count ? "airy_star" : "airy_empty_star", Center, (i - 1) * spacing, y, size, Color.white);
    }
}
