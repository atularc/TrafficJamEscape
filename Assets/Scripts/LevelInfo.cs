using UnityEngine;

/// <summary>
/// Sits on the root of every level prefab (Assets/Resources/Levels/Level_N.prefab).
/// A level prefab only contains level content (roads, land, cars, pedestrians, escape triggers...).
/// The single Game scene owns the camera, light and UI, and applies this data when the level is spawned.
/// </summary>
public class LevelInfo : MonoBehaviour
{
    [Header("Camera used while this level is active")]
    public Vector3 cameraPosition = new Vector3(1.73f, 7.2f, -0.5f);
    public Vector3 cameraEuler = new Vector3(69.4498f, 0.3439f, 0f);
    public float cameraFieldOfView = 38f;

    [Header("Hint data: indices of the cars (hierarchy order) in a valid clearing order")]
    public int[] solution;

    public void ApplyCamera(Camera cam)
    {
        if (cam == null)
            return;

        cam.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraEuler));
        cam.fieldOfView = cameraFieldOfView;
    }

    public static void FitCarsAboveControls(Camera cam, Car[] cars)
    {
        if (cam == null || cam.orthographic)
            return;

        Rect playArea = PlayViewport();
        float expansion = 1f;
        foreach (Car car in cars)
        {
            if (car == null || !car.TryGetComponent<BoxCollider>(out var hitbox))
                continue;

            Transform model = car.transform.Find(CartoonModels.CarModelName);
            // The rendered body is wider and taller than the original collision shape.
            Bounds bounds = model != null
                ? new Bounds(new Vector3(0, .215f, 0), new Vector3(1.19f, .45f, 1.02f))
                : new Bounds(hitbox.center, Vector3.Scale(hitbox.size, new Vector3(1.4f, 1.6f, 1.08f)));
            Transform space = model != null ? model : car.transform;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 localPoint = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 point = cam.WorldToViewportPoint(space.TransformPoint(localPoint));
                if (point.z <= 0f)
                    continue;
                expansion = Mathf.Max(expansion, RequiredExpansion(point.x, playArea.xMin, playArea.xMax));
                expansion = Mathf.Max(expansion, RequiredExpansion(point.y, playArea.yMin, playArea.yMax));
            }
        }
        if (expansion > 1f)
        {
            float halfAngle = cam.fieldOfView * Mathf.Deg2Rad * .5f;
            cam.fieldOfView = Mathf.Atan(Mathf.Tan(halfAngle) * expansion) * Mathf.Rad2Deg * 2f;
        }
    }

    public static Rect PlayViewport()
    {
        float width = Mathf.Max(1, Screen.width);
        float height = Mathf.Max(1, Screen.height);
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0)
            safe = new Rect(0, 0, width, height);

        // Screen-space CanvasScaler Expand uses the smaller reference scale.
        float uiScale = Mathf.Min(width / 1080f, height / 1920f);
        return Rect.MinMaxRect(
            Mathf.Min(.40f, (safe.xMin + 30f * uiScale) / width),
            Mathf.Min(.40f, (safe.yMin + MetaHud.GameplayBottomInset * uiScale) / height),
            Mathf.Max(.60f, (safe.xMax - 30f * uiScale) / width),
            Mathf.Max(.60f, (safe.yMax - MetaHud.GameplayTopInset * uiScale) / height));
    }

    static float RequiredExpansion(float point, float min, float max) =>
        point < .5f ? (.5f - point) / (.5f - min) : (point - .5f) / (max - .5f);

}
