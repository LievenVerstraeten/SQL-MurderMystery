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
        string profileId = profile.GetValueOrDefault("id", "");
        string profileName = profile.GetValueOrDefault("profile_name", "Unknown Detective");
        string lastPlayed = profile.GetValueOrDefault("last_played", "Never");

        // FIX: Validate the id parse here. If it fails we still build the card
        // but the delete button will show a warning and do nothing, rather than
        // silently calling DeleteProfile(-1) which deletes nothing and leaves the
        // card stranded in the list forever.
        bool idValid = int.TryParse(profileId, out int id) && id >= 0;
        if (!idValid)
            Debug.LogWarning($"[LoadGameManager] Profile has invalid id '{profileId}' — delete will be disabled.");

        string formattedDate = FormatTimestamp(lastPlayed);
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
        card.RegisterCallback<ClickEvent>(e =>
        {
            if (!idValid) return;

            // Don't load if the click originated from the delete button
            VisualElement target = e.target as VisualElement;
            while (target != null)
            {
                if (target == deleteButton) return;
                if (target == card) break;
                target = target.parent;
            }

            Debug.Log($"[LoadGameManager] Loading profile {id}: {profileName}");
            GameManager.Instance.LoadProfile(id);
        });

        // ── Delete button handler ─────────────────────────────────────────────
        deleteButton.clicked += () =>
        {
            // FIX: Guard against invalid ids — never call DeleteProfile(-1).
            if (!idValid)
            {
                Debug.LogWarning($"[LoadGameManager] Cannot delete profile with invalid id '{profileId}'. Removing card from UI only.");
                // Remove the orphaned card from the UI so the player isn't stuck
                card.RemoveFromHierarchy();
                RefreshEmptyState();
                return;
            }

            Debug.Log($"[LoadGameManager] Deleting profile {id}: {profileName}");

            // FIX: Remove the card from the UI immediately before the DB call.
            // This prevents the list from briefly showing a stale state when
            // PopulateProfiles re-queries, which was the original symptom of
            // "can't delete a profile that was just created then immediately exited".
            card.RemoveFromHierarchy();
            RefreshEmptyState();

            GameManager.Instance.DeleteProfile(id);
        };

        return card;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    /// <summary>
    /// Checks whether the profile list is now empty and updates the empty-state
    /// label visibility accordingly. Call after removing any card from the hierarchy.
    /// </summary>
    private void RefreshEmptyState()
    {
        if (profileList == null) return;

        bool isEmpty = profileList.childCount == 0;

        if (noProfilesLabel != null)
            noProfilesLabel.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        if (profileScroll != null)
            profileScroll.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>
    /// Looks up the case name for a profile from case_progress in saves.db.
    /// Falls back to "No case started" if none found.
    /// </summary>
    private string GetCaseNameForProfile(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) return "No case started";

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