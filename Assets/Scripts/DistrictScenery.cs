using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fills each land tile with the scenery of its world (one world per district of 10 levels): trees, bushes,
/// ponds, fences and world specials. Layout is seeded by the tile position so a level always looks the same.
/// Tall props keep clear of the tile's far edge so they never hide the road; nothing gets an outline, so the
/// outlined animals always read as the game pieces.
/// </summary>
static class DistrictScenery
{
    // world index = ChapterArt.Theme(chapter)
    const int Meadow = 0, Blossom = 1, Snow = 2, Beach = 3, Desert = 4, Jungle = 5, Autumn = 6, Lavender = 7, Orchard = 8, Volcano = 9;

    static readonly Color32 WoodDark = new Color32(160, 112, 74, 255);
    static readonly Color32 Stone = new Color32(178, 184, 198, 255);
    static readonly Color32 White = new Color32(255, 255, 255, 255);

    public static void Decorate(Transform parent, Bounds tile, int world)
    {
        float width = tile.size.x, depth = tile.size.z;
        float shortSide = Mathf.Min(width, depth);
        if (shortSide < .42f)
            return;

        float top = tile.max.y;
        var rng = new System.Random(Mathf.RoundToInt(tile.center.x * 197 + tile.center.z * 911) & 0x7fffffff);
        var root = new GameObject("Tile scenery").transform;
        root.SetParent(parent, false);
        // props are placed in world coordinates
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        const float inset = .2f;
        var area = Rect.MinMaxRect(tile.min.x + inset, tile.min.z + inset, tile.max.x - inset, tile.max.z - inset);
        if (area.width <= 0 || area.height <= 0)
            return;
        var placed = new List<Vector3>(); // x, z, radius
        float size = area.width * area.height;

        if (shortSide < .9f)
        {
            Vector3 c = new Vector3(tile.center.x, top, tile.center.z);
            if (shortSide > .62f)
                Tree(root, c, world, Mathf.Min(shortSide * .37f, .45f), rng);
            else
                Small(root, c, world, rng);
            return;
        }

        if (shortSide > 1.3f && rng.NextDouble() < .6)
            Fence(root, tile, top, world);

        if (size > 3.2f && rng.NextDouble() < .7)
        {
            float rx = Mathf.Min(area.width * .22f, .7f), rz = Mathf.Min(area.height * .2f, .5f);
            if (TryPlace(rng, area, Mathf.Max(rx, rz) + .15f, placed, 0f, out Vector3 p))
                Pond(root, new Vector3(p.x, top, p.z), rx, rz, world, rng);
        }

        int trees = Mathf.Clamp(Mathf.RoundToInt(size / 1.5f), 1, 6);
        for (int i = 0; i < trees; i++)
        {
            float s = Mathf.Lerp(.32f, .48f, (float)rng.NextDouble());
            if (!TryPlace(rng, area, s * .75f, placed, s * .9f, out Vector3 p))
                break;
            Tree(root, new Vector3(p.x, top, p.z), world, s, rng);
        }

        int specials = Mathf.Clamp(Mathf.RoundToInt(size / 1.1f), 1, 8);
        for (int i = 0; i < specials; i++)
        {
            if (!TryPlace(rng, area, .17f, placed, .3f, out Vector3 p))
                break;
            Special(root, new Vector3(p.x, top, p.z), world, rng);
        }

        int smalls = Mathf.Clamp(Mathf.RoundToInt(size * 2.2f), 3, 22);
        for (int i = 0; i < smalls; i++)
        {
            if (!TryPlace(rng, area, .08f, placed, 0f, out Vector3 p))
                break;
            Small(root, new Vector3(p.x, top, p.z), world, rng);
        }
    }

