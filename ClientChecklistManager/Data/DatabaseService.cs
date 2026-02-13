using System.IO;
using Microsoft.Data.Sqlite;
using ClientChecklistManager.Models;

namespace ClientChecklistManager.Data;

public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public DatabaseService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClientChecklistManager");
        Directory.CreateDirectory(appData);
        var dbPath = Path.Combine(appData, "clients.db");
        _connectionString = $"Data Source={dbPath}";
    }

    private SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }
        return _connection;
    }

    public void Initialize()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Clients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientId TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL DEFAULT '',
                LastEmailed TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS ChecklistItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientRowId INTEGER NOT NULL,
                Description TEXT NOT NULL,
                IsReceived INTEGER NOT NULL DEFAULT 0,
                ReceivedDate TEXT,
                Notes TEXT NOT NULL DEFAULT '',
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                FOREIGN KEY (ClientRowId) REFERENCES Clients(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS EmailLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientRowId INTEGER NOT NULL,
                SentAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                Subject TEXT NOT NULL DEFAULT '',
                Body TEXT NOT NULL DEFAULT '',
                OutstandingItemCount INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ClientRowId) REFERENCES Clients(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_ChecklistItems_ClientRowId ON ChecklistItems(ClientRowId);
            CREATE INDEX IF NOT EXISTS IX_EmailLogs_ClientRowId ON EmailLogs(ClientRowId);
            CREATE INDEX IF NOT EXISTS IX_Clients_ClientId ON Clients(ClientId);
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Client CRUD ──

    public List<Client> GetAllClients()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ClientId, Name, Email, LastEmailed, CreatedAt FROM Clients ORDER BY Name";
        using var reader = cmd.ExecuteReader();
        var list = new List<Client>();
        while (reader.Read())
        {
            list.Add(ReadClient(reader));
        }
        return list;
    }

    public List<Client> SearchClients(string query)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ClientId, Name, Email, LastEmailed, CreatedAt FROM Clients
            WHERE ClientId LIKE @q OR Name LIKE @q OR Email LIKE @q
            ORDER BY Name
            """;
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        using var reader = cmd.ExecuteReader();
        var list = new List<Client>();
        while (reader.Read())
        {
            list.Add(ReadClient(reader));
        }
        return list;
    }

    public Client? GetClientById(int id)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ClientId, Name, Email, LastEmailed, CreatedAt FROM Clients WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadClient(reader) : null;
    }

    public bool ClientIdExists(string clientId, int? excludeRowId = null)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = excludeRowId.HasValue
            ? "SELECT COUNT(*) FROM Clients WHERE ClientId = @cid AND Id != @eid"
            : "SELECT COUNT(*) FROM Clients WHERE ClientId = @cid";
        cmd.Parameters.AddWithValue("@cid", clientId);
        if (excludeRowId.HasValue)
            cmd.Parameters.AddWithValue("@eid", excludeRowId.Value);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public int AddClient(Client client)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Clients (ClientId, Name, Email, CreatedAt)
            VALUES (@cid, @name, @email, @created);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@cid", client.ClientId);
        cmd.Parameters.AddWithValue("@name", client.Name);
        cmd.Parameters.AddWithValue("@email", client.Email);
        cmd.Parameters.AddWithValue("@created", client.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateClient(Client client)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Clients SET ClientId = @cid, Name = @name, Email = @email, LastEmailed = @last
            WHERE Id = @id
            """;
        cmd.Parameters.AddWithValue("@id", client.Id);
        cmd.Parameters.AddWithValue("@cid", client.ClientId);
        cmd.Parameters.AddWithValue("@name", client.Name);
        cmd.Parameters.AddWithValue("@email", client.Email);
        cmd.Parameters.AddWithValue("@last", client.LastEmailed.HasValue
            ? client.LastEmailed.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void DeleteClient(int id)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Clients WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ── Checklist Items ──

    public List<ChecklistItem> GetChecklistItems(int clientRowId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ClientRowId, Description, IsReceived, ReceivedDate, Notes, SortOrder, CreatedAt
            FROM ChecklistItems WHERE ClientRowId = @crid ORDER BY SortOrder, Id
            """;
        cmd.Parameters.AddWithValue("@crid", clientRowId);
        using var reader = cmd.ExecuteReader();
        var list = new List<ChecklistItem>();
        while (reader.Read())
        {
            list.Add(ReadChecklistItem(reader));
        }
        return list;
    }

    public int AddChecklistItem(ChecklistItem item)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ChecklistItems (ClientRowId, Description, IsReceived, ReceivedDate, Notes, SortOrder, CreatedAt)
            VALUES (@crid, @desc, @recv, @rdate, @notes, @sort, @created);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@crid", item.ClientRowId);
        cmd.Parameters.AddWithValue("@desc", item.Description);
        cmd.Parameters.AddWithValue("@recv", item.IsReceived ? 1 : 0);
        cmd.Parameters.AddWithValue("@rdate", item.ReceivedDate.HasValue
            ? item.ReceivedDate.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@notes", item.Notes);
        cmd.Parameters.AddWithValue("@sort", item.SortOrder);
        cmd.Parameters.AddWithValue("@created", item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateChecklistItem(ChecklistItem item)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE ChecklistItems
            SET Description = @desc, IsReceived = @recv, ReceivedDate = @rdate, Notes = @notes, SortOrder = @sort
            WHERE Id = @id
            """;
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@desc", item.Description);
        cmd.Parameters.AddWithValue("@recv", item.IsReceived ? 1 : 0);
        cmd.Parameters.AddWithValue("@rdate", item.ReceivedDate.HasValue
            ? item.ReceivedDate.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@notes", item.Notes);
        cmd.Parameters.AddWithValue("@sort", item.SortOrder);
        cmd.ExecuteNonQuery();
    }

    public void DeleteChecklistItem(int id)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ChecklistItems WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ── Email Logs ──

    public void AddEmailLog(EmailLog log)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO EmailLogs (ClientRowId, SentAt, Subject, Body, OutstandingItemCount)
            VALUES (@crid, @sent, @subj, @body, @count)
            """;
        cmd.Parameters.AddWithValue("@crid", log.ClientRowId);
        cmd.Parameters.AddWithValue("@sent", log.SentAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@subj", log.Subject);
        cmd.Parameters.AddWithValue("@body", log.Body);
        cmd.Parameters.AddWithValue("@count", log.OutstandingItemCount);
        cmd.ExecuteNonQuery();
    }

    public List<EmailLog> GetEmailLogs(int clientRowId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ClientRowId, SentAt, Subject, Body, OutstandingItemCount
            FROM EmailLogs WHERE ClientRowId = @crid ORDER BY SentAt DESC
            """;
        cmd.Parameters.AddWithValue("@crid", clientRowId);
        using var reader = cmd.ExecuteReader();
        var list = new List<EmailLog>();
        while (reader.Read())
        {
            list.Add(new EmailLog
            {
                Id = reader.GetInt32(0),
                ClientRowId = reader.GetInt32(1),
                SentAt = DateTime.Parse(reader.GetString(2)),
                Subject = reader.GetString(3),
                Body = reader.GetString(4),
                OutstandingItemCount = reader.GetInt32(5)
            });
        }
        return list;
    }

    public void UpdateLastEmailed(int clientRowId, DateTime sentAt)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Clients SET LastEmailed = @last WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", clientRowId);
        cmd.Parameters.AddWithValue("@last", sentAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    // ── Helpers ──

    private static Client ReadClient(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ClientId = reader.GetString(1),
        Name = reader.GetString(2),
        Email = reader.GetString(3),
        LastEmailed = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
        CreatedAt = DateTime.Parse(reader.GetString(5))
    };

    private static ChecklistItem ReadChecklistItem(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ClientRowId = reader.GetInt32(1),
        Description = reader.GetString(2),
        IsReceived = reader.GetInt32(3) == 1,
        ReceivedDate = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
        Notes = reader.GetString(5),
        SortOrder = reader.GetInt32(6),
        CreatedAt = DateTime.Parse(reader.GetString(7))
    };

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
}
