using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the single-scene setup:
///  1. converts every legacy Level_N scene into a level prefab (Assets/Resources/Levels/Level_N.prefab)
///  2. builds the generated levels from Assets/Editor/LevelData/generated_levels.json
///  3. creates Assets/Scenes/Game.unity (camera, light, UI/managers) and puts it in the build settings
/// Run from the menu: Tools / Traffic Jam / Build Single Scene
/// </summary>
public static class LevelBuilder
{
    const string LevelsDir = "Assets/Resources/Levels";
    const string GameScenePath = "Assets/Scenes/Game.unity";
    const string SpecPath = "Assets/Editor/LevelData/generated_levels.json";
    const int LegacyLevelCount = 30;

    static readonly string[] LegacySceneFolders = { "Assets/Scenes/Legacy", "Assets/Scenes/Levels" };

    [Serializable] class CarSpec { public string type; public float x; public float z; public float yaw; }
    [Serializable] class LevelSpec { public int level; public int source; public bool mirror; public int k; public int rounds; public int free0; public CarSpec[] cars; }
    [Serializable] class SpecList { public LevelSpec[] levels; }

    static readonly Dictionary<string, string> CarPrefabs = new Dictionary<string, string>
    {
        { "CarFwd", "Assets/Prefabs/Car_Forward.prefab" },
        { "CarLeft", "Assets/Prefabs/Car_LeftTurn.prefab" },
        { "CarRight", "Assets/Prefabs/Car_RightTurn.prefab" },
        { "CarFullLeft", "Assets/Prefabs/Car_FullLeftTurn.prefab" },
        { "CarFullRight", "Assets/Prefabs/Car_FullRightTurn.prefab" },
    };

