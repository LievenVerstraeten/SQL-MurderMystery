using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NameUI : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public Button submitButton;
    public TMP_Text statusText;
    public TMP_Text namesText;

    void Start()
    {
        submitButton.onClick.AddListener(OnSubmit);
        DatabaseManager.Instance.RunNonQuery(
            "CREATE TABLE IF NOT EXISTS names (id INTEGER PRIMARY KEY, name TEXT)"
        );
        RefreshList();
    }

    void OnSubmit()
    {
        string playerName = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            statusText.text = "Please enter a name!";
            statusText.color = Color.red;
            return;
        }

        DatabaseManager.Instance.RunNonQuery(
            $"INSERT INTO names (name) VALUES ('{playerName}')"
        );

        statusText.text = "Name added successfully!";
        statusText.color = Color.green;
        nameInputField.text = "";
        RefreshList();
    }

    void RefreshList()
    {
        var names = DatabaseManager.Instance.RunQueryWithResults("SELECT * FROM names");
        string display = "";

        foreach (var row in names)
            display += row["id"] + ".  " + row["name"] + "\n";

        namesText.text = display;
        namesText.color = Color.black;
        namesText.fontSize = 25;
    }
}