using UnityEngine;
using UnityEngine.Rendering;

enum AnimalKind { Bunny, Piglet, Panda, Fox, Frog, Penguin, Cat, Bear }

/// <summary>
/// Builds the chubby toy animals that replace the cars. The animal sits in the car's footprint, faces the
/// car's forward direction, and carries the turn arrow on a badge on its back ("Roof sign", used by the hint).
/// Model space: 1 unit = the car's length, +z = forward, y = up.
/// </summary>
static class AnimalModels
{
    static readonly Color32 Eye = new Color32(34, 30, 48, 255);
    static readonly Color32 White = new Color32(252, 251, 248, 255);
    static readonly Color32 Blush = new Color32(255, 150, 170, 255);
    static readonly Color32 Badge = new Color32(255, 248, 230, 255);

    public static void Build(Car car, AnimalKind kind, int variant)
    {
        var footprint = car.GetComponent<BoxCollider>();
        if (footprint == null)
            return;

        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        Transform model = new GameObject(CartoonModels.CarModelName).transform;
        model.SetParent(car.transform, false);
        model.localPosition = new Vector3(footprint.center.x, 0, footprint.center.z);
        model.localScale = Vector3.one * footprint.size.z;

        Color32 accent;
        switch (kind)
        {
            case AnimalKind.Bunny: accent = Bunny(model, variant); break;
            case AnimalKind.Piglet: accent = Piglet(model); break;
            case AnimalKind.Panda: accent = Panda(model); break;
            case AnimalKind.Fox: accent = Fox(model, variant); break;
            case AnimalKind.Frog: accent = Frog(model, variant); break;
            case AnimalKind.Penguin: accent = Penguin(model); break;
            case AnimalKind.Cat: accent = Cat(model, variant); break;
            default: accent = Bear(model, variant); break;
        }

        // badge with the turn arrow on the animal's back
        Box(model, "Badge rim", new Vector3(0, .405f, -.11f), new Vector3(.44f, .05f, .40f), accent, 30);
        Box(model, "Roof sign", new Vector3(0, .43f, -.11f), new Vector3(.36f, .04f, .32f), Badge, 40);
        CartoonModels.ArrowSign(car, model, new Vector3(0, .455f, -.11f), new Vector2(.30f, .27f));

        AddOutline(model);
        ContactShadow(car, footprint);

        model.gameObject.AddComponent<AnimalMotion>().Init(car, kind == AnimalKind.Penguin ? .03f : .07f,
            kind == AnimalKind.Frog || kind == AnimalKind.Bunny ? 11f : 15f);
    }

    // ------------------------------------------------------------------ standing out from the scenery

    static Material outline;
    static Sprite shadowSprite;

