using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
sealed class LevelMapTrail : MaskableGraphic
{
    public List<Vector2> points = new List<Vector2>();
    public float width = 28;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (points.Count < 2)
            return;

        var samples = new List<Vector2>();
        for (int segment = 0; segment < points.Count - 1; segment++)
        {
            Vector2 a = points[Mathf.Max(0, segment - 1)];
            Vector2 b = points[segment];
            Vector2 c = points[segment + 1];
            Vector2 d = points[Mathf.Min(points.Count - 1, segment + 2)];
            for (int step = 0; step < 20; step++)
            {
                float t = step / 20f;
                samples.Add(.5f * ((2 * b) + (-a + c) * t +
                    (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t));
            }
        }
        samples.Add(points[points.Count - 1]);
        for (int i = 0; i < samples.Count; i++)
        {
            Vector2 tangent = samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(0, i - 1)];
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * width * .5f;
            mesh.AddVert(samples[i] - normal, color, Vector2.zero);
            mesh.AddVert(samples[i] + normal, color, Vector2.one);
            if (i == 0)
                continue;
            int previous = (i - 1) * 2;
            mesh.AddTriangle(previous, previous + 1, previous + 2);
            mesh.AddTriangle(previous + 1, previous + 3, previous + 2);
        }
    }
}
