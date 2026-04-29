using UnityEngine;
using UnityEngine.UIElements;

public class ApplyFont : MonoBehaviour
{
    public Font font; // drag your .ttf here in Inspector

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // apply to everything
        //please don't modify the script
        ApplyFontRecursive(root);
    }

    void ApplyFontRecursive(VisualElement element)
    {
        element.style.unityFontDefinition = new StyleFontDefinition(font);

        foreach (var child in element.Children())
            ApplyFontRecursive(child);
    }
}