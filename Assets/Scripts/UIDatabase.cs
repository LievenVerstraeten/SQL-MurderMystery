using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIDatabase : MonoBehaviour
{
    [SerializeField]
    private UIDocument uiDocument;

    private TextField queryInputField;
    private ListView commandHistoryList;
    private Button sendQueryButton;
    private Label queryOutputText;

    private List<string> history = new List<string>();

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        
        }

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("UIDatabase: UIDocument is missing!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        queryInputField = root.Q<TextField>("query-input-field");
        commandHistoryList = root.Q<ListView>("command-history-list");
        sendQueryButton = root.Q<Button>("send-query-button");
        queryOutputText = root.Q<Label>("query-output-text");

        if (sendQueryButton != null)
        {
            sendQueryButton.clicked += OnSendQuery;
        }

        if (commandHistoryList != null)
        {
            // Setup ListView logic
            commandHistoryList.itemsSource = history;
            
            commandHistoryList.makeItem = () => new Label();
            commandHistoryList.bindItem = (VisualElement e, int i) =>
            {
                var label = (Label)e;
                label.text = history[i];
                // basic styling for history items
                label.style.color = new StyleColor(Color.white);
                label.style.fontSize = 14;
                label.style.paddingLeft = 5;
                label.style.paddingTop = 5;
                label.style.paddingBottom = 5;
            };

            // Hook up selection change to put old command back in input field
            commandHistoryList.selectionChanged += OnHistoryItemSelected;
        }
    }

    private void OnDisable()
    {
        if (sendQueryButton != null)
        {
            sendQueryButton.clicked -= OnSendQuery;
        }
        if (commandHistoryList != null)
        {
            commandHistoryList.selectionChanged -= OnHistoryItemSelected;
        }
    }

    private void OnSendQuery()
    {
        Debug.Log("Send Button Clicked!");
        
        if (queryInputField == null) 
        {
            Debug.LogError("queryInputField is null.");
            return;
        }
        if (DatabaseManager.Instance == null) 
        {
            Debug.LogError("DatabaseManager.Instance is null. Is DatabaseManager in the scene?");
            return;
        }

        string query = queryInputField.value;
        if (query != null) query = query.Trim();
        
        if (string.IsNullOrEmpty(query)) 
        {
            Debug.LogWarning("Query is empty, doing nothing.");
            return;
        }

        // Remember query
        if (!history.Contains(query))
        {
            history.Add(query);
            // Refresh list
            if (commandHistoryList != null)
            {
                commandHistoryList.RefreshItems();
                commandHistoryList.ScrollToItem(-1); // Scroll to bottom
            }
        }

        string errorMessage;
        var results = DatabaseManager.Instance.RunQueryWithResults(query, out errorMessage);

        if (queryOutputText != null)
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                queryOutputText.style.color = new StyleColor(Color.red);
                queryOutputText.text = "SQL Error:\n" + errorMessage;
            }
            else
            {
                queryOutputText.style.color = new StyleColor(Color.white);
                
                if (results.Count == 0)
                {
                    queryOutputText.text = "Query executed successfully. (0 rows affected / returned)";
                }
                else
                {
                    // Format response into a simple text table
                    string display = "";
                    foreach (var row in results)
                    {
                        foreach (var kvp in row)
                        {
                            display += $"{kvp.Key}: {kvp.Value} | ";
                        }
                        display = display.TrimEnd(' ', '|') + "\n";
                    }
                    queryOutputText.text = display;
                }
            }
        }
    }

    private void OnHistoryItemSelected(IEnumerable<object> selection)
    {
        foreach (var sel in selection)
        {
            string selectedQuery = sel as string;
            if (!string.IsNullOrEmpty(selectedQuery) && queryInputField != null)
            {
                queryInputField.value = selectedQuery;
            }
            break; // only handle first selection
        }
    }
}
