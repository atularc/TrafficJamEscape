using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
sealed class DistrictTile : MonoBehaviour
{
    const int ArcSteps = 8;
    const int RingSize = 4 * (ArcSteps + 1);
    Mesh mesh;

    public static bool Replace(MeshRenderer source, Transform parent, Material finish)
    {
        if (!source.enabled || !source.gameObject.activeInHierarchy ||
            !source.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
            return false;

        Bounds local = filter.sharedMesh.bounds;
        Transform original = source.transform;
        Vector3 right = Vector3.ProjectOnPlane(original.right, Vector3.up).normalized;
        if (right.sqrMagnitude < .5f)
            return false;

        Vector3 forward = Vector3.Cross(right, Vector3.up);
        Vector3 x = original.TransformVector(Vector3.right * local.extents.x);
        Vector3 y = original.TransformVector(Vector3.up * local.extents.y);
        Vector3 z = original.TransformVector(Vector3.forward * local.extents.z);
        float width = ProjectExtent(right, x, y, z);
        float depth = ProjectExtent(forward, x, y, z);
        if (Mathf.Min(width, depth) < .04f)
            return false;

        Bounds world = source.bounds;
        Vector3 center = original.TransformPoint(local.center);
        center.y = world.max.y;
        var tile = new GameObject("Beveled terrain tile", typeof(DistrictTile)).GetComponent<DistrictTile>();
        tile.transform.SetParent(parent, false);
        tile.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward));
        tile.CreateMesh(width, depth, Mathf.Clamp(world.size.y, .12f, .65f));
        var renderer = tile.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = finish;
        float seed = Mathf.Repeat(Mathf.Sin(center.x * 41.79f + center.z * 25.13f) * 532.17f, 1f);
        var variation = new MaterialPropertyBlock();
        variation.SetVector("_TileVariation", new Vector4(seed * .87f, seed * .53f, Mathf.Lerp(.94f, 1.04f, seed), 0));
        renderer.SetPropertyBlock(variation);
        source.enabled = false;
        return true;
    }

    static float ProjectExtent(Vector3 axis, Vector3 x, Vector3 y, Vector3 z) =>
        Mathf.Abs(Vector3.Dot(axis, x)) + Mathf.Abs(Vector3.Dot(axis, y)) + Mathf.Abs(Vector3.Dot(axis, z));

    void CreateMesh(float halfWidth, float halfDepth, float height)
    {
        float narrow = Mathf.Min(halfWidth, halfDepth);
        float radius = Mathf.Min(.19f, narrow * .36f);
        float rim = Mathf.Min(.066f, narrow * .20f);

        // The vertex weights blend the shaded skirt, colored curb, and patterned top in one material.
        Color side = new Color(1, 0, 0, .91f);
        Color rimShade = new Color(.18f, .82f, 0, .91f);
        Color rimLight = new Color(0, 1, 0, 1f);
        Color grassEdge = new Color(0, .04f, .96f, .96f);
        Color grass = new Color(0, 0, 1, 1f);
        const int rings = 6;
        var points = new Vector3[rings * RingSize + 1];
        var colors = new Color[points.Length];
        var faces = new int[((rings - 1) * 6 + 3) * RingSize];

        AddRing(points, colors, 0, halfWidth, halfDepth, radius, -height, rim * .12f, side);
        AddRing(points, colors, 1, halfWidth, halfDepth, radius, -rim * 1.45f, 0f, side);
        AddRing(points, colors, 2, halfWidth, halfDepth, radius, -rim * .48f, 0f, rimShade);
        AddRing(points, colors, 3, halfWidth, halfDepth, radius, rim * .06f, rim * .52f, rimLight);
        AddRing(points, colors, 4, halfWidth, halfDepth, radius, rim * .06f, rim, grassEdge);
        AddRing(points, colors, 5, halfWidth, halfDepth, radius, rim * .21f, rim * 1.75f, grass);

        int face = 0;
        for (int ring = 0; ring < rings - 1; ring++)
        for (int i = 0; i < RingSize; i++)
        {
            int a = ring * RingSize + i;
            int b = ring * RingSize + (i + 1) % RingSize;
            faces[face++] = a;
            faces[face++] = a + RingSize;
            faces[face++] = b;
            faces[face++] = b;
            faces[face++] = a + RingSize;
            faces[face++] = b + RingSize;
        }

        int center = points.Length - 1;
        points[center] = Vector3.up * (rim * .21f);
        colors[center] = grass;
        for (int i = 0; i < RingSize; i++)
        {
            faces[face++] = (rings - 1) * RingSize + i;
            faces[face++] = center;
            faces[face++] = (rings - 1) * RingSize + (i + 1) % RingSize;
        }

        mesh = new Mesh { name = "Rounded district terrain" };
        mesh.vertices = points;
        mesh.colors = colors;
        mesh.triangles = faces;
        mesh.RecalculateNormals();
        mesh.UploadMeshData(true);
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    static void AddRing(Vector3[] points, Color[] colors, int ring, float halfWidth, float halfDepth,
        float radius, float height, float inset, Color color)
    {
        int index = ring * RingSize;
        float arcRadius = Mathf.Max(.001f, radius - inset);
        for (int corner = 0; corner < 4; corner++)
        {
            float x = (corner == 0 || corner == 3 ? 1 : -1) * (halfWidth - radius);
            float z = (corner < 2 ? 1 : -1) * (halfDepth - radius);
            for (int step = 0; step <= ArcSteps; step++)
            {
                float angle = (corner + step / (float)ArcSteps) * Mathf.PI * .5f;
                points[index] = new Vector3(x + Mathf.Cos(angle) * arcRadius, height,
                    z + Mathf.Sin(angle) * arcRadius);
                colors[index++] = color;
            }
        }
    }

    void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}
