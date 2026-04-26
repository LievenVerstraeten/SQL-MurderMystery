// LoadGameManager.cs
// Displays all saved detective profiles as cards on the Load Game screen.
// Each card shows the profile name, active case name, and last played timestamp.
// Clicking a card calls GameManager.LoadProfile() to resume that investigation.
//
// SETUP:
//   Attach to the GameObject with the UIDocument component in the LoadGame scene.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class LoadGameManager : MonoBehaviour
{
    private UIDocument uiDocument;

    private Label titleLabel;
    private Button backButton;
    private ScrollView profileScroll;
    private VisualElement profileList;
    private Label noProfilesLabel;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[LoadGameManager] UIDocument missing or root is null.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Query all elements by name
        titleLabel = root.Q<Label>("TitleLabel");
        backButton = root.Q<Button>("BackButton");
        profileScroll = root.Q<ScrollView>("ProfileScroll");
        profileList = root.Q<VisualElement>("ProfileList");
        noProfilesLabel = root.Q<Label>("NoProfilesLabel");

        // Warn on missing elements
        if (titleLabel == null) Debug.LogError("[LoadGameManager] 'TitleLabel' not found.");
        if (backButton == null) Debug.LogError("[LoadGameManager] 'BackButton' not found.");
        if (profileScroll == null) Debug.LogError("[LoadGameManager] 'ProfileScroll' not found.");
        if (profileList == null) Debug.LogError("[LoadGameManager] 'ProfileList' not found.");
        if (noProfilesLabel == null) Debug.LogError("[LoadGameManager] 'NoProfilesLabel' not found.");

        // Set title
        if (titleLabel != null)
            titleLabel.text = "Load Game";

        // Wire back button
        backButton?.RegisterCallback<ClickEvent>(OnBackClicked);

        // Populate profile cards
        PopulateProfiles();
    }

    private void OnDisable()
    {
        backButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
    }

    // =========================================================================
    // PROFILE POPULATION
    // =========================================================================

    private void PopulateProfiles()
    {
        if (profileList == null) return;

        profileList.Clear();

        var profiles = DatabaseManager.Instance.GetAllProfiles();

        if (profiles == null || profiles.Count == 0)
        {
            // No profiles — show the empty state label
            if (noProfilesLabel != null)
                noProfilesLabel.style.display = DisplayStyle.Flex;
            if (profileScroll != null)
                profileScroll.style.display = DisplayStyle.None;
            return;
        }

        // Hide empty state, show scroll
        if (noProfilesLabel != null)
            noProfilesLabel.style.display = DisplayStyle.None;
        if (profileScroll != null)
            profileScroll.style.display = DisplayStyle.Flex;

        foreach (var profile in profiles)
        {
            var card = BuildProfileCard(profile);
            profileList.Add(card);
        }
    }

    // =========================================================================
    // CARD BUILDER
    // =========================================================================

    private VisualElement BuildProfileCard(Dictionary<string, string> profile)
    {
        // Extract profile data
        string profileId = profile.GetValueOrDefault("id", "0");
        string profileName = profile.GetValueOrDefault("profile_name", "Unknown Detective");
        string lastPlayed = profile.GetValueOrDefault("last_played", "Never");
        string createdAt = profile.GetValueOrDefault("created_at", "Unknown");

        // Format the timestamp to be more readable
        string formattedDate = FormatTimestamp(lastPlayed);

        // Get the active case name for this profile
        string caseName = GetCaseNameForProfile(profileId);

        // ── Card container ────────────────────────────────────────────────────
        var card = new VisualElement();
        card.AddToClassList("profile-card");

        // ── Left section — detective icon placeholder + name ──────────────────
        var leftSection = new VisualElement();
        leftSection.AddToClassList("card-left");

        var iconLabel = new Label { text = "🔍" };
        iconLabel.AddToClassList("card-icon");

        var nameLabel = new Label { text = profileName };
        nameLabel.AddToClassList("card-name");

        leftSection.Add(iconLabel);
        leftSection.Add(nameLabel);

        // ── Right section — case and timestamp ────────────────────────────────
        var rightSection = new VisualElement();
        rightSection.AddToClassList("card-right");

        var caseLabel = new Label { text = caseName };
        caseLabel.AddToClassList("card-case");

        var dateLabel = new Label { text = $"Last played: {formattedDate}" };
        dateLabel.AddToClassList("card-date");

        rightSection.Add(caseLabel);
        rightSection.Add(dateLabel);

        // ── Delete button ─────────────────────────────────────────────────────
        var deleteButton = new Button { text = "✕" };
        deleteButton.AddToClassList("card-delete");

        // ── Assemble card ─────────────────────────────────────────────────────
        card.Add(leftSection);
        card.Add(rightSection);
        card.Add(deleteButton);

        // ── Click to load — whole card is clickable ───────────────────────────
        int id = int.TryParse(profileId, out int parsed) ? parsed : -1;

        card.RegisterCallback<ClickEvent>(e =>
        {
            if (id < 0) return;

            // Don't load if the click originated from the delete button
            VisualElement target = e.target as VisualElement;
            while (target != null)
            {
                if (target == deleteButton) return;
                if (target == card) break; // Stop looking once we hit the card itself
                target = target.parent;
            }

            Debug.Log($"[LoadGameManager] Loading profile {id}: {profileName}");
            GameManager.Instance.LoadProfile(id);
        });

        // ── Delete button handler ─────────────────────────────────────────────
        deleteButton.clicked += () =>
        {
            GameManager.Instance.DeleteProfile(id);
            PopulateProfiles();  // Refresh the list
            Debug.Log($"[LoadGameManager] Deleted profile {id}.");
        };

        return card;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    /// <summary>
    /// Looks up the case name for a profile from case_progress in saves.db.
    /// Falls back to "No case started" if none found.
    /// </summary>
    private string GetCaseNameForProfile(string profileId)
    {
        var result = DatabaseManager.Instance.RunSaveQueryWithResults(
            "SELECT case_id FROM case_progress WHERE profile_id = ? ORDER BY id DESC LIMIT 1",
            profileId
        );

        if (result == null || result.Count == 0)
            return "No case started";

        string caseId = result[0]["case_id"];
        var caseDef = CaseManager.Instance.AllCases.Find(c => c.CaseId == caseId);
        return caseDef != null ? caseDef.Title : caseId;
    }

    /// <summary>
    /// Converts a yyyy-MM-dd HH:mm:ss timestamp into a readable format.
    /// </summary>
    private string FormatTimestamp(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw == "Never") return "Never";

        if (System.DateTime.TryParse(raw, out System.DateTime dt))
            return dt.ToString("dd MMM yyyy  HH:mm");

        return raw;
    }

    // =========================================================================
    // NAVIGATION
    // =========================================================================

    private void OnBackClicked(ClickEvent e)
    {
        GameManager.Instance.ReturnToMainMenu();
    }
}