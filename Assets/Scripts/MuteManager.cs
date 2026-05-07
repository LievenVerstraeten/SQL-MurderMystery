// MuteManager.cs
// Toggles global audio mute via AudioListener.volume + AudioListener.pause.
// Icon swapping is done via USS class swap — no texture loading in C#.
//
// SETUP CHECKLIST — all four steps are required:
//
//   1. Add to GameUI.uss:
//         .mute-icon-on  { background-image: url('project://database/Assets/Textures/Graphics/icons/UI_VolumeOn.png'); }
//         .mute-icon-off { background-image: url('project://database/Assets/Textures/Graphics/icons/UI_Muted.png'); }
//
//   2. In GameUI.uxml, add this as the FIRST button inside burger-menu-dropdown:
//         <ui:Button tooltip="Toggle Sound" name="mute-button" class="menu-button">
//             <ui:VisualElement name="mute-icon" class="btn-icon mute-icon-on" />
//         </ui:Button>
//
//   3. Attach this script to the same persistent GameObject as GameManager.
//
//   4. GameUIManager.OnEnable() already calls MuteManager.Instance?.ConnectToUI(uiDocument).

using UnityEngine;
using UnityEngine.UIElements;

public class MuteManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static MuteManager Instance { get; private set; }

    private const string PREFS_KEY = "audio_muted";

    private bool _isMuted;
    private Button _muteButton;
    private VisualElement _muteIcon;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _isMuted = PlayerPrefs.GetInt(PREFS_KEY, 0) == 1;
        ApplyMute();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void ConnectToUI(UIDocument uiDocument)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        if (_muteButton != null)
            _muteButton.clicked -= ToggleMute;

        var root = uiDocument.rootVisualElement;
        _muteButton = root.Q<Button>("mute-button");
        _muteIcon = root.Q<VisualElement>("mute-icon");

        if (_muteButton == null)
        {
            Debug.LogError("[MuteManager] 'mute-button' not found in UIDocument.");
            return;
        }

        if (_muteIcon == null)
        {
            Debug.LogError("[MuteManager] 'mute-icon' not found. " +
                           "Make sure the VisualElement inside mute-button is named 'mute-icon' in the UXML.");
            return;
        }

        _muteButton.clicked += ToggleMute;
        UpdateIcon();

        Debug.Log($"[MuteManager] Connected. Currently {(_isMuted ? "muted" : "unmuted")}.");
    }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
        ApplyMute();
        PlayerPrefs.SetInt(PREFS_KEY, _isMuted ? 1 : 0);
        PlayerPrefs.Save();
        UpdateIcon();
        Debug.Log($"[MuteManager] Toggled → {(_isMuted ? "MUTED" : "UNMUTED")}. " +
                  $"Icon classes: {_muteIcon?.GetClasses()}");
    }

    public bool IsMuted => _isMuted;

    // ─── Internal helpers ─────────────────────────────────────────────────────

    private void ApplyMute()
    {
        AudioListener.volume = _isMuted ? 0f : 1f;
        AudioListener.pause = _isMuted;
    }

    private void UpdateIcon()
    {
        if (_muteIcon == null) return;

        if (_isMuted)
        {
            _muteIcon.RemoveFromClassList("mute-icon-on");
            _muteIcon.AddToClassList("mute-icon-off");
        }
        else
        {
            _muteIcon.RemoveFromClassList("mute-icon-off");
            _muteIcon.AddToClassList("mute-icon-on");
        }
    }

    private void OnDestroy()
    {
        if (_muteButton != null)
            _muteButton.clicked -= ToggleMute;
    }
}