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

    public void ApplyCamera(Camera cam)
    {
        if (cam == null)
            return;

        cam.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraEuler));
        cam.fieldOfView = cameraFieldOfView;
    }
}
