using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to any GameObject in the scene (e.g. the Canvas or an empty "Styler" object).
/// It finds all uGUI Buttons under the Canvas and applies a black + white-border pixel style.
/// </summary>
public class MainMenuStyler : MonoBehaviour
{
    [Header("Style Settings")]
    public int borderWidth = 2;
    public Color backgroundColor = Color.black;
    public Color borderColor = Color.white;
    public Color textColor = Color.white;
    public int fontSize = 20;

    void Start()
    {
        Sprite buttonSprite = CreateBorderedSprite(200, 50, borderWidth, backgroundColor, borderColor);

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            StyleButton(btn, buttonSprite);
        }
    }

    void StyleButton(Button btn, Sprite sprite)
    {
        // --- Image ---
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white; // tint must be white so the sprite colors show
        }

        // --- ColorBlock (hover / press states) ---
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;

        // --- Text ---
        Text legacyText = btn.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.color = textColor;
            legacyText.fontSize = fontSize;
            legacyText.alignment = TextAnchor.MiddleCenter;
        }

        // TextMeshPro support
        TMPro.TextMeshProUGUI tmpText = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.color = textColor;
            tmpText.fontSize = fontSize;
            tmpText.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }

    Sprite CreateBorderedSprite(int width, int height, int border, Color fill, Color outline)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // crisp pixel look

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < border || x >= width - border
                             || y < border || y >= height - border;
                pixels[y * width + x] = isBorder ? outline : fill;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Vector4 sliceBorder = new Vector4(border, border, border, border);
        Rect rect = new Rect(0, 0, width, height);
        return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, sliceBorder);
    }
}
