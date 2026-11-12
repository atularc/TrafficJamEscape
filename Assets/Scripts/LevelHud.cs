using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small gameplay helpers that build their own UI from code (no scene/prefab edits needed):
///  - "CARS LEFT" counter
///  - HINT button (highlights the next car of a valid solution)
///  - automatic hint on the first levels when the player stalls
///  - camera shake when a car crashes
///  - "PERFECT!" toast for a level cleared without a collision
/// </summary>
public class LevelHud : MonoBehaviour
{
    public float autoHintDelay = 4f;
    public int autoHintUntilLevel = 2;

    GameManager gm;
    Canvas canvas;
    Text carsLeftText;
    Button hintButton;
    Text toastText;
    Button undoButton;
    Text undoLabel;
    Text chapterText;
    Text tipText;
    Coroutine tipRoutine;

    static readonly string[] ChapterNames =
    {
        "Rookie Roads", "City Streets", "Rush Hour", "Downtown Maze", "Gridlock",
        "Night Shift", "Crosswalk Chaos", "Highway Hustle", "Traffic Tycoon", "Jam Master"
    };
    GameObject marker;
    float idleTimer;
    Tween shakeTween;

    void Start()
    {
        gm = GameManager.Instance;
        BuildUi();

        gm.LevelLoaded += OnLevelLoaded;
        gm.OnCarEscape += RefreshCounter;
        gm.OnCarHit += OnCarHit;
        gm.OnLevelFinished += OnLevelFinished;
    }

    void OnDestroy()
    {
        if (gm == null)
            return;

        gm.LevelLoaded -= OnLevelLoaded;
        gm.OnCarEscape -= RefreshCounter;
        gm.OnCarHit -= OnCarHit;
        gm.OnLevelFinished -= OnLevelFinished;
    }

    // ------------------------------------------------------------------ UI

    void BuildUi()
    {
        var go = new GameObject("LevelHud_Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1; // below the popups
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        carsLeftText = MakeText("CarsLeft", font, 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 235f), new Vector2(600f, 80f));
        carsLeftText.color = Color.white;
        AddOutline(carsLeftText.gameObject);

        // hint button
        var btnGo = new GameObject("HintButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)btnGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(320f, 110f);
        var img = btnGo.GetComponent<Image>();
        img.color = new Color(1f, 0.78f, 0.1f, 1f);
        hintButton = btnGo.GetComponent<Button>();
        hintButton.targetGraphic = img;
        hintButton.onClick.AddListener(OnHintButton);

        Text label = MakeText("Label", font, 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 110f), btnGo.transform);
        label.text = "HINT";
        label.color = new Color(0.15f, 0.1f, 0f, 1f);
        label.fontStyle = FontStyle.Bold;

        // undo button (bottom left of the hint button)
        var undoGo = new GameObject("UndoButton", typeof(RectTransform), typeof(Image), typeof(Button));
        undoGo.transform.SetParent(go.transform, false);
        var urt = (RectTransform)undoGo.transform;
        urt.anchorMin = urt.anchorMax = new Vector2(0.5f, 0f);
        urt.pivot = new Vector2(0.5f, 0.5f);
        urt.anchoredPosition = new Vector2(-340f, 120f);
        urt.sizeDelta = new Vector2(320f, 110f);
        var uimg = undoGo.GetComponent<Image>();
        uimg.color = new Color(0.35f, 0.7f, 1f, 1f);
        undoButton = undoGo.GetComponent<Button>();
        undoButton.targetGraphic = uimg;
        undoButton.onClick.AddListener(DoUndo);
        undoLabel = MakeText("Label", font, 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 110f), undoGo.transform);
        undoLabel.fontStyle = FontStyle.Bold;
        undoLabel.color = new Color(0.05f, 0.12f, 0.25f, 1f);

        // chapter label + tutorial tip banner
        chapterText = MakeText("Chapter", font, 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(900f, 60f));
        chapterText.color = Color.white;
        AddOutline(chapterText.gameObject);

