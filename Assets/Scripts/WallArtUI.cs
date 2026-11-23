using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared look for the app's UI (colours, rounded shapes) and small helpers for building UI in code.
/// </summary>
public static class WallArtUI
{
    /// <summary>The canvas is laid out for this width and scaled to fit the device.</summary>
    public const float ReferenceWidth = 1080f;

    // The project renders in linear colour space, where translucent dark panels let bright
    // backgrounds through much more than the alpha suggests - hence the high alphas.
    public static readonly Color Sheet = new Color(0.07f, 0.08f, 0.10f, 1f);
    public static readonly Color Bar = new Color(0.07f, 0.08f, 0.10f, 0.96f);
    public static readonly Color Control = new Color(0.18f, 0.20f, 0.25f, 0.95f);
    public static readonly Color Accent = new Color(0.16f, 0.65f, 0.42f, 1f);
    public static readonly Color Danger = new Color(1f, 0.45f, 0.42f, 1f);
    public static readonly Color Text = Color.white;
    public static readonly Color TextDim = new Color(0.78f, 0.80f, 0.84f, 1f);

    static readonly Dictionary<int, Sprite> s_Rounded = new Dictionary<int, Sprite>();
    static TMP_FontAsset s_Font;

    public static TMP_FontAsset Font
    {
        get
        {
            if (s_Font == null) s_Font = TMP_Settings.defaultFontAsset;
            return s_Font;
        }
        set { if (value != null) s_Font = value; }
    }

    /// <summary>Canvas units per screen pixel when the canvas scales with <see cref="ReferenceWidth"/>.</summary>
    public static float UnitsPerPixel => Screen.width > 0 ? ReferenceWidth / Screen.width : 1f;

    /// <summary>Height of the area under the safe area (home indicator), in canvas units.</summary>
    public static float BottomInset => Mathf.Max(0f, Screen.safeArea.yMin) * UnitsPerPixel;

    /// <summary>Height of the area above the safe area (notch / Dynamic Island), in canvas units.</summary>
    public static float TopInset => Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) * UnitsPerPixel;

    /// <summary>A white rounded rectangle that can be stretched to any size (9-sliced).</summary>
    public static Sprite Rounded(int radius)
    {
        radius = Mathf.Clamp(radius, 2, 64);
        if (s_Rounded.TryGetValue(radius, out Sprite cached) && cached != null) return cached;

        int size = radius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Rounded" + radius,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded box, for a 1px anti-aliased edge
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = radius + 1;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        s_Rounded[radius] = sprite;
        return sprite;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Color color, int cornerRadius = 0)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (cornerRadius > 0) SetRounded(image, cornerRadius);
        return image;
    }

    public static void SetRounded(Image image, int cornerRadius)
    {
        image.sprite = Rounded(cornerRadius);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    public static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) label.font = Font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    /// <summary>A rounded button with a centred text label.</summary>
    public static Button CreateButton(string name, Transform parent, string text, float fontSize, int cornerRadius = 28)
    {
        Image image = CreateImage(name, parent, Control, cornerRadius);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (!string.IsNullOrEmpty(text))
        {
            TextMeshProUGUI label = CreateLabel("Label", image.transform, text, fontSize, Text);
            Stretch(label.rectTransform);
        }
        return button;
    }

    /// <summary>Makes a rect fill its parent, with optional margins.</summary>
    public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Anchors a rect to a point of its parent with a fixed size.</summary>
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static LayoutElement SetPreferredSize(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.minWidth = width;
        element.minHeight = height;
        return element;
    }

    /// <summary>Draws a "+" (or an "x" when rotated) out of two bars, so no icon font is needed.</summary>
    public static RectTransform CreateCross(Transform parent, float size, float thickness, Color color, bool diagonal)
    {
        RectTransform root = CreateRect(diagonal ? "IconClose" : "IconPlus", parent);
        Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        for (int i = 0; i < 2; i++)
        {
            Image bar = CreateImage("Bar", root, color, Mathf.Max(2, Mathf.RoundToInt(thickness * 0.5f)));
            bar.raycastTarget = false;
            Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                i == 0 ? new Vector2(size, thickness) : new Vector2(thickness, size));
        }
        if (diagonal) root.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return root;
    }
}
