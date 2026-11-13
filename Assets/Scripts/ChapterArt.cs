using UnityEngine;

static class ChapterArt
{
    public static readonly string[] Names =
    {
        "Daisy Downs", "Meadow Crossing", "Powder Pines", "Frosty Pass", "Cactus Canyon",
        "Golden Dunes", "Maple Mile", "Harvest Lane", "Orchard Loop", "Applewood Bend"
    };

    static readonly string[] Patterns = { "meadow", "snow", "desert", "autumn", "orchard" };
    static readonly Color32[] Roads =
    {
        new Color32(122, 137, 166, 255), new Color32(125, 147, 173, 255),
        new Color32(151, 137, 141, 255), new Color32(135, 124, 144, 255), new Color32(111, 141, 151, 255)
    };
    static readonly Color32[] Curbs =
    {
        new Color32(238, 238, 231, 255), new Color32(238, 246, 249, 255),
        new Color32(250, 236, 212, 255), new Color32(247, 227, 210, 255), new Color32(234, 241, 228, 255)
    };
    static readonly Color32[] Skirts =
    {
        new Color32(187, 194, 206, 255), new Color32(177, 199, 216, 255),
        new Color32(205, 187, 163, 255), new Color32(204, 175, 169, 255), new Color32(169, 193, 184, 255)
    };
    static readonly (CarSkin skin, Color32 paint)[] Fleet =
    {
        (CarSkin.Hatchback, new Color32(255, 80, 27, 255)),
        (CarSkin.Rally, new Color32(39, 133, 250, 255)),
        (CarSkin.Delivery, new Color32(244, 250, 255, 255)),
        (CarSkin.Hatchback, new Color32(255, 195, 22, 255)),
        (CarSkin.Rally, new Color32(13, 215, 172, 255)),
        (CarSkin.Delivery, new Color32(235, 64, 145, 255))
    };
    static readonly Material[] Ground = new Material[Patterns.Length];
    static readonly Material[] Asphalt = new Material[Patterns.Length];

    public static int Theme(int chapter) => ((chapter - 1) / 2) % Patterns.Length;
    public static string Name(int chapter) => Names[(chapter - 1) % Names.Length];

