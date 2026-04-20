// SQLite-net library documentation https://github.com/praeclarum/sqlite-net/wiki
// SQLite3 functions documentation https://www.sqlite.org/c3ref/funclist.html

using UnityEngine;
using SQLite;
using System.Collections.Generic;
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
    public void RunNonQuery(string sql, params object[] args)
    {
        db.Execute(sql, args);
    }

    // For executing operations which return a value
    public void RunQuery(string sql)
    {
        // Compiles SQL into SQLite format.
        var stmt = SQLite3.Prepare2(db.Handle, sql);
        // Counts the amount of columns stmt gives
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
        // Clears in memory SQL statements
        SQLite3.Finalize(stmt);
    }
    public List<Dictionary<string, string>> RunQueryWithResults(string sql)
    {
        return RunQueryWithResults(sql, out _);
    }

    public List<Dictionary<string, string>> RunQueryWithResults(string sql, out string errorMessage)
    {
        errorMessage = null;
        var results = new List<Dictionary<string, string>>();
        
        try
        {
            // Compiles SQL into SQLite format.
            var stmt = SQLite3.Prepare2(db.Handle, sql);
            try
            {
                // Counts the amount of columns stmt gives
                int cols = SQLite3.ColumnCount(stmt);
                while (SQLite3.Step(stmt) == SQLite3.Result.Row)
                {
                    var row = new Dictionary<string, string>();
                    for (int i = 0; i < cols; i++)
                    {
                        // Get column name with UTF-16 encoding
                        string colName = SQLite3.ColumnName16(stmt, i);
                        // Reads value of column as a string
                        string colVal = SQLite3.ColumnString(stmt, i);
                        row[colName] = colVal;
                    }
                    results.Add(row);
                }
            }
            finally 
            {
                // Clears in memory SQL statements
                SQLite3.Finalize(stmt);
            }
        }
        catch (System.Exception ex)
        {
            errorMessage = ex.Message;
            Debug.LogError("Query failed: " + ex.Message);
        }
        return results;
    }
    // Closes connection when object is destroyed
    void OnDestroy()
    {
        db?.Close();
    }
}