    /// <summary>Random free spot; 'farMargin' keeps tall props away from the far (+z) edge seen above the road.</summary>
    static bool TryPlace(System.Random rng, Rect area, float radius, List<Vector3> placed, float farMargin, out Vector3 result)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            if (area.xMax - radius < area.xMin + radius || area.yMax - radius - farMargin < area.yMin + radius)
                break;
            float x = Mathf.Lerp(area.xMin + radius, area.xMax - radius, (float)rng.NextDouble());
            float z = Mathf.Lerp(area.yMin + radius, area.yMax - radius - farMargin, (float)rng.NextDouble());
            bool free = true;
            foreach (Vector3 q in placed)
            {
                float dx = x - q.x, dz = z - q.y;
                float min = radius + q.z;
                if (dx * dx + dz * dz < min * min) { free = false; break; }
            }
            if (free)
            {
                placed.Add(new Vector3(x, z, radius));
                result = new Vector3(x, 0, z);
                return true;
            }
        }
        result = Vector3.zero;
        return false;
    }

    // ------------------------------------------------------------------ building blocks

    static Transform Group(Transform parent, string name, Vector3 position, float scale, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = position;
        t.localScale = Vector3.one * scale;
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        return t;
    }

    static Transform Ball(Transform p, string name, Vector3 pos, Vector3 size, Color32 c, bool shadow = false, int gloss = 12)
    {
        var t = CartoonModels.Part(p, name, pos, size, c, false, gloss);
        if (shadow) t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        return t;
    }

    static Transform Box(Transform p, string name, Vector3 pos, Vector3 size, Color32 c, bool shadow = false, int gloss = 12)
    {
        var t = CartoonModels.Part(p, name, pos, size, c, true, gloss);
        if (shadow) t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        return t;
    }

    static Transform Glow(Transform t, Color32 c)
    {
        t.GetComponent<MeshRenderer>().sharedMaterial = CartoonModels.GlowFor(c);
        return t;
    }

    static float Range(System.Random rng, float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

    // ------------------------------------------------------------------ trees

    static void Tree(Transform parent, Vector3 origin, int world, float size, System.Random rng)
    {
        switch (world)
        {
            case Snow: Pine(parent, origin, size, rng); return;
            case Desert: Cactus(parent, origin, size, rng); return;
            case Beach: Palm(parent, origin, size / .42f, rng); return;
            case Volcano: Spire(parent, origin, size, rng); return;
        }

        Color32 leaves, light;
        switch (world)
        {
            case Blossom: leaves = new Color32(255, 170, 204, 255); light = new Color32(255, 206, 226, 255); break;
            case Jungle: leaves = new Color32(46, 150, 72, 255); light = new Color32(86, 186, 86, 255); break;
            case Autumn: leaves = new Color32(226, 132, 58, 255); light = new Color32(244, 182, 84, 255); break;
            case Lavender: leaves = new Color32(150, 172, 112, 255); light = new Color32(178, 198, 136, 255); break;
            default: leaves = new Color32(155, 199, 104, 255); light = new Color32(174, 214, 116, 255); break;
        }

        var tree = Group(parent, "Tree", origin, size, (float)rng.NextDouble() * 360);
        Ball(tree, "Trunk", new Vector3(0, .25f, 0), new Vector3(.24f, .58f, .24f), new Color32(178, 132, 95, 255), true);
        Ball(tree, "Crown", new Vector3(0, .82f, 0), new Vector3(.82f, .82f, .81f), light, true, 9);
        Ball(tree, "Left foliage", new Vector3(-.28f, .61f, .10f), new Vector3(.61f, .61f, .62f), leaves, true, 9);
        Ball(tree, "Right foliage", new Vector3(.27f, .67f, -.11f), new Vector3(.62f, .63f, .61f), leaves, true, 9);

        if (world == Jungle)
            for (int i = 0; i < 3; i++)
                Box(tree, "Vine", new Vector3(-.3f + i * .3f, .48f, .3f), new Vector3(.04f, .3f, .04f), new Color32(60, 128, 60, 255));
        if (world == Orchard)
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f;
                Ball(tree, "Apple", new Vector3(Mathf.Cos(a) * .36f, .7f + (i % 2) * .15f, Mathf.Sin(a) * .36f), new Vector3(.12f, .12f, .12f), new Color32(232, 52, 60, 255), false, 50);
            }
        if (world == Blossom)
            for (int i = 0; i < 4; i++)
                Ball(tree, "Fallen petal", new Vector3(Range(rng, -.6f, .6f), .02f, Range(rng, -.6f, .6f)), new Vector3(.1f, .02f, .07f), new Color32(255, 186, 214, 255));
    }

    static void Pine(Transform parent, Vector3 origin, float size, System.Random rng)
    {
        var tree = Group(parent, "Pine", origin, size, (float)rng.NextDouble() * 360);
        Ball(tree, "Pine trunk", new Vector3(0, .16f, 0), new Vector3(.18f, .34f, .20f), new Color32(169, 146, 129, 255), true);
        for (int i = 0; i < 3; i++)
        {
            float w = .93f - .24f * i;
            Ball(tree, "Pine bough", new Vector3(0, .34f + i * .21f, 0), new Vector3(w, .43f, w), new Color32(127, 170, 186, 255), true, 9);
            Ball(tree, "Snow", new Vector3(0, .43f + i * .21f, 0), new Vector3(w * .84f, .32f, w * .84f), new Color32(244, 250, 255, 255), false, 9);
        }
    }

    static void Cactus(Transform parent, Vector3 origin, float size, System.Random rng)
    {
        var tree = Group(parent, "Cactus", origin, size, (float)rng.NextDouble() * 360);
        var green = new Color32(150, 184, 123, 255);
        Ball(tree, "Cactus", new Vector3(0, .38f, 0), new Vector3(.43f, .82f, .43f), green, true);
        foreach (int side in new[] { -1, 1 })
            Ball(tree, "Cactus arm", new Vector3(side * .24f, .33f + .09f * side, 0), new Vector3(.23f, .49f, .26f), green, true)
                .localRotation = Quaternion.Euler(0, 0, -side * 32);
        Ball(tree, "Cactus flower", new Vector3(0, .8f, 0), new Vector3(.14f, .08f, .14f), new Color32(255, 120, 160, 255));
    }

    static void Spire(Transform parent, Vector3 origin, float size, System.Random rng)
    {
        var spire = Group(parent, "Basalt spire", origin, size, (float)rng.NextDouble() * 360);
        var rock = new Color32(70, 58, 66, 255);
        Box(spire, "Basalt", new Vector3(0, .25f, 0), new Vector3(.5f, .5f, .45f), rock, true, 20);
        Box(spire, "Basalt", new Vector3(.08f, .65f, 0), new Vector3(.34f, .4f, .32f), new Color32(84, 70, 78, 255), true, 20);
        Box(spire, "Basalt", new Vector3(.12f, .95f, .02f), new Vector3(.2f, .26f, .2f), rock, true, 20);
        var lava = new Color32(255, 120, 30, 255);
        Glow(Box(spire, "Lava seam", new Vector3(0, .5f, -.2f), new Vector3(.36f, .05f, .06f), lava), lava);
    }

    static void Palm(Transform parent, Vector3 c, float scale, System.Random rng)
    {
        var g = Group(parent, "Palm", c, scale, Range(rng, 0, 360));
        for (int i = 0; i < 4; i++)
            Ball(g, "Palm trunk", new Vector3(i * .015f, .05f + i * .08f, 0), new Vector3(.07f, .1f, .07f), new Color32(178, 128, 84, 255), true);
        for (int i = 0; i < 6; i++)
        {
            var leaf = Ball(g, "Palm leaf", new Vector3(.05f, .36f, 0), new Vector3(.07f, .03f, .3f), new Color32(96, 176, 84, 255), true);
            leaf.localRotation = Quaternion.Euler(18, i * 60, 0);
            leaf.localPosition += leaf.localRotation * new Vector3(0, 0, .12f);
        }
        Ball(g, "Coconut", new Vector3(.05f, .33f, .02f), new Vector3(.05f, .05f, .05f), new Color32(126, 86, 56, 255));
    }

    // ------------------------------------------------------------------ fence and pond

    static void Fence(Transform parent, Bounds tile, float top, int world)
    {
        Color32 wood = world == Snow ? new Color32(214, 170, 122, 255)
            : world == Blossom ? new Color32(236, 120, 110, 255)
            : world == Beach ? new Color32(236, 222, 196, 255)
            : world == Volcano ? new Color32(96, 82, 88, 255)
            : new Color32(196, 146, 98, 255);
        Color32 post = world == Volcano ? new Color32(60, 50, 56, 255) : world == Blossom ? new Color32(200, 90, 86, 255) : WoodDark;
        var root = new GameObject("Zoo fence").transform;
        root.SetParent(parent, false);
        const float inset = .13f;
        float x0 = tile.min.x + inset, x1 = tile.max.x - inset, z0 = tile.min.z + inset, z1 = tile.max.z - inset;
        // corners stay open (pedestrians wait there) and the far side is skipped so the fence never hides the road
        Side(root, new Vector3(x0 + .3f, top, z0), new Vector3(x1 - .3f, top, z0), wood, post);
        Side(root, new Vector3(x0, top, z0 + .3f), new Vector3(x0, top, z1 - .45f), wood, post);
        Side(root, new Vector3(x1, top, z0 + .3f), new Vector3(x1, top, z1 - .45f), wood, post);
    }

    static void Side(Transform parent, Vector3 a, Vector3 b, Color32 wood, Color32 post)
    {
        float length = Vector3.Distance(a, b);
        if (length < .3f)
            return;
        int posts = Mathf.Max(2, Mathf.RoundToInt(length / .32f) + 1);
        Quaternion along = Quaternion.LookRotation((b - a).normalized);
        for (int i = 0; i < posts; i++)
            Box(parent, "Fence post", Vector3.Lerp(a, b, i / (float)(posts - 1)) + Vector3.up * .065f, new Vector3(.045f, .13f, .045f), post, true);
        foreach (float h in new[] { .05f, .1f })
            Box(parent, "Fence rail", (a + b) * .5f + Vector3.up * h, new Vector3(.025f, .028f, length), wood, true).rotation = along;
    }

    static void Pond(Transform parent, Vector3 c, float rx, float rz, int world, System.Random rng)
    {
        var g = Group(parent, "Pond", c, 1f, 0);
        Color32 rim = world == Desert || world == Beach ? new Color32(236, 206, 150, 255)
            : world == Snow ? new Color32(236, 244, 250, 255)
            : world == Volcano ? new Color32(56, 46, 52, 255) : Stone;
        Color32 water = world == Snow ? new Color32(176, 222, 246, 255)
            : world == Beach ? new Color32(80, 214, 222, 255)
            : world == Jungle ? new Color32(70, 176, 170, 255)
            : new Color32(88, 186, 236, 255);
        Ball(g, "Pond rim", new Vector3(0, .005f, 0), new Vector3(rx * 2.3f, .04f, rz * 2.3f), rim, false, 10);

        if (world == Volcano)
        {
            var lava = new Color32(255, 110, 26, 255);
            Glow(Ball(g, "Lava", new Vector3(0, .02f, 0), new Vector3(rx * 2f, .03f, rz * 2f), lava), lava);
            var hot = new Color32(255, 210, 90, 255);
            for (int i = 0; i < 3; i++)
                Glow(Ball(g, "Lava bubble", new Vector3(Range(rng, -.5f, .5f) * rx, .035f, Range(rng, -.5f, .5f) * rz), new Vector3(.08f, .02f, .08f), hot), hot);
            return;
        }

        Ball(g, "Water", new Vector3(0, .02f, 0), new Vector3(rx * 2f, .03f, rz * 2f), water, false, world == Snow ? 85 : 92);
        Ball(g, "Water shine", new Vector3(-rx * .35f, .036f, rz * .3f), new Vector3(rx * .5f, .01f, rz * .2f), new Color32(220, 244, 255, 255), false, 60);

        if (world == Blossom)
            for (int i = 0; i < 3; i++)
            {
                float a = Range(rng, 0, Mathf.PI * 2), d = Range(rng, .2f, .6f);
                Ball(g, "Koi", new Vector3(Mathf.Cos(a) * rx * d, .036f, Mathf.Sin(a) * rz * d), new Vector3(.05f, .015f, .1f),
                    i == 1 ? White : new Color32(255, 128, 50, 255)).localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            }
        else if (world == Snow)
            Snowman(parent, c + new Vector3(rx + .12f, 0, -rz * .3f), rng);
        else if (world == Desert)
            Palm(parent, c + new Vector3(rx * .9f, 0, rz * .5f), 1f, rng);
        else if (world == Beach)
            for (int i = 0; i < 2; i++)
                Starfish(g, new Vector3(Range(rng, -.5f, .5f) * rx, .04f, Range(rng, -.5f, .5f) * rz), rng);
        else if (world == Autumn)
            for (int i = 0; i < 4; i++)
            {
                float a = Range(rng, 0, Mathf.PI * 2), d = Range(rng, .1f, .7f);
                Ball(g, "Floating leaf", new Vector3(Mathf.Cos(a) * rx * d, .038f, Mathf.Sin(a) * rz * d),
                    new Vector3(.07f, .01f, .045f), i % 2 == 0 ? new Color32(236, 120, 44, 255) : new Color32(246, 196, 70, 255));
            }
        else
            for (int i = 0; i < 3; i++)
            {
                float a = Range(rng, 0, Mathf.PI * 2), d = Range(rng, .2f, .6f);
                Vector3 p = new Vector3(Mathf.Cos(a) * rx * d, .038f, Mathf.Sin(a) * rz * d);
                Ball(g, "Lily pad", p, new Vector3(.13f, .01f, .13f), new Color32(102, 186, 86, 255));
                if (i == 0)
                    Ball(g, "Lily flower", p + new Vector3(.02f, .015f, 0), new Vector3(.05f, .03f, .05f), new Color32(255, 170, 200, 255));
            }
    }

    // ------------------------------------------------------------------ specials and small things

    static void Special(Transform parent, Vector3 c, int world, System.Random rng)
    {
        float roll = (float)rng.NextDouble();
        switch (world)
        {
            case Blossom:
                if (roll < .35f) Lantern(parent, c, rng);
                else if (roll < .7f) Bush(parent, c, new Color32(255, 170, 204, 255), new Color32(255, 214, 230, 255), rng);
                else FlowerBed(parent, c, rng);
                break;
            case Snow:
                if (roll < .35f) Snowman(parent, c, rng);
                else if (roll < .7f) IceRock(parent, c, rng);
                else Bush(parent, c, new Color32(150, 196, 200, 255), new Color32(240, 248, 255, 255), rng);
                break;
            case Beach:
                if (roll < .35f) Umbrella(parent, c, rng);
                else if (roll < .65f) Sandcastle(parent, c, rng);
                else if (roll < .85f) BeachBall(parent, c, rng);
                else Boulder(parent, c, new Color32(200, 190, 176, 255), rng);
                break;
            case Desert:
                if (roll < .45f) Boulder(parent, c, new Color32(214, 150, 96, 255), rng);
                else if (roll < .75f) Tumbleweed(parent, c, rng);
                else Bush(parent, c, new Color32(160, 176, 92, 255), new Color32(196, 206, 120, 255), rng);
                break;
            case Jungle:
                if (roll < .4f) Fern(parent, c, rng);
                else if (roll < .7f) Hibiscus(parent, c, rng);
                else Boulder(parent, c, new Color32(120, 150, 110, 255), rng);
                break;
            case Autumn:
                if (roll < .4f) Pumpkin(parent, c, rng);
                else if (roll < .65f) HayBale(parent, c, rng);
                else if (roll < .85f) Mushrooms(parent, c, rng);
                else Bush(parent, c, new Color32(226, 132, 58, 255), new Color32(244, 182, 84, 255), rng);
                break;
            case Lavender:
                if (roll < .6f) LavenderBush(parent, c, rng);
                else if (roll < .8f) Beehive(parent, c, rng);
                else Boulder(parent, c, new Color32(226, 218, 200, 255), rng);
                break;
            case Orchard:
                if (roll < .3f) Beehive(parent, c, rng);
                else Bush(parent, c, new Color32(106, 180, 86, 255), new Color32(255, 170, 200, 255), rng, true);
                break;
            case Volcano:
                if (roll < .4f) Boulder(parent, c, new Color32(76, 64, 72, 255), rng, true);
                else if (roll < .7f) Crystals(parent, c, rng);
                else Vent(parent, c, rng);
                break;
            default:
                if (roll < .55f) Bush(parent, c, new Color32(92, 172, 72, 255), new Color32(132, 204, 92, 255), rng, roll < .25f);
                else if (roll < .8f) FlowerBed(parent, c, rng);
                else Boulder(parent, c, Stone, rng);
                break;
        }
    }

    static void Small(Transform parent, Vector3 c, int world, System.Random rng)
    {
        float roll = (float)rng.NextDouble();
        switch (world)
        {
            case Blossom:
                Flower(parent, c, roll < .5f ? new Color32(255, 186, 214, 255) : White, new Color32(255, 214, 72, 255));
                break;
            case Snow:
                if (roll < .5f) Ball(parent, "Snow mound", c + Vector3.up * .01f, new Vector3(.12f, .05f, .1f), new Color32(246, 251, 255, 255));
                else Pebble(parent, c, new Color32(196, 214, 232, 255), rng);
                break;
            case Beach:
                if (roll < .5f) Ball(parent, "Shell", c + Vector3.up * .01f, new Vector3(.07f, .03f, .06f), roll < .25f ? new Color32(255, 220, 206, 255) : new Color32(255, 246, 230, 255), false, 40);
                else Pebble(parent, c, new Color32(214, 200, 176, 255), rng);
                break;
            case Desert:
                Pebble(parent, c, roll < .5f ? new Color32(190, 140, 92, 255) : new Color32(222, 178, 120, 255), rng);
                break;
            case Jungle:
                Flower(parent, c, roll < .5f ? new Color32(255, 80, 110, 255) : new Color32(255, 176, 46, 255), new Color32(255, 236, 120, 255));
                break;
            case Autumn:
                if (roll < .5f) Flower(parent, c, new Color32(246, 168, 60, 255), new Color32(170, 80, 40, 255));
                else Ball(parent, "Leaf pile", c + Vector3.up * .012f, new Vector3(.12f, .04f, .1f), new Color32(220, 112, 50, 255));
                break;
            case Lavender:
                Flower(parent, c, roll < .6f ? new Color32(160, 128, 226, 255) : White, new Color32(255, 236, 160, 255));
                break;
            case Orchard:
                Flower(parent, c, roll < .5f ? new Color32(255, 182, 206, 255) : White, new Color32(255, 214, 72, 255));
                break;
            case Volcano:
                if (roll < .4f)
                {
                    var ember = new Color32(255, 150, 50, 255);
                    Glow(Ball(parent, "Ember", c + Vector3.up * .01f, new Vector3(.04f, .02f, .04f), ember), ember);
                }
                else Pebble(parent, c, new Color32(64, 54, 60, 255), rng);
                break;
            default:
                if (roll < .75f)
                    Flower(parent, c, roll < .3f ? White : roll < .55f ? new Color32(255, 214, 72, 255) : new Color32(180, 150, 255, 255),
                        roll < .3f ? new Color32(255, 206, 60, 255) : new Color32(255, 244, 200, 255));
                else
                    Pebble(parent, c, Stone, rng);
                break;
        }
    }

    static void Flower(Transform parent, Vector3 c, Color32 petals, Color32 middle)
    {
        Ball(parent, "Flower", c + Vector3.up * .02f, new Vector3(.075f, .03f, .075f), petals);
        Ball(parent, "Flower middle", c + Vector3.up * .035f, new Vector3(.03f, .02f, .03f), middle);
    }

    static void Pebble(Transform parent, Vector3 c, Color32 color, System.Random rng)
    {
        float s = Range(rng, .05f, .09f);
        Ball(parent, "Pebble", c + Vector3.up * s * .2f, new Vector3(s * 1.2f, s * .55f, s), color, false, 20);
    }

    static void FlowerBed(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Flower bed", c, 1f, Range(rng, 0, 360));
        Ball(g, "Leaves", new Vector3(0, .03f, 0), new Vector3(.34f, .07f, .24f), new Color32(84, 160, 68, 255), true);
        Color32[] colors = { new Color32(255, 96, 120, 255), new Color32(255, 206, 60, 255), new Color32(170, 130, 255, 255), White };
        Color32 middle = new Color32(255, 236, 140, 255);
        for (int i = 0; i < 7; i++)
        {
            var p = new Vector3(Range(rng, -.13f, .13f), .07f, Range(rng, -.08f, .08f));
            Ball(g, "Bed flower", p, new Vector3(.075f, .03f, .075f), colors[rng.Next(colors.Length)], false, 20);
            Ball(g, "Bed flower middle", p + Vector3.up * .015f, new Vector3(.028f, .02f, .028f), middle);
        }
    }

    static void Bush(Transform parent, Vector3 c, Color32 leaves, Color32 accent, System.Random rng, bool blossoms = false)
    {
        var g = Group(parent, "Bush", c, Range(rng, .85f, 1.15f), Range(rng, 0, 360));
        Ball(g, "Bush", new Vector3(0, .07f, 0), new Vector3(.2f, .16f, .2f), leaves, true);
        Ball(g, "Bush", new Vector3(.1f, .055f, .04f), new Vector3(.15f, .12f, .15f), leaves, true);
        Ball(g, "Bush top", new Vector3(-.03f, .11f, -.02f), new Vector3(.12f, .08f, .12f), accent);
        if (blossoms)
            for (int i = 0; i < 3; i++)
                Ball(g, "Blossom", new Vector3(Range(rng, -.07f, .1f), .14f, Range(rng, -.06f, .06f)), new Vector3(.04f, .03f, .04f), new Color32(255, 150, 190, 255));
    }

    static void Boulder(Transform parent, Vector3 c, Color32 color, System.Random rng, bool lavaSeam = false)
    {
        var g = Group(parent, "Boulder", c, Range(rng, .8f, 1.2f), Range(rng, 0, 360));
        Ball(g, "Rock", new Vector3(0, .05f, 0), new Vector3(.2f, .13f, .16f), color, true, 18);
        Ball(g, "Rock", new Vector3(.11f, .03f, .05f), new Vector3(.1f, .07f, .09f), color, true, 18);
        Ball(g, "Rock light", new Vector3(-.03f, .1f, -.02f), new Vector3(.09f, .03f, .07f), Color32.Lerp(color, White, .35f));
        if (lavaSeam)
        {
            var lava = new Color32(255, 130, 40, 255);
            Glow(Box(g, "Lava seam", new Vector3(.02f, .07f, -.06f), new Vector3(.12f, .02f, .02f), lava), lava);
        }
    }

    static void IceRock(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Ice", c, Range(rng, .8f, 1.1f), Range(rng, 0, 360));
        Box(g, "Ice block", new Vector3(0, .06f, 0), new Vector3(.14f, .13f, .12f), new Color32(170, 220, 246, 255), true, 80).localRotation = Quaternion.Euler(0, 20, 8);
        Box(g, "Ice block", new Vector3(.09f, .04f, .05f), new Vector3(.09f, .08f, .08f), new Color32(200, 236, 252, 255), true, 80).localRotation = Quaternion.Euler(0, -15, -6);
    }

    static void Snowman(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Snowman", c, 1f, Range(rng, 150, 210));
        var snow = new Color32(250, 252, 255, 255);
        Ball(g, "Snow body", new Vector3(0, .07f, 0), new Vector3(.17f, .15f, .17f), snow, true, 20);
        Ball(g, "Snow head", new Vector3(0, .18f, 0), new Vector3(.12f, .11f, .12f), snow, true, 20);
        Box(g, "Scarf", new Vector3(0, .135f, 0), new Vector3(.13f, .03f, .13f), new Color32(236, 72, 88, 255));
        Ball(g, "Carrot", new Vector3(0, .185f, -.07f), new Vector3(.025f, .025f, .06f), new Color32(255, 140, 40, 255));
        Box(g, "Hat", new Vector3(0, .245f, 0), new Vector3(.08f, .05f, .08f), new Color32(60, 70, 110, 255));
    }

    static void Tumbleweed(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Tumbleweed", c, Range(rng, .8f, 1.1f), Range(rng, 0, 360));
        Ball(g, "Tumbleweed", new Vector3(0, .05f, 0), new Vector3(.12f, .1f, .12f), new Color32(200, 168, 110, 255), true);
        Ball(g, "Tumbleweed", new Vector3(.03f, .07f, .02f), new Vector3(.08f, .07f, .08f), new Color32(222, 190, 128, 255));
    }

    static void Pumpkin(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Pumpkin", c, Range(rng, .8f, 1.2f), Range(rng, 0, 360));
        Ball(g, "Pumpkin", new Vector3(0, .05f, 0), new Vector3(.15f, .1f, .13f), new Color32(255, 136, 36, 255), true, 30);
        Ball(g, "Pumpkin rib", new Vector3(.05f, .05f, 0), new Vector3(.09f, .095f, .12f), new Color32(244, 120, 30, 255), false, 30);
        Ball(g, "Pumpkin rib", new Vector3(-.05f, .05f, 0), new Vector3(.09f, .095f, .12f), new Color32(244, 120, 30, 255), false, 30);
        Box(g, "Stem", new Vector3(0, .11f, 0), new Vector3(.025f, .04f, .025f), new Color32(96, 140, 60, 255));
    }

    static void HayBale(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Hay bale", c, 1f, Range(rng, 0, 90));
        Box(g, "Hay", new Vector3(0, .06f, 0), new Vector3(.22f, .12f, .14f), new Color32(240, 200, 96, 255), true, 8);
        foreach (float x in new[] { -.05f, .05f })
            Box(g, "Hay band", new Vector3(x, .061f, 0), new Vector3(.015f, .125f, .145f), new Color32(184, 132, 64, 255));
    }

    static void Mushrooms(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Mushrooms", c, 1f, Range(rng, 0, 360));
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector3(Range(rng, -.06f, .06f), 0, Range(rng, -.06f, .06f));
            float s = Range(rng, .7f, 1.1f);
            Box(g, "Mushroom stem", p + new Vector3(0, .03f * s, 0), new Vector3(.03f, .06f, .03f) * s, new Color32(250, 240, 224, 255));
            Ball(g, "Mushroom cap", p + new Vector3(0, .065f * s, 0), new Vector3(.08f, .05f, .08f) * s, new Color32(226, 64, 64, 255), true, 30);
            Ball(g, "Cap dot", p + new Vector3(.015f, .088f * s, 0), new Vector3(.02f, .01f, .02f) * s, White);
        }
    }

    static void Beehive(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Beehive", c, 1f, Range(rng, 160, 200));
        var honey = new Color32(255, 200, 70, 255);
        Ball(g, "Hive", new Vector3(0, .05f, 0), new Vector3(.16f, .09f, .16f), honey, true, 30);
        Ball(g, "Hive", new Vector3(0, .11f, 0), new Vector3(.13f, .08f, .13f), honey, true, 30);
        Ball(g, "Hive", new Vector3(0, .16f, 0), new Vector3(.08f, .06f, .08f), honey, true, 30);
        Ball(g, "Hive door", new Vector3(0, .05f, -.075f), new Vector3(.04f, .035f, .02f), new Color32(110, 70, 40, 255));
        Ball(g, "Bee", new Vector3(.1f, .2f, -.05f), new Vector3(.04f, .03f, .05f), new Color32(255, 216, 50, 255));
    }

    // ------------------------------------------------------------------ props of the newer worlds

    static void Lantern(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Stone lantern", c, 1f, Range(rng, 0, 90));
        var stone = new Color32(196, 196, 204, 255);
        Box(g, "Lantern base", new Vector3(0, .02f, 0), new Vector3(.14f, .04f, .14f), stone, true);
        Box(g, "Lantern post", new Vector3(0, .08f, 0), new Vector3(.05f, .1f, .05f), stone, true);
        var light = new Color32(255, 214, 120, 255);
        Glow(Box(g, "Lantern light", new Vector3(0, .15f, 0), new Vector3(.08f, .06f, .08f), light), light);
        Box(g, "Lantern roof", new Vector3(0, .2f, 0), new Vector3(.16f, .04f, .16f), new Color32(150, 150, 164, 255), true);
    }

    static void Umbrella(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Beach umbrella", c, 1f, Range(rng, 0, 360));
        Color32 cloth = rng.NextDouble() < .5 ? new Color32(255, 92, 110, 255) : new Color32(60, 170, 255, 255);
        Box(g, "Umbrella pole", new Vector3(0, .1f, 0), new Vector3(.015f, .2f, .015f), White);
        Ball(g, "Umbrella", new Vector3(0, .2f, 0), new Vector3(.3f, .06f, .3f), cloth, true, 30);
        Ball(g, "Umbrella stripe", new Vector3(0, .215f, 0), new Vector3(.16f, .045f, .16f), White, false, 30);
        Box(g, "Towel", new Vector3(.12f, .006f, .04f), new Vector3(.12f, .008f, .2f), new Color32(255, 214, 80, 255));
    }

    static void Sandcastle(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Sandcastle", c, 1f, Range(rng, 0, 90));
        var sand = new Color32(240, 206, 140, 255);
        Box(g, "Castle", new Vector3(0, .04f, 0), new Vector3(.18f, .08f, .14f), sand, true);
        foreach (int x in new[] { -1, 1 })
            Box(g, "Tower", new Vector3(x * .08f, .08f, 0), new Vector3(.06f, .12f, .06f), sand, true);
        Box(g, "Flag", new Vector3(.08f, .17f, 0), new Vector3(.04f, .03f, .005f), new Color32(255, 92, 110, 255));
    }

    static void BeachBall(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Beach ball", c, 1f, Range(rng, 0, 360));
        Ball(g, "Ball", new Vector3(0, .05f, 0), new Vector3(.1f, .1f, .1f), new Color32(255, 92, 110, 255), true, 50);
        Ball(g, "Ball band", new Vector3(0, .05f, 0), new Vector3(.104f, .104f, .05f), White, false, 50);
        Ball(g, "Ball band", new Vector3(0, .05f, 0), new Vector3(.05f, .104f, .104f), new Color32(60, 170, 255, 255), false, 50);
    }

    static void Starfish(Transform parent, Vector3 p, System.Random rng)
    {
        var color = new Color32(255, 130, 90, 255);
        float turn = Range(rng, 0, 72);
        for (int i = 0; i < 5; i++)
            Box(parent, "Starfish arm", p, new Vector3(.02f, .01f, .07f), color).localRotation = Quaternion.Euler(0, i * 72 + turn, 0);
    }

    static void Fern(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Fern", c, Range(rng, .9f, 1.2f), Range(rng, 0, 360));
        for (int i = 0; i < 6; i++)
        {
            var leaf = Ball(g, "Fern leaf", new Vector3(0, .05f, 0), new Vector3(.06f, .03f, .2f),
                i % 2 == 0 ? new Color32(46, 140, 64, 255) : new Color32(70, 168, 76, 255), true);
            leaf.localRotation = Quaternion.Euler(-20, i * 60, 0);
            leaf.localPosition += leaf.localRotation * new Vector3(0, 0, .08f);
        }
    }

    static void Hibiscus(Transform parent, Vector3 c, System.Random rng)
    {
        Bush(parent, c, new Color32(46, 140, 64, 255), new Color32(70, 168, 76, 255), rng);
        var color = rng.NextDouble() < .5 ? new Color32(255, 70, 100, 255) : new Color32(255, 160, 40, 255);
        for (int i = 0; i < 3; i++)
            Ball(parent, "Hibiscus", c + new Vector3(Range(rng, -.08f, .08f), .15f, Range(rng, -.06f, .06f)), new Vector3(.07f, .03f, .07f), color);
    }

    static void LavenderBush(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Lavender bush", c, Range(rng, .9f, 1.15f), Range(rng, 0, 360));
        Ball(g, "Lavender leaves", new Vector3(0, .04f, 0), new Vector3(.22f, .08f, .18f), new Color32(110, 150, 90, 255), true);
        for (int i = 0; i < 7; i++)
            Ball(g, "Lavender", new Vector3(Range(rng, -.08f, .08f), .1f + Range(rng, 0, .03f), Range(rng, -.06f, .06f)), new Vector3(.035f, .09f, .035f),
                i % 2 == 0 ? new Color32(150, 120, 222, 255) : new Color32(178, 150, 236, 255), true);
    }

    static void Crystals(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Crystals", c, Range(rng, .9f, 1.2f), Range(rng, 0, 360));
        var glow = rng.NextDouble() < .5 ? new Color32(255, 140, 60, 255) : new Color32(190, 110, 255, 255);
        for (int i = 0; i < 3; i++)
            Glow(Box(g, "Crystal", new Vector3(i * .04f - .04f, .06f + i * .01f, Range(rng, -.03f, .03f)), new Vector3(.04f, .12f + i * .03f, .04f), glow), glow)
                .localRotation = Quaternion.Euler(Range(rng, -18, 18), i * 30, Range(rng, -18, 18));
    }

    static void Vent(Transform parent, Vector3 c, System.Random rng)
    {
        var g = Group(parent, "Lava vent", c, 1f, Range(rng, 0, 360));
        Ball(g, "Vent rock", new Vector3(0, .03f, 0), new Vector3(.2f, .06f, .2f), new Color32(60, 50, 56, 255), true, 18);
        var lava = new Color32(255, 120, 30, 255);
        Glow(Ball(g, "Vent lava", new Vector3(0, .055f, 0), new Vector3(.1f, .02f, .1f), lava), lava);
    }
}
