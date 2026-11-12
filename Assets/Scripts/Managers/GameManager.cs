using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DG.Tweening;


public class GameManager : MonoBehaviour
{
    static GameManager instance;

    public GameObject coinPrefab;

    public int playerCoins;

    public int coinsEarnedInCurrentLevel;

    public int currentLevel;

    public bool isSoundOn = true;

    public bool isActive;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();

                if (instance == null)
                {
                    GameObject obj = new GameObject();
                    obj.name = typeof(GameManager).Name;
                    instance = obj.AddComponent<GameManager>();
                    DontDestroyOnLoad(obj);
                }
            }

            return instance;
        }
    }

    public event Action OnCarHit;
    public event Action OnCarEscape;
    public event Action OnLevelFinished;
    public event Action OnLose;
    public event Action CoinsAdded;
    public event Action LevelLoaded;
    public event Action OnCarClick;

    public List<Car> CarsInLevel;

    public int carsInitialCount;



    public GameObject level_complete_UI;
    public GameObject level_lose_UI;

    public CanvasGroup logoScreen;

    [Header("Single-scene level loading")]
    [Tooltip("Levels are prefabs in a Resources folder named Level_<number>.")]
    public string levelsResourcePath = "Levels/Level_";

    [Tooltip("Editor testing: when > 0 this level is loaded on Play instead of the saved progress.")]
    public int editorStartLevel;

    [Tooltip("When the player runs out of authored levels a random one from this number upwards is played.")]
    public int randomLevelsStartAt = 10;

    GameObject currentLevelInstance;
    Car[] levelCars = new Car[0];

    [Header("Gameplay")]
    public int hitsInLevel;
    public bool lastLevelPerfect;
    public int perfectBonusCoins = 2;

    public GameObject CurrentLevelInstance { get { return currentLevelInstance; } }
    public Car[] LevelCars { get { return levelCars; } }
    public LevelInfo CurrentLevelInfo { get { return currentLevelInstance != null ? currentLevelInstance.GetComponent<LevelInfo>() : null; } }
    Transform levelContainer;
    int totalLevels = -1;

    public int TotalLevels
    {
        get
        {
            if (totalLevels < 0)
            {
                totalLevels = 0;
                while (Resources.Load<GameObject>(levelsResourcePath + (totalLevels + 1)) != null)
                    totalLevels++;
            }
            return totalLevels;
        }
    }

    private void Awake()
    {
        //PlayerPrefs.SetInt("Level", 29);

        ApplyOriginalLighting();

        if (GetComponent<LevelHud>() == null)
            gameObject.AddComponent<LevelHud>();

        if (GetComponent<MetaHud>() == null)
            gameObject.AddComponent<MetaHud>();
    }

    /// <summary>
    /// Same lighting the old per-level scenes had, applied from code so the single Game scene
    /// always looks identical (independent of baked/auto-generated lighting state).
    /// </summary>
    void ApplyOriginalLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.8181766f, 0.8900104f, 1.1486982f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.1254895f, 0.39043456f, 0.5660378f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.33962262f, 0.30384475f, 0.23228905f, 1f);
        RenderSettings.ambientIntensity = 1f;

        foreach (Light l in FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional)
                continue;

            l.color = new Color(0.94706f, 1f, 0.9009434f, 1f);
            l.intensity = 1.1f;
            l.shadows = LightShadows.Soft;
            RenderSettings.sun = l;
        }

        DynamicGI.UpdateEnvironment();
    }

    void Start()
    {
        // The logo screen is visible while the first level loads.
        Invoke("InitGame", 1.5f);
    }

    void InitGame()
    {
        currentLevel = PlayerPrefs.GetInt("Level");
        if (currentLevel == 0)
        {
            currentLevel = 1;

            PlayerPrefs.SetInt("Level", currentLevel);
        }

#if UNITY_EDITOR
        if (editorStartLevel > 0)
            currentLevel = editorStartLevel;
#endif

        LoadLevel(currentLevel);
    }

    public void CarClicked()
    {
        OnCarClick?.Invoke();
    }

    public void CarHit()
    {
        hitsInLevel++;
        OnCarHit?.Invoke();
    }

    [Header("Chapters & undo")]
    public int chapterSize = 10;
    public int chapterBonusCoins = 5;
    public int maxUndosPerLevel = 3;
    public int undosUsed;
    public int hintsUsed;
    public int lastLevelStars;
    public bool lastLevelEndedChapter;

    readonly List<Car> escapedCars = new List<Car>();
    readonly List<GameObject> escapedCoins = new List<GameObject>();

    public int ChapterOf(int level) { return (Mathf.Max(1, level) - 1) / Mathf.Max(1, chapterSize) + 1; }
    public int LevelInChapter(int level) { return (Mathf.Max(1, level) - 1) % Mathf.Max(1, chapterSize) + 1; }
    public bool CanUndo { get { return escapedCars.Count > 0 && undosUsed < maxUndosPerLevel && !AnyCarMoving(); } }
    public int UndosLeft { get { return Mathf.Max(0, maxUndosPerLevel - undosUsed); } }

    bool AnyCarMoving()
    {
        foreach (Car c in CarsInLevel)
            if (c != null && c.IsMoving)
                return true;
        return false;
    }

    /// <summary>Brings the most recently escaped car back to its start spot.</summary>
    public bool UndoLastEscape()
    {
        if (!CanUndo)
            return false;

        CancelInvoke("DestroyCar");

        int i = escapedCars.Count - 1;
        Car car = escapedCars[i];
        GameObject coin = escapedCoins[i];
        escapedCars.RemoveAt(i);
        escapedCoins.RemoveAt(i);

        if (coin != null)
            Destroy(coin);

        if (car != null)
        {
            car.ResetToStart();
            if (!CarsInLevel.Contains(car))
                CarsInLevel.Add(car);
        }

        undosUsed++;
        OnCarEscape?.Invoke(); // refreshes the HUD counter
        return true;
    }

    GameObject carEscaped;
    public void CarEscape(GameObject car)
    {
        carEscaped = car;

        // spawn coin
        GameObject coinObj = Instantiate(coinPrefab, car.transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity,
            currentLevelInstance != null ? currentLevelInstance.transform : null);

        Car carComp = car.GetComponent<Car>();
        CarsInLevel.Remove(carComp);
        escapedCars.Add(carComp);
        escapedCoins.Add(coinObj);

        if(CarsInLevel.Count <= 0)
        {
            coinsEarnedInCurrentLevel = carsInitialCount + 3;

            // finishing the last level of a chapter pays a chapter bonus
            lastLevelEndedChapter = currentLevel > 0 && currentLevel % Mathf.Max(1, chapterSize) == 0;
            if (lastLevelEndedChapter)
                coinsEarnedInCurrentLevel += chapterBonusCoins;

            // clean run (no collisions) earns a bonus
            lastLevelPerfect = hitsInLevel == 0;
            if (lastLevelPerfect)
                coinsEarnedInCurrentLevel += perfectBonusCoins;

            // 3 stars = clean, unaided run; each kind of mistake/aid costs one star
            lastLevelStars = Mathf.Clamp(3 - (hitsInLevel > 0 ? 1 : 0) - ((undosUsed + hintsUsed) > 0 ? 1 : 0), 1, 3);
            if (lastLevelStars > PlayerPrefs.GetInt("stars_" + currentLevel, 0))
                PlayerPrefs.SetInt("stars_" + currentLevel, lastLevelStars);

            LevelFinished();

            return;
        }

        Invoke("DestroyCar", 0.4f);

        OnCarEscape?.Invoke();
    }

    void DestroyCar()
    {
        // kept (inactive) instead of destroyed so Undo can bring it back
        if (carEscaped != null)
            carEscaped.SetActive(false);
    }

    public void UpdateCoins(int amout)
    {
        playerCoins = amout;

        PlayerPrefs.SetInt("playercoins", playerCoins);

        CoinsAdded?.Invoke();
    }

    public void LevelFinished()
    {
        Debug.Log("Level Finished !");

        UpdateCoins(PlayerPrefs.GetInt("playercoins"));

        level_complete_UI.SetActive(true);
        level_complete_UI.GetComponent<DOTweenAnimation>().DORestartAllById("LVL_COMPLETE");

        OnLevelFinished?.Invoke();
    }

    public void RestartLevel()
    {
        ShowLogoScreen();
    }

    public void LoadNextLevelDelay()
    {
        Invoke("LoadNextLevel", 1.25f);
    }

    public void SkipLevel()
    {
        LoadNextLevel();
    }

    void LoadNextLevel()
    {
        currentLevel++;

        // replaying an old level must never lower the saved progress
        PlayerPrefs.SetInt("Level", Mathf.Max(PlayerPrefs.GetInt("Level"), currentLevel));

        ShowLogoScreen();
    }

    public int MaxUnlockedLevel { get { return Mathf.Max(1, PlayerPrefs.GetInt("Level")); } }

    /// <summary>Jump to an unlocked level from the level-select screen.</summary>
    public void JumpToLevel(int level)
    {
        if (level < 1 || level > MaxUnlockedLevel)
            return;

        CancelInvoke("LoadNextLevel");
        currentLevel = level;
        ShowLogoScreen();
    }

    public void AddCoins(int amount)
    {
        UpdateCoins(PlayerPrefs.GetInt("playercoins") + amount);
    }

    void ShowLogoScreen()
    {
        // Set the initial alpha to 0
        logoScreen.alpha = 0f;

        logoScreen.gameObject.SetActive(true);

        logoScreen.DOFade(1f, 1.35f)
            .OnComplete(() => LoadLevel(currentLevel));
    }

    /// <summary>Prefab that holds the content of a level, or null when the level doesn't exist.</summary>
    GameObject FindLevelPrefab(int level)
    {
        return Resources.Load<GameObject>(levelsResourcePath + level);
    }

    public void LoadLevel(int level)
    {
        StopAllCoroutines();
        StartCoroutine(LoadLevelRoutine(level));
    }

    IEnumerator LoadLevelRoutine(int level)
    {
        // remove whatever level is currently on screen
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;

            // destroyed objects are still found until the end of the frame
            yield return null;
        }

        CarsInLevel.Clear();

        GameObject prefab = FindLevelPrefab(level);
        if (prefab == null)
        {
            // out of authored levels -> endless random levels (same behaviour the game had before)
            int total = TotalLevels;
            int from = Mathf.Clamp(randomLevelsStartAt, 1, Mathf.Max(1, total));
            prefab = FindLevelPrefab(UnityEngine.Random.Range(from, total + 1));
        }

        if (prefab == null)
        {
            Debug.LogError("No level prefabs found at Resources/" + levelsResourcePath + "<n>");
            yield break;
        }

        if (levelContainer == null)
        {
            GameObject container = GameObject.Find("LevelContainer");
            if (container == null)
                container = new GameObject("LevelContainer");

            levelContainer = container.transform;
        }

        currentLevelInstance = Instantiate(prefab, levelContainer);
        currentLevelInstance.name = prefab.name;

        LevelInfo info = currentLevelInstance.GetComponent<LevelInfo>();
        if (info != null)
            info.ApplyCamera(Camera.main);

        OnLevelReady();
    }

    void OnLevelReady()
    {
        LevelLoaded?.Invoke();

        level_complete_UI.gameObject.SetActive(false);
        level_lose_UI.gameObject.SetActive(false);

        HideLogoScreen();

        CarsInLevel.Clear();

        Car[] cars = currentLevelInstance.GetComponentsInChildren<Car>();
        foreach (Car car in cars)
        {
            CarsInLevel.Add(car);
        }

        carsInitialCount = CarsInLevel.Count;

        levelCars = cars;
        hitsInLevel = 0;
        lastLevelPerfect = false;
        lastLevelEndedChapter = false;
        undosUsed = 0;
        hintsUsed = 0;
        lastLevelStars = 0;
        escapedCars.Clear();
        escapedCoins.Clear();
    }

    void HideLogoScreen()
    {
        logoScreen.DOFade(0f, 1.35f)
            .OnComplete(() => logoScreen.gameObject.SetActive(false));
    }

    public void LevelLose()
    {
        
        level_lose_UI.GetComponent<DOTweenAnimation>().DORestartAllById("LVL_FAIL");
        level_lose_UI.SetActive(true);

        Debug.Log("YOU LOST !");


        OnLose?.Invoke();

        //CarsInLevel.Clear();
    }
}
