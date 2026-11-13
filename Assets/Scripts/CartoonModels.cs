using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

enum CarSkin { Hatchback, Rally, Delivery }

static class CartoonModels
{
    public const string CarModelName = "Cartoon car";
    static readonly Dictionary<(Color32, int), Material> Materials = new Dictionary<(Color32, int), Material>();
    static readonly Dictionary<Direction, Sprite> Arrows = new Dictionary<Direction, Sprite>();
    static Mesh rounded, sphere;
    static readonly Color32 Tire = new Color32(29, 36, 56, 255);
    static readonly Color32 Glass = new Color32(36, 70, 82, 255);
    static readonly Color32 Roof = new Color32(250, 232, 198, 255);
    static readonly Color32 Chrome = new Color32(181, 214, 237, 255);
    static readonly Color32 Light = new Color32(255, 246, 174, 255);

    public static void StyleCar(Car car, Color32 paint, CarSkin skin)
    {
        var footprint = car.GetComponent<BoxCollider>();
        if (footprint == null)
            return;

        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        Transform model = new GameObject(CarModelName).transform;
        model.SetParent(car.transform, false);
        model.localPosition = new Vector3(footprint.center.x, 0, footprint.center.z);
        float length = footprint.size.z;
        bool van = skin == CarSkin.Delivery;
        bool rally = skin == CarSkin.Rally;
        float width = Mathf.Max(footprint.size.x * 1.06f, length * (van ? .51f : rally ? .48f : .46f));
        model.localScale = new Vector3(width * 1.15f, length * 1.20f, length * 1.035f);

        Color32 lowerPaint = Color.Lerp(paint, new Color32(75, 81, 103, 255), 0.18f);
        Part(model, "Bumpers", new Vector3(0, .093f, 0), new Vector3(.96f, .121f, .98f), lowerPaint);
        Part(model, "Rounded body", new Vector3(0, .157f, 0), new Vector3(1, .195f, 1), paint, true, 69)
            .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        Part(model, "Sculpted hood", new Vector3(0, .218f, .307f),
            new Vector3(.81f, .119f, .35f), paint, true, 74);
        Part(model, "Front grille", new Vector3(0, .107f, .482f),
            new Vector3(.39f, .058f, .052f), Glass, true, 30);
        float cabinZ = van ? -.08f : rally ? -.11f : -.045f;
        float roofHeight = van ? .383f : rally ? .316f : .346f;
        float cabinLength = van ? .75f : rally ? .49f : .61f;
        float roofLength = van ? .58f : rally ? .40f : .46f;
        float cabinHeight = van ? .230f : .166f;
        Part(model, "Cabin glass", new Vector3(0, roofHeight - cabinHeight * .5f - .012f, cabinZ + .026f),
            new Vector3(van ? .88f : .80f, cabinHeight, cabinLength), Glass, true, 76);
        Part(model, "Roof sign", new Vector3(0, roofHeight, cabinZ),
            new Vector3(van ? .81f : .72f, .052f, roofLength), Roof, true, 45);

        foreach (int side in new[] { -1, 1 })
        {
            Part(model, "Side mirror", new Vector3(side * .49f, .226f, .20f),
                new Vector3(.19f, .053f, .10f), paint, false);
            Part(model, "Headlight", new Vector3(side * .29f, .17f, .445f),
                new Vector3(.25f, .085f, .108f), Light, false, 48);
            Part(model, "Tail light", new Vector3(side * .31f, .145f, -.454f),
                new Vector3(.25f, .058f, .083f), new Color32(244, 54, 73, 255), false);
            foreach (int axle in new[] { -1, 1 })
            {
                Part(model, "Wheel arch", new Vector3(side * .414f, .165f, axle * .293f),
                    new Vector3(.33f, .177f, .276f), paint, false, 70);
                Part(model, "Tire", new Vector3(side * .438f, .080f, axle * .29f),
                    new Vector3(.222f, .160f, .17f), Tire, false, 16);
                Part(model, "Hubcap", new Vector3(side * .52f, .080f, axle * .29f),
                    new Vector3(.079f, .085f, .092f), Chrome, false, 64);
            }
        }

        var sign = new GameObject("Direction arrow", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        sign.transform.SetParent(model, false);
        sign.transform.localPosition = new Vector3(0, roofHeight + .030f, cabinZ);
        sign.transform.localRotation = Quaternion.Euler(90, 0, 0);
        sign.transform.localScale = new Vector3(.74f, Mathf.Min(.40f, roofLength), 1);
        if (!Arrows.TryGetValue(car.direction, out Sprite sprite))
        {
            Texture2D tex = Resources.Load<Texture2D>("Cartoon/" + car.direction.ToString().ToLowerInvariant());
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    Vector2.one * .5f, tex.width, 0, SpriteMeshType.FullRect);
                Arrows.Add(car.direction, sprite);
            }
        }
        sign.sprite = sprite;
        // Keep symbols readable on mirrored layouts without flipping the represented turn.
        if (model.lossyScale.x * model.lossyScale.y * model.lossyScale.z < 0)
            sign.flipX = true;
    }

    public static Transform Part(Transform parent, string name, Vector3 position, Vector3 size,
        Color32 color, bool box = true, int gloss = 23)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<MeshFilter>().sharedMesh = box ? Rounded : Sphere;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = MaterialFor(color, gloss);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return part.transform;
    }

    public static Material MaterialFor(Color32 color, int gloss = 23)
    {
        if (Materials.TryGetValue((color, gloss), out Material existing))
            return existing;

        var material = new Material(Shader.Find("Standard")) { color = color, name = "Cartoon finish", enableInstancing = true };
        material.SetFloat("_Glossiness", gloss * .01f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", (Color)color * .06f);
        Materials.Add((color, gloss), material);
        return material;
    }

    static Mesh Sphere => sphere != null ? sphere : sphere = BuildShape(.5f);
    static Mesh Rounded => rounded != null ? rounded : rounded = BuildShape(.27f);

    static Mesh BuildShape(float radius)
    {
        float inner = .5f - radius;
        const int divisions = 10;
        int stride = divisions + 1;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        Vector3[] faces = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        foreach (Vector3 face in faces)
        {
            int start = vertices.Count;
            Vector3 x = Vector3.Cross(Mathf.Abs(face.y) > .9f ? Vector3.forward : Vector3.up, face);
            Vector3 y = Vector3.Cross(face, x);
            for (int row = 0; row <= divisions; row++)
            for (int column = 0; column <= divisions; column++)
            {
                Vector3 point = face * .5f + x * (column / (float)divisions - .5f) + y * (row / (float)divisions - .5f);
                Vector3 core = new Vector3(Mathf.Clamp(point.x, -inner, inner),
                    Mathf.Clamp(point.y, -inner, inner), Mathf.Clamp(point.z, -inner, inner));
                Vector3 normal = (point - core).normalized;
                vertices.Add(core + radius * normal);
                normals.Add(normal);
                if (row == divisions || column == divisions)
                    continue;

                int i = start + row * stride + column;
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + stride);
                triangles.Add(i + 1);
                triangles.Add(i + stride + 1);
                triangles.Add(i + stride);
            }
        }
        var mesh = new Mesh { name = "Rounded cartoon mesh" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.UploadMeshData(true);
        return mesh;
    }
}