    /// <summary>Dark outline on the silhouette parts (scenery has none, so animals read as game pieces).</summary>
    static void AddOutline(Transform model)
    {
        if (outline == null)
        {
            var shader = Resources.Load<Shader>("Cartoon/Outline");
            if (shader == null)
                return;
            outline = new Material(shader) { name = "Animal outline", enableInstancing = true };
            outline.SetColor("_Color", new Color32(36, 30, 64, 255));
            outline.SetFloat("_Width", .011f);
        }

        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            string part = renderer.gameObject.name;
            if (part.StartsWith("Eye") || part == "Blush" || part == "Nostril" || part == "Stripe" || part == "Whisker"
                || part == "Nose" || part == "Spot" || part == "Ear inside" || part == "Face" || part == "Belly"
                || part == "Roof sign" || part == "Chin" || part == "Tail tip" || part == "Ear tip")
                continue;
            renderer.sharedMaterials = new[] { renderer.sharedMaterial, outline };
        }
    }

    /// <summary>Soft dark shadow on the road under the animal so it sits on the ground.</summary>
    static void ContactShadow(Car car, BoxCollider footprint)
    {
        if (shadowSprite == null)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Animal shadow" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
                }
            tex.Apply();
            shadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }

        var shadow = new GameObject("Contact shadow", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        shadow.transform.SetParent(car.transform, false);
        shadow.transform.localPosition = new Vector3(footprint.center.x, .01f, footprint.center.z);
        shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
        shadow.transform.localScale = new Vector3(footprint.size.z * .95f, footprint.size.z * 1.25f, 1);
        shadow.sprite = shadowSprite;
        shadow.color = new Color32(22, 24, 52, 135);
        shadow.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ shared pieces

    static Transform Ball(Transform p, string name, Vector3 pos, Vector3 size, Color32 c, int gloss = 22)
        => CartoonModels.Part(p, name, pos, size, c, false, gloss);

    static Transform Box(Transform p, string name, Vector3 pos, Vector3 size, Color32 c, int gloss = 22)
        => CartoonModels.Part(p, name, pos, size, c, true, gloss);

    static void Shadowed(Transform t) => t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

    static void Body(Transform m, Color32 fur, Color32 paws, float headY = .30f, float headSize = .42f)
    {
        Shadowed(Ball(m, "Body", new Vector3(0, .22f, -.07f), new Vector3(.56f, .36f, .72f), fur));
        Shadowed(Ball(m, "Head", new Vector3(0, headY, .29f), new Vector3(headSize + .04f, headSize, headSize), fur));
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Front paw", new Vector3(side * .17f, .06f, .16f), new Vector3(.15f, .12f, .17f), paws);
            Ball(m, "Back paw", new Vector3(side * .19f, .06f, -.29f), new Vector3(.17f, .12f, .19f), paws);
        }
    }

    static void Face(Transform m, float y = .45f, float z = .42f, bool blush = true)
    {
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Eye", new Vector3(side * .1f, y, z), new Vector3(.07f, .07f, .07f), Eye, 80);
            Ball(m, "Eye shine", new Vector3(side * .1f - .015f, y + .03f, z + .015f), new Vector3(.025f, .025f, .025f), White, 10);
            if (blush)
                Ball(m, "Blush", new Vector3(side * .17f, y - .07f, z - .01f), new Vector3(.09f, .03f, .06f), Blush, 10);
        }
    }

    static Color32 Pick(int variant, params Color32[] options) => options[Mathf.Abs(variant) % options.Length];

    // ------------------------------------------------------------------ animals

    static Color32 Bunny(Transform m, int variant)
    {
        Color32 fur = Pick(variant, new Color32(240, 184, 120, 255), new Color32(196, 160, 246, 255), new Color32(178, 124, 96, 255));
        Body(m, fur, fur);
        foreach (int side in new[] { -1, 1 })
        {
            var ear = Ball(m, "Ear", new Vector3(side * .09f, .48f, .17f), new Vector3(.12f, .07f, .36f), fur);
            ear.localRotation = Quaternion.Euler(-12, side * 8, 0);
            var inner = Ball(m, "Ear inside", new Vector3(side * .09f, .515f, .18f), new Vector3(.06f, .03f, .26f), new Color32(255, 176, 196, 255), 10);
            inner.localRotation = ear.localRotation;
        }
        Ball(m, "Tail", new Vector3(0, .30f, -.45f), new Vector3(.18f, .17f, .17f), White, 8);
        Ball(m, "Nose", new Vector3(0, .40f, .50f), new Vector3(.06f, .04f, .04f), new Color32(255, 130, 160, 255), 40);
        Face(m);
        return new Color32(120, 196, 255, 255);
    }

    static Color32 Piglet(Transform m)
    {
        var pink = new Color32(255, 150, 180, 255);
        var deep = new Color32(232, 104, 146, 255);
        Body(m, pink, deep);
        Ball(m, "Snout", new Vector3(0, .33f, .49f), new Vector3(.20f, .15f, .11f), deep, 30);
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Nostril", new Vector3(side * .035f, .40f, .53f), new Vector3(.035f, .02f, .03f), new Color32(170, 70, 100, 255), 10);
            var ear = Box(m, "Ear", new Vector3(side * .15f, .48f, .27f), new Vector3(.13f, .04f, .13f), deep);
            ear.localRotation = Quaternion.Euler(0, side * 30, side * -25);
        }
        Ball(m, "Curly tail", new Vector3(0, .32f, -.45f), new Vector3(.09f, .09f, .09f), deep);
        Face(m, .45f, .40f);
        return new Color32(255, 214, 90, 255);
    }

    static Color32 Panda(Transform m)
    {
        var black = new Color32(52, 50, 66, 255);
        Body(m, White, black);
        Shadowed(Ball(m, "Shoulders", new Vector3(0, .24f, .07f), new Vector3(.60f, .37f, .26f), black));
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Ear", new Vector3(side * .16f, .47f, .27f), new Vector3(.13f, .11f, .11f), black);
            var patch = Ball(m, "Eye patch", new Vector3(side * .1f, .44f, .42f), new Vector3(.12f, .05f, .09f), black, 30);
            patch.localRotation = Quaternion.Euler(0, side * -25, 0);
            Ball(m, "Eye", new Vector3(side * .1f, .47f, .43f), new Vector3(.045f, .03f, .045f), White, 60);
            Ball(m, "Blush", new Vector3(side * .18f, .38f, .41f), new Vector3(.08f, .03f, .05f), Blush, 10);
        }
        Ball(m, "Nose", new Vector3(0, .40f, .50f), new Vector3(.07f, .04f, .04f), black, 50);
        return new Color32(126, 214, 120, 255);
    }

    static Color32 Fox(Transform m, int variant)
    {
        Color32 fur = Pick(variant, new Color32(255, 128, 36, 255), new Color32(240, 170, 80, 255), new Color32(232, 92, 48, 255));
        var dark = new Color32(60, 44, 52, 255);
        Body(m, fur, dark);
        Ball(m, "Muzzle", new Vector3(0, .29f, .46f), new Vector3(.24f, .18f, .22f), White, 14);
        Ball(m, "Nose", new Vector3(0, .36f, .57f), new Vector3(.06f, .05f, .05f), dark, 60);
        foreach (int side in new[] { -1, 1 })
        {
            var ear = Ball(m, "Ear", new Vector3(side * .13f, .51f, .27f), new Vector3(.13f, .17f, .08f), fur);
            ear.localRotation = Quaternion.Euler(0, 0, side * -18);
            Ball(m, "Ear tip", new Vector3(side * .15f, .58f, .27f), new Vector3(.07f, .07f, .06f), dark);
        }
        Shadowed(Ball(m, "Tail", new Vector3(.04f, .27f, -.47f), new Vector3(.25f, .21f, .36f), fur));
        Ball(m, "Tail tip", new Vector3(.06f, .29f, -.62f), new Vector3(.17f, .16f, .15f), White, 10);
        Face(m, .45f, .40f);
        return new Color32(90, 200, 220, 255);
    }

    static Color32 Frog(Transform m, int variant)
    {
        Color32 skin = Pick(variant, new Color32(118, 206, 92, 255), new Color32(84, 196, 170, 255));
        Color32 belly = new Color32(214, 240, 160, 255);
        Shadowed(Ball(m, "Body", new Vector3(0, .17f, -.08f), new Vector3(.62f, .28f, .70f), skin, 40));
        Shadowed(Ball(m, "Head", new Vector3(0, .23f, .27f), new Vector3(.54f, .30f, .40f), skin, 40));
        Ball(m, "Chin", new Vector3(0, .17f, .36f), new Vector3(.40f, .16f, .26f), belly, 20);
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Eye bulb", new Vector3(side * .15f, .38f, .33f), new Vector3(.17f, .16f, .16f), skin, 40);
            Ball(m, "Eye white", new Vector3(side * .15f, .43f, .35f), new Vector3(.13f, .09f, .12f), White, 40);
            Ball(m, "Eye", new Vector3(side * .15f, .47f, .37f), new Vector3(.07f, .04f, .06f), Eye, 80);
            Ball(m, "Back leg", new Vector3(side * .28f, .09f, -.24f), new Vector3(.20f, .13f, .32f), skin, 30);
            Ball(m, "Front foot", new Vector3(side * .22f, .05f, .20f), new Vector3(.13f, .07f, .12f), skin, 30);
            Ball(m, "Spot", new Vector3(side * .16f, .30f, -.32f), new Vector3(.09f, .03f, .09f), new Color32(70, 150, 70, 255), 20);
            Ball(m, "Blush", new Vector3(side * .2f, .33f, .43f), new Vector3(.08f, .03f, .05f), Blush, 10);
        }
        return new Color32(255, 140, 170, 255);
    }

    static Color32 Penguin(Transform m)
    {
        var ink = new Color32(46, 54, 86, 255);
        var orange = new Color32(255, 160, 48, 255);
        Shadowed(Ball(m, "Body", new Vector3(0, .19f, -.07f), new Vector3(.52f, .32f, .80f), ink, 50));
        Shadowed(Ball(m, "Head", new Vector3(0, .27f, .31f), new Vector3(.42f, .36f, .36f), ink, 50));
        Ball(m, "Belly", new Vector3(0, .13f, -.05f), new Vector3(.46f, .2f, .74f), White, 20);
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Face", new Vector3(side * .09f, .40f, .39f), new Vector3(.13f, .06f, .12f), White, 20);
            Ball(m, "Eye", new Vector3(side * .09f, .43f, .41f), new Vector3(.05f, .04f, .05f), Eye, 80);
            var flipper = Ball(m, "Flipper", new Vector3(side * .30f, .17f, -.02f), new Vector3(.08f, .07f, .32f), ink, 40);
            flipper.localRotation = Quaternion.Euler(0, side * 18, 0);
            Ball(m, "Foot", new Vector3(side * .1f, .12f, -.47f), new Vector3(.12f, .05f, .12f), orange, 30);
            Ball(m, "Blush", new Vector3(side * .16f, .36f, .40f), new Vector3(.07f, .03f, .05f), Blush, 10);
        }
        Ball(m, "Beak", new Vector3(0, .31f, .52f), new Vector3(.12f, .08f, .14f), orange, 50);
        return new Color32(255, 110, 120, 255);
    }

    static Color32 Cat(Transform m, int variant)
    {
        Color32 fur = Pick(variant, new Color32(255, 160, 60, 255), new Color32(110, 150, 230, 255), new Color32(90, 84, 120, 255));
        Color32 stripe = Pick(variant, new Color32(214, 104, 30, 255), new Color32(70, 104, 190, 255), new Color32(56, 52, 80, 255));
        Body(m, fur, fur);
        for (int i = 0; i < 3; i++)
            Box(m, "Stripe", new Vector3(0, .495f - Mathf.Abs(i - 1) * .01f, .22f + i * .05f), new Vector3(.05f + .03f * (i % 2), .02f, .025f), stripe, 10);
        foreach (int side in new[] { -1, 1 })
        {
            var ear = Ball(m, "Ear", new Vector3(side * .14f, .51f, .27f), new Vector3(.13f, .15f, .09f), fur);
            ear.localRotation = Quaternion.Euler(0, 0, side * -22);
            Ball(m, "Ear inside", new Vector3(side * .14f, .52f, .30f), new Vector3(.06f, .08f, .03f), new Color32(255, 180, 190, 255), 10);
            Box(m, "Whisker", new Vector3(side * .2f, .38f, .48f), new Vector3(.14f, .01f, .01f), White, 10).localRotation = Quaternion.Euler(0, side * 12, 0);
        }
        var tail = Ball(m, "Tail", new Vector3(.12f, .25f, -.49f), new Vector3(.09f, .09f, .34f), fur);
        tail.localRotation = Quaternion.Euler(0, 35, 0);
        Ball(m, "Nose", new Vector3(0, .40f, .49f), new Vector3(.05f, .03f, .03f), new Color32(255, 130, 150, 255), 40);
        Face(m, .45f, .41f);
        return new Color32(160, 130, 255, 255);
    }

    static Color32 Bear(Transform m, int variant)
    {
        Color32 fur = Pick(variant, new Color32(176, 110, 62, 255), new Color32(150, 96, 70, 255), new Color32(222, 150, 70, 255));
        Color32 snout = Pick(variant, new Color32(240, 210, 170, 255), new Color32(226, 232, 242, 255), new Color32(250, 226, 186, 255));
        Body(m, fur, fur, .31f, .44f);
        Ball(m, "Muzzle", new Vector3(0, .30f, .47f), new Vector3(.22f, .16f, .17f), snout, 14);
        Ball(m, "Nose", new Vector3(0, .37f, .55f), new Vector3(.07f, .05f, .05f), Eye, 60);
        foreach (int side in new[] { -1, 1 })
        {
            Ball(m, "Ear", new Vector3(side * .16f, .49f, .27f), new Vector3(.13f, .12f, .10f), fur);
            Ball(m, "Ear inside", new Vector3(side * .16f, .51f, .29f), new Vector3(.07f, .05f, .04f), snout, 10);
        }
        Face(m, .46f, .40f);
        return new Color32(255, 120, 90, 255);
    }
}
