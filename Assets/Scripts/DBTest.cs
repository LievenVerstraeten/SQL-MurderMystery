using UnityEngine;

public class DBTest : MonoBehaviour
{
    void Start()
    {
        DatabaseManager.Instance.RunNonQuery(
            "CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY, name TEXT)"
        );

        DatabaseManager.Instance.RunNonQuery(
            "INSERT INTO test (name) VALUES ('Hero')"
        );

        DatabaseManager.Instance.RunQuery("SELECT * FROM test");
    }
}