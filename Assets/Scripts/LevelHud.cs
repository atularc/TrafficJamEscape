using System.Collections;
using DG.Tweening;
using UnityEngine;

public class LevelHud : MonoBehaviour
{
    public float autoHintDelay = 4f;
    public int autoHintUntilLevel = 2;

    GameManager gm;
    MetaHud hud;
    Coroutine tipRoutine;
    Tween shakeTween;
    float idleTimer;
    bool hintQueued;

    void Start()
    {
        gm = GetComponent<GameManager>();
        hud = GetComponent<MetaHud>();
        gm.LevelLoaded += OnLevelLoaded;
        gm.OnCarHit += OnCarHit;
        gm.OnCarClick += ClearMarker;
        gm.OnLevelFinished += ClearMarker;
        gm.OnLose += ClearMarker;
    }

    void OnDestroy()
    {
        if (gm != null)
        {
            gm.LevelLoaded -= OnLevelLoaded;
            gm.OnCarHit -= OnCarHit;
            gm.OnCarClick -= ClearMarker;
            gm.OnLevelFinished -= ClearMarker;
            gm.OnLose -= ClearMarker;
        }
        shakeTween?.Kill();
        ClearMarker();
    }

    void OnLevelLoaded()
    {
        hintQueued = false;
        ClearMarker();
        idleTimer = 0;
        if (tipRoutine != null)
            StopCoroutine(tipRoutine);
        tipRoutine = StartCoroutine(ShowTips());
    }

    IEnumerator ShowTips()
    {
        yield return new WaitUntil(() => gm.CanPlay && !hud.BlocksTraffic);
        string key;
        string message;
        bool hasPedestrian = gm.CurrentLevelInstance.GetComponentInChildren<Pedestrian>() != null;
        if (gm.currentLevel == 1)
        {
            key = "first";
            message = "Tap a car to send it along the road.";
        }
        else if (gm.currentLevel == 2)
        {
            key = "blocked";
            message = "Clear the blocking car first. A car rolls back after a collision.";
        }
        else if (hasPedestrian && PlayerPrefs.GetInt("tip_ped") == 0)
        {
            key = "ped";
            message = "Wait for the crosswalk to clear before tapping a car.";
        }
        else if (gm.currentLevel == 5)
        {
            key = "hint";
            message = "Use Hint to find a car to move. Undo brings back the last car.";
        }
        else
        {
            key = "turn";
            message = "Curved arrows show where cars will turn. Check the road ahead.";
            if (!System.Array.Exists(gm.LevelCars, car => car.direction != Direction.Forward))
                yield break;
        }

        if (PlayerPrefs.GetInt("tip_" + key) != 0)
            yield break;
        PlayerPrefs.SetInt("tip_" + key, 1);
        hud.ShowTip(message);
        yield return new WaitForSeconds(5f);
        hud.ShowTip(null);
    }

    public void Undo()
    {
        if (gm.UndoLastEscape())
        {
            ClearMarker();
            idleTimer = 0;
        }
    }

    void OnCarHit()
    {
        idleTimer = 0;
        if (Camera.main == null)
            return;

        shakeTween?.Complete();
        shakeTween = Camera.main.transform.DOShakePosition(0.25f, 0.07f, 22, 90f, false, true);
    }

    void Update()
    {
        if (gm == null || !gm.CanPlay || hud.BlocksTraffic)
            return;
        if (hintQueued && !AnyCarMoving())
        {
            hintQueued = false;
            UseHint();
            return;
        }
        if (gm.currentLevel > autoHintUntilLevel)
            return;
        if (AnyCarMoving() || Input.GetMouseButton(0))
        {
            idleTimer = 0;
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer >= autoHintDelay)
        {
            idleTimer = 0;
            ShowHint();
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

    public void UseHint()
    {
        if (!gm.CanPlay || hud.BlocksTraffic)
            return;
        if (AnyCarMoving())
        {
            hintQueued = true;
            return;
        }
        if (ShowHint())
            gm.hintsUsed++;
    }

    public bool ShowHint()
    {
        Car car = NextHintCar();
        if (car == null || AnyCarMoving())
            return false;

        return hud.ShowCarHint(car);
    }

    void ClearMarker()
    {
        if (hud != null)
            hud.ClearCarHint();
    }
}