        tipText = MakeText("Tip", font, 48, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -560f), new Vector2(940f, 160f));
        tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tipText.color = new Color(1f, 1f, 0.85f, 1f);
        AddOutline(tipText.gameObject);
        tipText.gameObject.SetActive(false);

        toastText = MakeText("Toast", font, 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1000f, 200f));
        toastText.color = new Color(1f, 0.9f, 0.2f, 1f);
        toastText.fontStyle = FontStyle.Bold;
        AddOutline(toastText.gameObject);
        toastText.gameObject.SetActive(false);

        SetHudVisible(false);
    }

    Text MakeText(string name, Font font, int size, TextAnchor anchor, Vector2 anchorPos, Vector2 pos, Vector2 sizeDelta, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent != null ? parent : canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchorPos;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void AddOutline(GameObject go)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f);
        o.effectDistance = new Vector2(3f, -3f);
    }

    void SetHudVisible(bool visible)
    {
        if (carsLeftText != null) carsLeftText.gameObject.SetActive(visible);
        if (hintButton != null) hintButton.gameObject.SetActive(visible);
        if (undoButton != null) undoButton.gameObject.SetActive(visible);
        if (chapterText != null) chapterText.gameObject.SetActive(visible);
    }

    void DoUndo()
    {
        if (gm.UndoLastEscape())
        {
            ClearMarker();
            idleTimer = 0f;
            RefreshCounter();
        }
    }

    string ChapterName(int chapter)
    {
        return ChapterNames[(chapter - 1) % ChapterNames.Length];
    }

    // ---- tutorial tips: shown once, only when the situation first appears
    void ShowTip(string key, string message)
    {
        string pref = "tip_" + key;
        if (PlayerPrefs.GetInt(pref, 0) == 1)
            return;
        PlayerPrefs.SetInt(pref, 1);

        if (tipRoutine != null)
            StopCoroutine(tipRoutine);
        tipRoutine = StartCoroutine(TipRoutine(message));
    }

    IEnumerator TipRoutine(string message)
    {
        yield return new WaitForSeconds(1.8f); // let the level fade in first
        tipText.text = message;
        tipText.gameObject.SetActive(true);
        yield return new WaitForSeconds(5f);
        tipText.gameObject.SetActive(false);
        tipRoutine = null;
    }

    void ShowLevelTips()
    {
        if (tipRoutine != null)
        {
            StopCoroutine(tipRoutine);
            tipRoutine = null;
        }
        tipText.gameObject.SetActive(false);

        if (gm.currentLevel == 1)
            ShowTip("first", "Tap a car to send it out of the map.");
        else if (gm.currentLevel == 2)
            ShowTip("blocked", "A car that hits another car rolls back. Clear the blocking car first!");

        bool hasTurner = false;
        foreach (Car c in gm.CarsInLevel)
            if (c != null && c.direction != Direction.Forward) { hasTurner = true; break; }
        if (hasTurner && gm.currentLevel > 2)
            ShowTip("turn", "Some cars turn at the first free road. Check where they will go.");

        if (gm.CurrentLevelInstance != null && gm.CurrentLevelInstance.GetComponentInChildren<Pedestrian>() != null)
            ShowTip("ped", "Pedestrian ahead! Hitting one loses the level. Wait for the red signal.");

        if (gm.currentLevel == 5)
            ShowTip("hint", "Stuck? Use HINT, and UNDO brings back the last car that left.");
    }

    // ------------------------------------------------------------------ events

    void OnLevelLoaded()
    {
        ClearMarker();
        idleTimer = 0f;
        toastText.gameObject.SetActive(false);
        SetHudVisible(true);
        RefreshCounter();

        // OnLevelReady fills CarsInLevel right after raising LevelLoaded, so wait a frame
        StartCoroutine(AfterLevelReady());
    }

    IEnumerator AfterLevelReady()
    {
        yield return null;
        RefreshCounter();
        ShowLevelTips();

        if (gm.LevelInChapter(gm.currentLevel) == 1)
            StartCoroutine(ShowToast("CHAPTER " + gm.ChapterOf(gm.currentLevel) + "\n" + ChapterName(gm.ChapterOf(gm.currentLevel)), 2.2f, 70));
    }

    void RefreshCounter()
    {
        if (carsLeftText != null && gm != null)
            carsLeftText.text = "CARS LEFT  " + gm.CarsInLevel.Count + " / " + gm.carsInitialCount;

        if (chapterText != null && gm != null)
            chapterText.text = "CHAPTER " + gm.ChapterOf(gm.currentLevel) + " · " + ChapterName(gm.ChapterOf(gm.currentLevel))
                             + "  (" + gm.LevelInChapter(gm.currentLevel) + "/" + gm.chapterSize + ")";

        if (undoButton != null && gm != null)
        {
            undoButton.interactable = gm.CanUndo;
            undoLabel.text = "UNDO " + gm.UndosLeft;
        }
    }

    void OnCarHit()
    {
        idleTimer = 0f;
        if (Camera.main == null)
            return;

        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Complete();

        shakeTween = Camera.main.transform.DOShakePosition(0.25f, 0.07f, 22, 90f, false, true);
    }

    void OnLevelFinished()
    {
        SetHudVisible(false);
        ClearMarker();

        if (gm.lastLevelEndedChapter)
            StartCoroutine(ShowToast("CHAPTER COMPLETE!  +" + gm.chapterBonusCoins, 2.2f, 80));
        else if (gm.lastLevelPerfect)
            StartCoroutine(ShowToast("PERFECT!  +" + gm.perfectBonusCoins + "\n" + new string('*', gm.lastLevelStars) + "  " + gm.lastLevelStars + " STARS", 1.6f, 90));
        else
            StartCoroutine(ShowToast(gm.lastLevelStars + " STARS", 1.4f, 100));
    }

    IEnumerator ShowToast(string message, float seconds = 1.6f, int size = 110)
    {
        toastText.fontSize = size;
        toastText.text = message;
        toastText.gameObject.SetActive(true);
        toastText.transform.localScale = Vector3.zero;
        toastText.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(seconds);
        toastText.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ hints

    void Update()
    {
        if (gm == null || gm.currentLevel == 0 || gm.CarsInLevel.Count == 0)
            return;

        bool popupOpen = (gm.level_complete_UI != null && gm.level_complete_UI.activeSelf)
                      || (gm.level_lose_UI != null && gm.level_lose_UI.activeSelf);
        if (popupOpen)
        {
            SetHudVisible(false);
            return;
        }

        if (undoButton != null)
            undoButton.interactable = gm.CanUndo;

        if (hintButton != null && !hintButton.gameObject.activeSelf && gm.logoScreen != null && !gm.logoScreen.gameObject.activeSelf)
            SetHudVisible(true);

        // auto hint on the first levels when nothing happens for a while
        if (gm.currentLevel <= autoHintUntilLevel)
        {
            if (AnyCarMoving() || Input.GetMouseButton(0))
                idleTimer = 0f;
            else
            {
                idleTimer += Time.deltaTime;
                if (idleTimer >= autoHintDelay)
                {
                    idleTimer = 0f;
                    ShowHint();
                }
            }
        }
    }

    bool AnyCarMoving()
    {
        foreach (Car c in gm.CarsInLevel)
            if (c != null && c.IsMoving)
                return true;
        return false;
    }

    /// <summary>First car, in the stored solution order, that is still on the road.</summary>
    Car NextHintCar()
    {
        LevelInfo info = gm.CurrentLevelInfo;
        Car[] cars = gm.LevelCars;

        if (info != null && info.solution != null)
        {
            foreach (int idx in info.solution)
            {
                if (idx < 0 || idx >= cars.Length)
                    continue;

                Car c = cars[idx];
                if (c != null && gm.CarsInLevel.Contains(c))
                    return c;
            }
        }

        // no stored solution: any remaining car
        foreach (Car c in gm.CarsInLevel)
            if (c != null)
                return c;

        return null;
    }

    void OnHintButton()
    {
        if (NextHintCar() != null)
            gm.hintsUsed++;
        ShowHint();
    }

    public void ShowHint()
    {
        Car car = NextHintCar();
        if (car == null)
            return;

        ClearMarker();

        car.transform.DOKill(true);
        car.transform.DOPunchScale(Vector3.one * 0.3f, 0.55f, 4, 0.6f).SetLoops(4);

        marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "HintMarker";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(car.transform.parent, true);
        marker.transform.localScale = Vector3.one * 0.22f;
        marker.transform.position = car.transform.position + Vector3.up * 1.0f;
        var rend = marker.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.15f, 0.15f, 1f) };
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marker.transform.DOMoveY(marker.transform.position.y + 0.25f, 0.35f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        Destroy(marker, 3f);
    }

    void ClearMarker()
    {
        if (marker != null)
        {
            marker.transform.DOKill();
            Destroy(marker);
        }
    }
}