    public static void Apply(GameObject level, int chapter)
    {
        int theme = Theme(chapter);
        if (Ground[theme] == null)
        {
            var shader = Resources.Load<Shader>("Cartoon/CartoonTerrain");
            if (shader != null)
            {
                var finish = new Material(shader) { mainTexture = Resources.Load<Texture2D>("Cartoon/" + Patterns[theme]) };
                finish.SetColor("_RimColor", Curbs[theme]);
                finish.SetColor("_SideColor", Skirts[theme]);
                Ground[theme] = finish;

                var road = new Material(shader) { mainTexture = Resources.Load<Texture2D>("Cartoon/asphalt") };
                road.SetColor("_RoadColor", Roads[theme]);
                road.SetFloat("_Road", 1);
                Asphalt[theme] = road;
            }
        }

        var decoration = new GameObject("District scenery").transform;
        decoration.SetParent(level.transform, false);
        foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool land = System.Array.Exists(materials, material => material != null && material.name == "Land_Border");
            if (land && Ground[theme] != null && DistrictTile.Replace(renderer, decoration, Ground[theme]))
            {
                DecorateTile(decoration, renderer.bounds, theme);
                continue;
            }

            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null && materials[index].name == "Road")
                {
                    materials[index] = Asphalt[theme] != null ? Asphalt[theme] : CartoonModels.MaterialFor(Roads[theme], 12);
                    changed = true;
                }
            }
            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    public static void StyleCars(Car[] cars, int chapter)
    {
        int start = Theme(chapter);
        for (int i = 0; i < cars.Length; i++)
        {
            var look = Fleet[(i + start) % Fleet.Length];
            CartoonModels.StyleCar(cars[i], look.paint, look.skin);
        }
    }

    static void DecorateTile(Transform parent, Bounds bounds, int theme)
    {
        float width = bounds.size.x;
        float depth = bounds.size.z;
        float shortSide = Mathf.Min(width, depth);
        if (shortSide < .66f)
            return;

        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        int seed = Mathf.RoundToInt(origin.x * 19 + origin.z * 43);
        float scale = Mathf.Min(shortSide * .37f, .52f);
        if (Mathf.Max(width, depth) > 1.55f && shortSide > .86f)
        {
            Vector3 offset = width > depth ? Vector3.right * width * .21f : Vector3.forward * depth * .21f;
            Prop(parent, origin - offset, theme, scale, seed);
            if (Mathf.Max(width, depth) > 2.2f)
                Prop(parent, origin + offset, theme, scale * .82f, seed + 1);
        }
        else
            Prop(parent, origin, theme, scale, seed);
    }

    static void Prop(Transform parent, Vector3 origin, int theme, float size, int seed)
    {
        var prop = new GameObject("Clay scenery").transform;
        prop.SetParent(parent, false);
        prop.position = origin;
        prop.localScale = Vector3.one * size;
        prop.localRotation = Quaternion.Euler(0, seed * 21, 0);

        if (theme == 1)
        {
            Part(prop, "Pine trunk", new Vector3(0, .16f, 0), new Vector3(.18f, .34f, .20f), new Color32(169, 146, 129, 255));
            for (int i = 0; i < 3; i++)
            {
                float width = .93f - .24f * i;
                Part(prop, "Pine bough", new Vector3(0, .34f + i * .21f, 0),
                    new Vector3(width, .43f, width), new Color32(127, 170, 186, 255));
                Part(prop, "Snow", new Vector3(0, .43f + i * .21f, 0),
                    new Vector3(width * .84f, .32f, width * .84f), new Color32(244, 250, 255, 255));
            }
            Part(prop, "Snow drift", new Vector3(.53f, .06f, -.35f), new Vector3(.39f, .14f, .29f),
                new Color32(242, 249, 253, 255));
            return;
        }

        if (theme == 2)
        {
            Color32 cactus = new Color32(150, 184, 123, 255);
            Part(prop, "Cactus", new Vector3(0, .38f, 0), new Vector3(.43f, .82f, .43f), cactus);
            foreach (int side in new[] { -1, 1 })
            {
                Transform arm = Part(prop, "Cactus arm", new Vector3(side * .24f, .33f + .09f * side, 0),
                    new Vector3(.23f, .49f, .26f), cactus);
                arm.localRotation = Quaternion.Euler(0, 0, -side * 32);
            }
            Part(prop, "Sandstone", new Vector3(.53f, .07f, -.37f), new Vector3(.33f, .19f, .26f),
                new Color32(216, 172, 127, 255));
            return;
        }

        Color32 leaves = theme == 3 ? new Color32(226, 168, 103, 255) : new Color32(155, 199, 104, 255);
        Color32 lightLeaves = theme == 3 ? new Color32(239, 190, 122, 255) : new Color32(174, 214, 116, 255);
        Part(prop, "Trunk", new Vector3(0, .25f, 0), new Vector3(.24f, .58f, .24f),
            new Color32(178, 132, 95, 255));
        Part(prop, "Crown", new Vector3(0, .82f, 0), new Vector3(.82f, .82f, .81f), lightLeaves);
        Part(prop, "Left foliage", new Vector3(-.28f, .61f, .10f), new Vector3(.61f, .61f, .62f), leaves);
        Part(prop, "Right foliage", new Vector3(.27f, .67f, -.11f), new Vector3(.62f, .63f, .61f), leaves);

        var bud = new Vector3(.56f, .047f, -.36f);
        if (theme == 3)
        {
            for (int i = 0; i < 3; i++)
                Part(prop, "Fallen leaf", bud + new Vector3(i * .105f, 0, i * .075f),
                    new Vector3(.13f, .05f, .22f), lightLeaves).localRotation = Quaternion.Euler(0, i * 38, 0);
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * .4f;
                Part(prop, "Daisy petal", bud + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .079f,
                    new Vector3(.12f, .077f, .12f), new Color32(255, 249, 233, 255));
            }
            Part(prop, "Daisy center", bud + Vector3.up * .022f, new Vector3(.10f, .07f, .10f),
                new Color32(246, 202, 97, 255));
        }

        for (int i = 0; i < 3; i++)
        {
            var tuft = Part(prop, "Grass blade", new Vector3(-.53f + i * .07f, .072f, -.39f),
                new Vector3(.095f, .24f - Mathf.Abs(i - 1) * .04f, .13f), leaves);
            tuft.localRotation = Quaternion.Euler(0, 0, 25 - i * 25);
        }
    }

    static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Color32 color)
    {
        var part = CartoonModels.Part(parent, name, position, size, color, false, 9);
        part.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return part;
    }
}
