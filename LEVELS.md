# Levels – single scene setup

The game now runs in **one scene**: `Assets/Scenes/Game.unity`
(camera, light, `MANAGERS_AND_UI` with GameManager / UIManager / SoundManager / all UI).

Every level is a **prefab** that only holds level content (roads, land, cars, pedestrians, crosswalks,
road points, escape triggers). `GameManager` spawns the prefab for the current level into `LevelContainer`,
moves the camera to the pose stored in the prefab's `LevelInfo` component, and the UI (level number, popups,
coins) updates from the existing `LevelLoaded` event.

```
Assets/Resources/Levels/Level_1.prefab ... Level_100.prefab    <- level content, loaded by name
Assets/Scenes/Game.unity                                       <- the only scene in Build Settings
Assets/Scenes/Legacy/                                          <- old per-level scenes (not in the build, safe to delete)
Assets/Scripts/LevelInfo.cs                                    <- camera pose per level
Assets/Editor/LevelBuilder.cs                                  <- Tools > Traffic Jam > ... (rebuild tools)
Assets/Editor/LevelData/generated_levels.json                  <- car layouts of levels 31-100
```

* Levels 1-30 are the original levels, converted 1:1 from the old scenes (same objects, same positions).
* Levels 31-100 reuse the original maps (some mirrored left/right) with new car puzzles. Each puzzle was
  checked with a solver and then played through automatically in Unity with the real `Car.cs` code.
* After level 100, endless stages reuse puzzles from `GameManager.randomLevelsStartAt` through the last authored level. Each endless stage keeps the same puzzle when retried or replayed.

## Testing a level
Select the `MANAGERS_AND_UI` / `GameManager` object in `Game.unity` and set **Editor Start Level** (e.g. 57), press Play.
(0 = use the saved progress.)

## Adding your own level
1. Duplicate any `Level_N.prefab` in `Assets/Resources/Levels`, name it `Level_<next number>` (e.g. `Level_101`).
2. Edit it (move land, add cars from `Assets/Prefabs/Car_*.prefab`, ...). Keep one `EscapeTrigger` at every road end
   and a `RoadPoint` (layer 6) wherever a car has to turn.
3. Set the camera in the `LevelInfo` component on the prefab root if you need a different framing.

No scene or build-settings changes are needed – levels are found by name.

## Art and interface

The game applies the cartoon cars, textured terrain, curbs, and scenery as each level loads. The existing level prefabs supply the roads, cars, movement directions, and colliders. The visual models use `Assets/Scripts/CartoonModels.cs`, and the district art and color choices live in `Assets/Scripts/ChapterArt.cs`.

| Levels | Theme |
| --- | --- |
| 1–20 | Flower meadows |
| 21–40 | Snowy pines |
| 41–60 | Cactus desert |
| 61–80 | Autumn woodland |
| 81–100 | Apple orchards |

`MetaHud.cs` builds the loading screen, in-game HUD, level map, results, retry, and daily reward screens. The art for these screens is in `Assets/Resources/TrafficUi`, and the terrain patterns and roof arrows are in `Assets/Resources/Cartoon`.

## Gameplay features
- **HINT** uses `LevelInfo.solution` (car hierarchy indices in a valid clearing order).
- **UNDO** (3 per level): `GameManager.UndoLastEscape()` brings the last escaped car back to its start pose
  (`Car.ResetToStart`). Escaped cars are now deactivated, not destroyed.
- **Chapters**: every `GameManager.chapterSize` (10) levels; names in `ChapterArt.Names`; finishing the last
  level of a chapter pays `chapterBonusCoins`.
- **Tutorial tips** appear once, when the situation first occurs (first level, blocked cars, turning cars,
  pedestrians, hint/undo). Stored in PlayerPrefs keys `tip_*`.
- **Pedestrian signal**: `Pedestrian.waitAtEnds` seconds of waiting at each curb; small sphere above the head is
  green while walking, red while waiting.
- **Stars**: 3 = no crash and no hint/undo; each costs one star (min 1). Best per level in PlayerPrefs `stars_<n>`.
- **Level select** (`MetaHud.cs`): LEVELS button; locked beyond `PlayerPrefs "Level"`; replaying never lowers progress.
- **Daily reward**: once per day, 10 coins x streak day (max 7).