    [MenuItem("Tools/Traffic Jam/Build Single Scene")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(LevelsDir);

        ConvertLegacyLevels();
        BuildGeneratedLevels();
        BuildGameScene();
        Validate();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // called by -executeMethod in batch mode
    public static void BuildAllBatch()
    {
        BuildAll();
    }

    // ------------------------------------------------------------------ 1. legacy scenes -> prefabs

    static string FindLegacyScene(string sceneName)
    {
        foreach (string folder in LegacySceneFolders)
        {
            string p = folder + "/" + sceneName + ".unity";
            if (File.Exists(p))
                return p;
        }
        return null;
    }

    [MenuItem("Tools/Traffic Jam/Convert Legacy Level Scenes To Prefabs")]
    public static void ConvertLegacyLevels()
    {
        Directory.CreateDirectory(LevelsDir);

        for (int n = 1; n <= LegacyLevelCount; n++)
        {
            string prefabPath = LevelsDir + "/Level_" + n + ".prefab";
            string scenePath = FindLegacyScene("Level_" + n);
            if (scenePath == null)
            {
                if (!File.Exists(prefabPath))
                    Debug.LogWarning("Level_" + n + ": neither legacy scene nor prefab found");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Camera cam = null;
            var movable = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() != null) { cam = root.GetComponent<Camera>(); continue; }
                if (root.GetComponent<Light>() != null) continue;
                if (root.GetComponent<MANAGERCHECK>() != null) continue;
                movable.Add(root);
            }

            var levelRoot = new GameObject("Level_" + n);
            var info = levelRoot.AddComponent<LevelInfo>();
            if (cam != null)
            {
                info.cameraPosition = cam.transform.position;
                info.cameraEuler = cam.transform.eulerAngles;
                info.cameraFieldOfView = cam.fieldOfView;
            }

            foreach (GameObject go in movable)
                go.transform.SetParent(levelRoot.transform, true);

            PrefabUtility.SaveAsPrefabAsset(levelRoot, prefabPath);
            Debug.Log("Converted " + scenePath + " -> " + prefabPath + " (" + movable.Count + " root objects)");
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    // ------------------------------------------------------------------ 2. generated levels

    [MenuItem("Tools/Traffic Jam/Build Generated Levels")]
    public static void BuildGeneratedLevels()
    {
        if (!File.Exists(SpecPath))
        {
            Debug.LogWarning("No generated level spec at " + SpecPath);
            return;
        }

        SpecList list = JsonUtility.FromJson<SpecList>(File.ReadAllText(SpecPath));
        if (list == null || list.levels == null)
        {
            Debug.LogError("Could not parse " + SpecPath);
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        foreach (LevelSpec spec in list.levels)
        {
            string srcPath = LevelsDir + "/Level_" + spec.source + ".prefab";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (src == null)
            {
                Debug.LogError("Level " + spec.level + ": missing source " + srcPath);
                continue;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            // keep nested prefab instances (cars, land, pedestrians...) but detach from the source level prefab
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            inst.name = "Level_" + spec.level;

            RemoveCars(inst);

            LevelInfo info = inst.GetComponent<LevelInfo>();
            if (spec.mirror)
                MirrorX(inst, info != null ? info.cameraPosition.x : 1.73f);

            int i = 0;
            foreach (CarSpec c in spec.cars)
            {
                var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabs[c.type]);
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, inst.transform);
                car.name = carPrefab.name + (i == 0 ? "" : " (" + i + ")");
                car.transform.SetPositionAndRotation(new Vector3(c.x, 0f, c.z), Quaternion.Euler(0f, c.yaw, 0f));
                i++;
            }

            string outPath = LevelsDir + "/Level_" + spec.level + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, outPath);
            UnityEngine.Object.DestroyImmediate(inst);
        }

        Debug.Log("Built " + list.levels.Length + " generated levels");
    }

    static void RemoveCars(GameObject levelRoot)
    {
        foreach (Car car in levelRoot.GetComponentsInChildren<Car>(true))
        {
            if (car != null)
                UnityEngine.Object.DestroyImmediate(car.gameObject);
        }

        // helper markers the designer used to line cars up
        foreach (Transform t in levelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == levelRoot.transform)
                continue;

            if (t.name.StartsWith("CarRef") || t.name == "Refs")
                UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    /// <summary>Mirrors the whole map left/right around world x = xm (cars are placed afterwards).</summary>
    static void MirrorX(GameObject levelRoot, float xm)
    {
        foreach (Transform t in levelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == levelRoot.transform)
                continue;

            GameObject go = t.gameObject;

            // children of a prefab instance move together with the instance root
            bool isInstanceRoot = PrefabUtility.IsOutermostPrefabInstanceRoot(go);
            if (!isInstanceRoot && PrefabUtility.IsPartOfAnyPrefab(go))
                continue;

            // plain grouping objects keep their place, their children are mirrored instead
            if (!isInstanceRoot && t.childCount > 0)
                continue;

            Vector3 p = t.position;
            p.x = 2f * xm - p.x;
            t.position = p;

            if (go.GetComponent<Pedestrian>() != null)
            {
                Vector3 e = t.eulerAngles;
                t.rotation = Quaternion.Euler(e.x, -e.y, e.z);
            }
        }
    }

    // ------------------------------------------------------------------ 3. game scene

    [MenuItem("Tools/Traffic Jam/Build Game Scene")]
    public static void BuildGameScene()
    {
        if (!File.Exists(GameScenePath))
        {
            string start = FindLegacyScene("StartScene");
            if (start == null)
            {
                Debug.LogError("StartScene not found, cannot create the Game scene");
                return;
            }
            AssetDatabase.CopyAsset(start, GameScenePath);
            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        // directional light that every level scene used to carry
        Light sun = null;
        foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) sun = l;

        if (sun == null)
        {
            var go = new GameObject("Directional Light");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.94706f, 1f, 0.9009434f, 1f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(63.1761f, 239.4269f, 249.2659f);
            go.transform.position = new Vector3(0f, 3f, 0f);
        }
        RenderSettings.sun = sun;

        if (GameObject.Find("LevelContainer") == null)
            new GameObject("LevelContainer");

        // make sure the camera is the "Main Camera"
        Camera cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.gameObject.tag = "MainCamera";
            var info = AssetDatabase.LoadAssetAtPath<GameObject>(LevelsDir + "/Level_1.prefab");
            if (info != null && info.GetComponent<LevelInfo>() != null)
                info.GetComponent<LevelInfo>().ApplyCamera(cam);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScenePath, true) };
        Debug.Log("Game scene ready: " + GameScenePath);
    }

    // ------------------------------------------------------------------ validation

    [MenuItem("Tools/Traffic Jam/Validate Levels")]
    public static void Validate()
    {
        var sb = new StringBuilder("LEVEL REPORT\n");
        int problems = 0;
        int n = 1;
        while (true)
        {
            string path = LevelsDir + "/Level_" + n + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) break;

            int cars = prefab.GetComponentsInChildren<Car>(true).Length;
            int escapes = 0, peds = 0, missing = 0;
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.CompareTag("EscapeTrigger")) escapes++;
                if (t.GetComponent<Pedestrian>() != null) peds++;
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            }

            bool hasInfo = prefab.GetComponent<LevelInfo>() != null;
            bool bad = cars == 0 || escapes == 0 || missing > 0 || !hasInfo;
            if (bad) problems++;
            sb.AppendLine("Level_" + n + ": cars=" + cars + " escapeTriggers=" + escapes + " pedestrians=" + peds + " missingScripts=" + missing + (hasInfo ? "" : " NO-LEVELINFO") + (bad ? "   <-- PROBLEM" : ""));
            n++;
        }

        sb.AppendLine("Total level prefabs: " + (n - 1) + ", problems: " + problems);
        Debug.Log(sb.ToString());
    }
}
