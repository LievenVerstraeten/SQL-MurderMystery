using JetBrains.Annotations;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

public class GameUIManager : MonoBehaviour
{
    [SerializeField]
    private UIDocument uiDocument;

    // UI Elements
    private Button burgerMenuButton;
    private VisualElement burgerMenuDropdown;
    private VisualElement querieInputMenu;

    private Button tutorialButton;
    private Button profileButton;
    private Button cluesButton;
    private Button notesButton;
    private Button sqlMenuButton;
    private Button sendSqlCommandButton;

    private Button leftArrowButton;
    private Button rightArrowButton;

    private bool isMenuOpen = false;
    private bool isInputMenuOpen = false;

    private void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("GameUIManager: UIDocument is missing or root is null!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Querying elements
        burgerMenuButton = root.Q<Button>("burger-menu-button");
        burgerMenuDropdown = root.Q<VisualElement>("burger-menu-dropdown");
        querieInputMenu = root.Q<VisualElement>("querie-input-menu");

        // Initialize dropdown to highly reliable inline style
        if (burgerMenuDropdown != null)
        {
            burgerMenuDropdown.style.display = DisplayStyle.None;
        }
        if (querieInputMenu != null)
        {
            querieInputMenu.style.display = DisplayStyle.None;
        }



        tutorialButton = root.Q<Button>("tutorial-button");
        profileButton = root.Q<Button>("profile-button");
        cluesButton = root.Q<Button>("clues-button");
        notesButton = root.Q<Button>("notes-button");
        sqlMenuButton = root.Q<Button>("sql-menu-button");
        sendSqlCommandButton = root.Q<Button>("send-query-button");

        leftArrowButton = root.Q<Button>("left-arrow-button");
        rightArrowButton = root.Q<Button>("right-arrow-button");

        // Assigning events
        if (burgerMenuButton != null)
            burgerMenuButton.clicked += OnBurgerMenuClicked;

        if (tutorialButton != null) tutorialButton.clicked += () => Debug.Log("Tutorial clicked");
        if (profileButton != null) profileButton.clicked += () => Debug.Log("Profile clicked");
        if (cluesButton != null) cluesButton.clicked += () => Debug.Log("Clues clicked");
        if (notesButton != null) notesButton.clicked += () => Debug.Log("Notes clicked");
        if (sqlMenuButton != null) 
            sqlMenuButton.clicked += OnSqlQuerieMenuClicked;

        if (sendSqlCommandButton != null) sendSqlCommandButton.clicked += () => Debug.Log("command sent");

        if (leftArrowButton != null) leftArrowButton.clicked += () => Debug.Log("Left arrow clicked");
        if (rightArrowButton != null) rightArrowButton.clicked += () => Debug.Log("Right arrow clicked");
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        if (burgerMenuButton != null)
            burgerMenuButton.clicked -= OnBurgerMenuClicked;
        if (sqlMenuButton != null)
            sqlMenuButton.clicked -= OnSqlQuerieMenuClicked;
    }

    private void OnBurgerMenuClicked()
    {

        if (burgerMenuDropdown != null)
        {
            isMenuOpen = !isMenuOpen;
            
            if (isMenuOpen)
            {
                burgerMenuDropdown.style.display = DisplayStyle.Flex;
                burgerMenuDropdown.BringToFront(); // Forces element to render perfectly on top
            }
            else
            {
                burgerMenuDropdown.style.display = DisplayStyle.None;
            }
        }
    }

    private void OnSqlQuerieMenuClicked()
    {
        if (querieInputMenu != null)
        {
            isInputMenuOpen = !isInputMenuOpen;

            if (isInputMenuOpen)
            {
                querieInputMenu.style.display = DisplayStyle.Flex;
                querieInputMenu.BringToFront(); // Forces element to render perfectly on top
            }
            else
            {
                querieInputMenu.style.display = DisplayStyle.None;
            }
        }
    }
}
