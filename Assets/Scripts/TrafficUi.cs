using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

static class TrafficUi
{
    public static readonly Color Ink = new Color32(29, 42, 94, 255);
    public static readonly Color Paper = new Color32(255, 253, 249, 255);
    public static readonly Color Blue = new Color32(101, 137, 245, 255);
    public static readonly Color Mint = new Color32(180, 213, 169, 255);
    public static readonly Color Gold = new Color32(255, 196, 66, 255);
    public static readonly Color Coral = new Color32(245, 143, 125, 255);
    public static readonly Color Muted = new Color32(142, 152, 174, 255);
    static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    static Font font;

    public static RectTransform Place(Transform parent, string name, Vector2 anchor, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static bool IsSurface(string name) => name == "airy_white" || name == "airy_blue" ||
        name == "airy_lavender" || name == "airy_gray" || name == "airy_card";

    static Sprite SpriteFor(string name)
    {
        if (Sprites.TryGetValue(name, out Sprite sprite) && sprite != null)
            return sprite;
        Texture2D texture = Resources.Load<Texture2D>("TrafficUi/" + name);
        if (texture == null)
            throw new System.InvalidOperationException("Missing UI artwork: " + name);
        Vector4 border = name == "panel" ? Vector4.one * 25 : name == "airy_card" ? Vector4.one * 122 : IsSurface(name) ? Vector4.one * 50 : Vector4.zero;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, border);
        Sprites[name] = sprite;
        return sprite;
    }

    public static Image Paint(RectTransform rect, Color color, string art = "panel")
    {
        var image = rect.gameObject.AddComponent<Image>();
        SetArt(image, art);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static void SetArt(Image image, string art)
    {
        image.sprite = art == null ? null : SpriteFor(art);
        bool sliced = art == "panel" || IsSurface(art);
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = IsSurface(art) ? 1.15f : 1;
        image.preserveAspect = art != null && !sliced;
    }

    public static string Finish(Color color) => color == Blue ? "airy_blue" : color == Muted ? "airy_gray" : "airy_white";

    public static Text Label(RectTransform rect, string value, int size, Color color)
    {
        if (font == null)
            font = Resources.Load<Font>("Fonts/NunitoExtraBold");
        var label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = size;
        label.text = value;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    public static Button Button(RectTransform rect, Color color, UnityEngine.Events.UnityAction click, string art = null)
    {
        Image face = Paint(rect, Color.white, art ?? Finish(color));
        face.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        var colors = button.colors;
        colors.pressedColor = new Color(.91f, .93f, 1);
        colors.disabledColor = new Color(.84f, .86f, .91f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(click);
        rect.gameObject.AddComponent<ScenicButtonMotion>();
        if (art != null)
        {
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.15f, .20f, .34f, .10f);
            shadow.effectDistance = new Vector2(0, -7);
        }
        return button;
    }
}
