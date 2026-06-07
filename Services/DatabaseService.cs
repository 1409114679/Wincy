using System.Data;
using System.Data.SQLite;
using System.IO;
using Wincy.Models;

namespace Wincy.Services;

/// <summary>
/// SQLite-backed storage for clipboard history.
/// </summary>
public class DatabaseService
{
    private static int _maxItems = 200;
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbDir = Path.Combine(appData, "Wincy");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "clipboard.db");
        _connectionString = $"Data Source={dbPath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ClipboardItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Text TEXT,
                ImageData BLOB,
                ContentType TEXT,
                IsPinned INTEGER DEFAULT 0,
                CopiedAt TEXT NOT NULL,
                SourceApplication TEXT,
                SourceAppPath TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_copied_at ON ClipboardItems(CopiedAt DESC);
            CREATE INDEX IF NOT EXISTS idx_pinned ON ClipboardItems(IsPinned);
        """;
        cmd.ExecuteNonQuery();

        // Migrate: add missing columns for older databases
        MigrateColumns(connection);
    }

    /// <summary>
    /// Add any missing columns to support schema upgrades from older versions.
    /// </summary>
    private static void MigrateColumns(SQLiteConnection connection)
    {
        // Get existing column names
        var columns = new HashSet<string>();
        var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info('ClipboardItems')";
        using var reader = pragmaCmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1)); // column name is at index 1

        // Add SourceAppPath if missing
        if (!columns.Contains("SourceAppPath"))
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE ClipboardItems ADD COLUMN SourceAppPath TEXT";
            try { alterCmd.ExecuteNonQuery(); } catch { }
        }
    }

    public void AddItem(ClipboardItem item)
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();

        // Deduplicate: if the same text is already the latest item, update its timestamp instead
        if (item.Text != null)
        {
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT Id, Text FROM ClipboardItems ORDER BY CopiedAt DESC LIMIT 1";
            using var reader = checkCmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(1) && reader.GetString(1) == item.Text)
            {
                var existingId = reader.GetInt64(0);
                reader.Close();
                // Update timestamp of existing entry
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = "UPDATE ClipboardItems SET CopiedAt = @copiedAt WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@copiedAt", item.CopiedAt.ToString("o"));
                updateCmd.Parameters.AddWithValue("@id", existingId);
                updateCmd.ExecuteNonQuery();
                return;
            }
        }

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ClipboardItems (Text, ImageData, ContentType, IsPinned, CopiedAt, SourceApplication, SourceAppPath)
            VALUES (@text, @image, @contentType, 0, @copiedAt, @sourceApp, @sourceAppPath)
        """;
        cmd.Parameters.AddWithValue("@text", (object?)item.Text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@image", (object?)item.ImageData ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@contentType", (object?)item.ContentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@copiedAt", item.CopiedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@sourceApp", (object?)item.SourceApplication ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sourceAppPath", (object?)item.SourceAppPath ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        // Keep max items in history
        var pruneCmd = connection.CreateCommand();
        pruneCmd.CommandText = $"""
            DELETE FROM ClipboardItems WHERE Id NOT IN (
                SELECT Id FROM ClipboardItems ORDER BY IsPinned DESC, CopiedAt DESC LIMIT {_maxItems}
            )
        """;
        pruneCmd.ExecuteNonQuery();
    }

    public List<ClipboardItem> GetHistory(string? search = null, int limit = 50)
    {
        var items = new List<ClipboardItem>();
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        if (!string.IsNullOrEmpty(search))
        {
            cmd.CommandText = """
                SELECT * FROM ClipboardItems
                WHERE Text LIKE @search
                ORDER BY IsPinned DESC, CopiedAt DESC
                LIMIT @limit
            """;
            cmd.Parameters.AddWithValue("@search", $"%{search}%");
        }
        else
        {
            cmd.CommandText = """
                SELECT * FROM ClipboardItems
                ORDER BY IsPinned DESC, CopiedAt DESC
                LIMIT @limit
            """;
        }
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ClipboardItem
            {
                Id = reader.GetInt64(0),
                Text = reader.IsDBNull(1) ? null : reader.GetString(1),
                ImageData = reader.IsDBNull(2) ? null : (byte[])reader["ImageData"],
                ContentType = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsPinned = reader.GetInt32(4) == 1,
                CopiedAt = DateTime.Parse(reader.GetString(5)),
                SourceApplication = reader.IsDBNull(6) ? null : reader.GetString(6),
                SourceAppPath = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return items;
    }

    public void TogglePin(long id)
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE ClipboardItems SET IsPinned = CASE WHEN IsPinned = 1 THEN 0 ELSE 1 END WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteItem(long id)
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardItems WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void TouchItem(long id)
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE ClipboardItems SET CopiedAt = @copiedAt WHERE Id = @id";
        cmd.Parameters.AddWithValue("@copiedAt", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public static int GetMaxItems() => _maxItems;

    public static void SetMaxItems(int max)
    {
        if (max > 0) _maxItems = max;
        // Persist to config
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wincy");
            Directory.CreateDirectory(dir);
            var configPath = Path.Combine(dir, "config.json");
            var json = System.Text.Json.JsonSerializer.Serialize(new { MaxItems = _maxItems });
            System.IO.File.WriteAllText(configPath, json);
        }
        catch { }
    }

    static DatabaseService()
    {
        // Load max items from config
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wincy");
            var configPath = Path.Combine(dir, "config.json");
            if (System.IO.File.Exists(configPath))
            {
                var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("MaxItems", out var maxProp) && maxProp.TryGetInt32(out int m) && m > 0)
                    _maxItems = m;
            }
        }
        catch { }
    }

    public void ClearAll(bool keepPinned = true)
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        if (keepPinned)
            cmd.CommandText = "DELETE FROM ClipboardItems WHERE IsPinned = 0";
        else
            cmd.CommandText = "DELETE FROM ClipboardItems";
        cmd.ExecuteNonQuery();
    }
}