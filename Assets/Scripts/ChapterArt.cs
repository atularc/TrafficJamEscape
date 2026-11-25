using UnityEngine;

static class ChapterArt
{
    // One world per district (10 levels each): name, ground pattern, road / curb / tile-side colours, animal cast.
    public static readonly string[] Names =
    {
        "Daisy Downs", "Cherry Lane", "Powder Pines", "Seashell Shore", "Cactus Canyon",
        "Jungle Trail", "Maple Mile", "Lavender Hills", "Applewood Bend", "Lava Ridge"
    };

    static readonly string[] Patterns = { "meadow", "blossom", "snow", "beach", "desert", "jungle", "autumn", "lavender", "orchard", "volcano" };
    static readonly Color32[] Roads =
    {
        new Color32(122, 137, 166, 255), new Color32(132, 128, 158, 255), new Color32(125, 147, 173, 255), new Color32(128, 150, 168, 255),
        new Color32(151, 137, 141, 255), new Color32(104, 128, 128, 255), new Color32(135, 124, 144, 255), new Color32(128, 122, 156, 255),
        new Color32(111, 141, 151, 255), new Color32(88, 84, 98, 255)
    };
    static readonly Color32[] Curbs =
    {
        new Color32(238, 238, 231, 255), new Color32(255, 236, 242, 255), new Color32(238, 246, 249, 255), new Color32(255, 248, 228, 255),
        new Color32(250, 236, 212, 255), new Color32(226, 238, 214, 255), new Color32(247, 227, 210, 255), new Color32(240, 234, 250, 255),
        new Color32(234, 241, 228, 255), new Color32(120, 104, 108, 255)
    };
    static readonly Color32[] Skirts =
    {
        new Color32(187, 194, 206, 255), new Color32(214, 186, 200, 255), new Color32(177, 199, 216, 255), new Color32(222, 200, 160, 255),
        new Color32(205, 187, 163, 255), new Color32(132, 150, 120, 255), new Color32(204, 175, 169, 255), new Color32(184, 172, 208, 255),
        new Color32(169, 193, 184, 255), new Color32(70, 58, 64, 255)
    };
    // Each world has its own cast of animals; the second value picks a colour variant.
    static readonly (AnimalKind kind, int variant)[][] Casts =
    {
        new[] { (AnimalKind.Bunny, 0), (AnimalKind.Piglet, 0), (AnimalKind.Fox, 0), (AnimalKind.Frog, 0), (AnimalKind.Cat, 0), (AnimalKind.Panda, 0), (AnimalKind.Bear, 0), (AnimalKind.Bunny, 1) },
        new[] { (AnimalKind.Panda, 0), (AnimalKind.Bunny, 1), (AnimalKind.Cat, 0), (AnimalKind.Piglet, 0), (AnimalKind.Fox, 0), (AnimalKind.Frog, 0), (AnimalKind.Bear, 2), (AnimalKind.Bunny, 0) },
        new[] { (AnimalKind.Penguin, 0), (AnimalKind.Bear, 0), (AnimalKind.Bunny, 1), (AnimalKind.Panda, 0), (AnimalKind.Cat, 0), (AnimalKind.Penguin, 0), (AnimalKind.Fox, 0), (AnimalKind.Bunny, 2) },
        new[] { (AnimalKind.Frog, 1), (AnimalKind.Piglet, 0), (AnimalKind.Cat, 0), (AnimalKind.Penguin, 0), (AnimalKind.Bunny, 1), (AnimalKind.Fox, 0), (AnimalKind.Bear, 2), (AnimalKind.Cat, 1) },
        new[] { (AnimalKind.Fox, 1), (AnimalKind.Cat, 0), (AnimalKind.Piglet, 0), (AnimalKind.Bunny, 1), (AnimalKind.Bear, 2), (AnimalKind.Frog, 1), (AnimalKind.Fox, 0), (AnimalKind.Cat, 2) },
        new[] { (AnimalKind.Frog, 0), (AnimalKind.Panda, 0), (AnimalKind.Fox, 2), (AnimalKind.Bear, 2), (AnimalKind.Piglet, 0), (AnimalKind.Frog, 1), (AnimalKind.Cat, 0), (AnimalKind.Bunny, 1) },
        new[] { (AnimalKind.Fox, 2), (AnimalKind.Bear, 0), (AnimalKind.Bunny, 1), (AnimalKind.Cat, 0), (AnimalKind.Piglet, 0), (AnimalKind.Frog, 0), (AnimalKind.Fox, 0), (AnimalKind.Panda, 0) },
        new[] { (AnimalKind.Bunny, 0), (AnimalKind.Cat, 1), (AnimalKind.Piglet, 0), (AnimalKind.Frog, 0), (AnimalKind.Fox, 0), (AnimalKind.Bear, 2), (AnimalKind.Panda, 0), (AnimalKind.Bunny, 2) },
        new[] { (AnimalKind.Piglet, 0), (AnimalKind.Panda, 0), (AnimalKind.Frog, 0), (AnimalKind.Bunny, 0), (AnimalKind.Bear, 2), (AnimalKind.Cat, 1), (AnimalKind.Fox, 0), (AnimalKind.Frog, 1) },
        new[] { (AnimalKind.Fox, 0), (AnimalKind.Piglet, 0), (AnimalKind.Frog, 0), (AnimalKind.Bunny, 1), (AnimalKind.Cat, 0), (AnimalKind.Panda, 0), (AnimalKind.Frog, 1), (AnimalKind.Bear, 2) },
    };
    static readonly Material[] Ground = new Material[Patterns.Length];
    static readonly Material[] Asphalt = new Material[Patterns.Length];

    public static int Theme(int chapter) => (Mathf.Max(1, chapter) - 1) % Patterns.Length;
    public static string Name(int chapter) => Names[(Mathf.Max(1, chapter) - 1) % Names.Length];

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
                DistrictScenery.Decorate(decoration, renderer.bounds, theme);
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

    /// <summary>Dresses every car of the level as one of the chapter's animals (neighbours in the list differ).</summary>
    public static void StyleCars(Car[] cars, int chapter)
    {
        var cast = Casts[Theme(chapter)];
        int start = chapter * 3;
        for (int i = 0; i < cars.Length; i++)
        {
            var look = cast[(start + i * 3) % cast.Length];
            AnimalModels.Build(cars[i], look.kind, look.variant + i / cast.Length);
        }
    }
}
