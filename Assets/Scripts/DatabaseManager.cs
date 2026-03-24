// SQLite-net library documentation https://github.com/praeclarum/sqlite-net/wiki
// SQLite3 functions documentation https://www.sqlite.org/c3ref/funclist.html

using UnityEngine;
using SQLite;
public class DatabaseManager : MonoBehaviour
{
    private SQLiteConnection db;
    public static DatabaseManager Instance;

    // Initializes and establishes connection to db
    void Awake()
    {
        Instance = this;
        string path = Application.streamingAssetsPath + "/world.db";
        db = new SQLiteConnection(path);
        Debug.Log("Connected to DB at: " + path);
    }

    // For executing operations which don't return a value
    public void RunNonQuery(string sql)
    {
        db.Execute(sql);
    }

    // For executing operations which return a value
    public void RunQuery(string sql)
    {
        var stmt = SQLite3.Prepare2(db.Handle, sql);
        int cols = SQLite3.ColumnCount(stmt);

        while (SQLite3.Step(stmt) == SQLite3.Result.Row)
        {
            string line = "";
            for (int i = 0; i < cols; i++)
            {
                string colName = SQLite3.ColumnName16(stmt, i);
                string colVal = SQLite3.ColumnString(stmt, i);
                line += colName + ": " + colVal + " | ";
            }
            Debug.Log(line);
        }

        SQLite3.Finalize(stmt);
    }

    // Closes connection when object is destroyed
    void OnDestroy()
    {
        db?.Close();
    }
}