using System.IO;
using Microsoft.Data.Sqlite;
using ClientChecklistManager.Models;

namespace ClientChecklistManager.Data;

public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public DatabaseService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClientChecklistManager");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "clients.db");
        _connectionString = $"Data Source={_dbPath}";
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
                Name TEXT NOT NULL DEFAULT '',
                FirstName TEXT NOT NULL DEFAULT '',
                LastName TEXT NOT NULL DEFAULT '',
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
                TaxYear INTEGER NOT NULL DEFAULT 2025,
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
                TaxYear INTEGER NOT NULL DEFAULT 2025,
                FOREIGN KEY (ClientRowId) REFERENCES Clients(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ChecklistTemplates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS ChecklistTemplateItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TemplateId INTEGER NOT NULL,
                Description TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (TemplateId) REFERENCES ChecklistTemplates(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ChecklistItems_ClientRowId ON ChecklistItems(ClientRowId);
            CREATE INDEX IF NOT EXISTS IX_ChecklistItems_ClientRowId_TaxYear ON ChecklistItems(ClientRowId, TaxYear);
            CREATE INDEX IF NOT EXISTS IX_EmailLogs_ClientRowId ON EmailLogs(ClientRowId);
            CREATE INDEX IF NOT EXISTS IX_EmailLogs_ClientRowId_TaxYear ON EmailLogs(ClientRowId, TaxYear);
            CREATE INDEX IF NOT EXISTS IX_Clients_ClientId ON Clients(ClientId);
            CREATE INDEX IF NOT EXISTS IX_ChecklistTemplateItems_TemplateId ON ChecklistTemplateItems(TemplateId);
            """;
        cmd.ExecuteNonQuery();

        // Migration for existing databases
        RunMigrations(conn);
    }

    private void RunMigrations(SqliteConnection conn)
    {
        using var versionCmd = conn.CreateCommand();
        versionCmd.CommandText = "PRAGMA user_version;";
        var schemaVersion = Convert.ToInt32(versionCmd.ExecuteScalar());

        if (schemaVersion < 1)
        {
            // Back up the database before migrating
            if (File.Exists(_dbPath))
            {
                var backupPath = _dbPath + ".pre-v1-backup";
                if (!File.Exists(backupPath))
                {
                    // Close and reopen to ensure WAL is checkpointed before copy
                    using var walCmd = conn.CreateCommand();
                    walCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    walCmd.ExecuteNonQuery();
                    File.Copy(_dbPath, backupPath);
                }
            }

            // Check if TaxYear column already exists (new installs have it from CREATE TABLE)
            bool hasTaxYearOnChecklist = ColumnExists(conn, "ChecklistItems", "TaxYear");
            bool hasTaxYearOnEmailLogs = ColumnExists(conn, "EmailLogs", "TaxYear");

            using var migrateCmd = conn.CreateCommand();
            var sql = "";
            if (!hasTaxYearOnChecklist)
                sql += "ALTER TABLE ChecklistItems ADD COLUMN TaxYear INTEGER NOT NULL DEFAULT 2025;\n";
            if (!hasTaxYearOnEmailLogs)
                sql += "ALTER TABLE EmailLogs ADD COLUMN TaxYear INTEGER NOT NULL DEFAULT 2025;\n";
            sql += "PRAGMA user_version = 1;\n";

            migrateCmd.CommandText = sql;
            migrateCmd.ExecuteNonQuery();

            schemaVersion = 1;
        }

        if (schemaVersion < 2)
        {
            // Back up the database before migrating
            if (File.Exists(_dbPath))
            {
                var backupPath = _dbPath + ".pre-v2-backup";
                if (!File.Exists(backupPath))
                {
                    using var walCmd = conn.CreateCommand();
                    walCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    walCmd.ExecuteNonQuery();
                    File.Copy(_dbPath, backupPath);
                }
            }

            // Add FirstName and LastName columns if they don't exist
            bool hasFirstName = ColumnExists(conn, "Clients", "FirstName");
            bool hasLastName = ColumnExists(conn, "Clients", "LastName");

            if (!hasFirstName)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Clients ADD COLUMN FirstName TEXT NOT NULL DEFAULT '';";
                alterCmd.ExecuteNonQuery();
            }
            if (!hasLastName)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Clients ADD COLUMN LastName TEXT NOT NULL DEFAULT '';";
                alterCmd.ExecuteNonQuery();
            }

            // Split existing Name data into FirstName and LastName
            using var splitCmd = conn.CreateCommand();
            splitCmd.CommandText = """
                UPDATE Clients SET
                    FirstName = CASE
                        WHEN INSTR(Name, ' ') > 0 THEN SUBSTR(Name, 1, INSTR(Name, ' ') - 1)
                        ELSE Name
                    END,
                    LastName = CASE
                        WHEN INSTR(Name, ' ') > 0 THEN SUBSTR(Name, INSTR(Name, ' ') + 1)
                        ELSE ''
                    END
                WHERE FirstName = '' AND LastName = '' AND Name != '';
                """;
            splitCmd.ExecuteNonQuery();

            // Migrate email settings
            MigrateEmailSettings();

            using var setVersionCmd = conn.CreateCommand();
            setVersionCmd.CommandText = "PRAGMA user_version = 2;";
            setVersionCmd.ExecuteNonQuery();
        }
    }

    private void MigrateEmailSettings()
    {
        // Construct EmailBodyTemplate from old header/footer if body template doesn't exist yet
        var existingBody = GetSetting("EmailBodyTemplate", "");
        if (string.IsNullOrEmpty(existingBody))
        {
            var header = GetSetting("EmailHeader", "");
            var footer = GetSetting("EmailFooter", "");

            if (!string.IsNullOrEmpty(header) || !string.IsNullOrEmpty(footer))
            {
                var body = "Dear {FirstName},\r\n\r\n";
                if (!string.IsNullOrEmpty(header))
                    body += header + "\r\n\r\n";
                body += "Client ID: {ClientId}\r\nTax Year: {TaxYear}\r\n\r\n";
                body += "{OutstandingItems}\r\n\r\n";
                if (!string.IsNullOrEmpty(footer))
                    body += footer + "\r\n\r\n";
                body += "Thank you,\r\n{FirmName}";

                SetSetting("EmailBodyTemplate", body);
            }
        }

        // Migrate subject template: replace {ClientName} with {FirstName}
        var subject = GetSetting("EmailSubjectTemplate", "");
        if (subject.Contains("{ClientName}"))
        {
            subject = subject.Replace("{ClientName}", "{FirstName}");
            SetSetting("EmailSubjectTemplate", subject);
        }
    }

    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ── Client CRUD ──

    public List<Client> GetAllClients()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ClientId, FirstName, LastName, Email, LastEmailed, CreatedAt FROM Clients ORDER BY LastName, FirstName";
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
            SELECT Id, ClientId, FirstName, LastName, Email, LastEmailed, CreatedAt FROM Clients
            WHERE ClientId LIKE @q OR FirstName LIKE @q OR LastName LIKE @q OR Email LIKE @q
                OR (FirstName || ' ' || LastName) LIKE @q
            ORDER BY LastName, FirstName
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
        cmd.CommandText = "SELECT Id, ClientId, FirstName, LastName, Email, LastEmailed, CreatedAt FROM Clients WHERE Id = @id";
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
            INSERT INTO Clients (ClientId, Name, FirstName, LastName, Email, CreatedAt)
            VALUES (@cid, @name, @first, @last, @email, @created);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@cid", client.ClientId);
        cmd.Parameters.AddWithValue("@name", client.FullName);
        cmd.Parameters.AddWithValue("@first", client.FirstName);
        cmd.Parameters.AddWithValue("@last", client.LastName);
        cmd.Parameters.AddWithValue("@email", client.Email);
        cmd.Parameters.AddWithValue("@created", client.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateClient(Client client)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Clients SET ClientId = @cid, Name = @name, FirstName = @first, LastName = @last,
                Email = @email, LastEmailed = @last_emailed
            WHERE Id = @id
            """;
        cmd.Parameters.AddWithValue("@id", client.Id);
        cmd.Parameters.AddWithValue("@cid", client.ClientId);
        cmd.Parameters.AddWithValue("@name", client.FullName);
        cmd.Parameters.AddWithValue("@first", client.FirstName);
        cmd.Parameters.AddWithValue("@last", client.LastName);
        cmd.Parameters.AddWithValue("@email", client.Email);
        cmd.Parameters.AddWithValue("@last_emailed", client.LastEmailed.HasValue
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

    public List<ChecklistItem> GetChecklistItems(int clientRowId, int taxYear)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ClientRowId, Description, IsReceived, ReceivedDate, Notes, SortOrder, CreatedAt, TaxYear
            FROM ChecklistItems WHERE ClientRowId = @crid AND TaxYear = @ty ORDER BY SortOrder, Id
            """;
        cmd.Parameters.AddWithValue("@crid", clientRowId);
        cmd.Parameters.AddWithValue("@ty", taxYear);
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
            INSERT INTO ChecklistItems (ClientRowId, Description, IsReceived, ReceivedDate, Notes, SortOrder, TaxYear, CreatedAt)
            VALUES (@crid, @desc, @recv, @rdate, @notes, @sort, @ty, @created);
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
        cmd.Parameters.AddWithValue("@ty", item.TaxYear);
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

    public void DeleteChecklistItemsByClientAndYear(int clientRowId, int taxYear)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ChecklistItems WHERE ClientRowId = @crid AND TaxYear = @ty";
        cmd.Parameters.AddWithValue("@crid", clientRowId);
        cmd.Parameters.AddWithValue("@ty", taxYear);
        cmd.ExecuteNonQuery();
    }

    public List<int> GetClientTaxYears(int clientRowId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT TaxYear FROM ChecklistItems WHERE ClientRowId = @crid ORDER BY TaxYear DESC";
        cmd.Parameters.AddWithValue("@crid", clientRowId);
        using var reader = cmd.ExecuteReader();
        var list = new List<int>();
        while (reader.Read())
        {
            list.Add(reader.GetInt32(0));
        }
        return list;
    }

    // ── Email Logs ──

    public void AddEmailLog(EmailLog log)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO EmailLogs (ClientRowId, SentAt, Subject, Body, OutstandingItemCount, TaxYear)
            VALUES (@crid, @sent, @subj, @body, @count, @ty)
            """;
        cmd.Parameters.AddWithValue("@crid", log.ClientRowId);
        cmd.Parameters.AddWithValue("@sent", log.SentAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@subj", log.Subject);
        cmd.Parameters.AddWithValue("@body", log.Body);
        cmd.Parameters.AddWithValue("@count", log.OutstandingItemCount);
        cmd.Parameters.AddWithValue("@ty", log.TaxYear);
        cmd.ExecuteNonQuery();
    }

    public List<EmailLog> GetEmailLogs(int clientRowId, int taxYear)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ClientRowId, SentAt, Subject, Body, OutstandingItemCount, TaxYear
            FROM EmailLogs WHERE ClientRowId = @crid AND TaxYear = @ty ORDER BY SentAt DESC
            """;
        cmd.Parameters.AddWithValue("@crid", clientRowId);
        cmd.Parameters.AddWithValue("@ty", taxYear);
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
                OutstandingItemCount = reader.GetInt32(5),
                TaxYear = reader.GetInt32(6)
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

    // ── Templates ──

    public List<ChecklistTemplate> GetAllTemplates()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAt FROM ChecklistTemplates ORDER BY Name";
        using var reader = cmd.ExecuteReader();
        var list = new List<ChecklistTemplate>();
        while (reader.Read())
        {
            list.Add(new ChecklistTemplate
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2))
            });
        }
        return list;
    }

    public int AddTemplate(string name)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ChecklistTemplates (Name) VALUES (@name);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void DeleteTemplate(int templateId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ChecklistTemplates WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.ExecuteNonQuery();
    }

    public bool TemplateNameExists(string name)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ChecklistTemplates WHERE Name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public List<ChecklistTemplateItem> GetTemplateItems(int templateId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, TemplateId, Description, SortOrder
            FROM ChecklistTemplateItems WHERE TemplateId = @tid ORDER BY SortOrder, Id
            """;
        cmd.Parameters.AddWithValue("@tid", templateId);
        using var reader = cmd.ExecuteReader();
        var list = new List<ChecklistTemplateItem>();
        while (reader.Read())
        {
            list.Add(new ChecklistTemplateItem
            {
                Id = reader.GetInt32(0),
                TemplateId = reader.GetInt32(1),
                Description = reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return list;
    }

    public void SaveTemplateItems(int templateId, List<string> descriptions)
    {
        var conn = GetConnection();

        // Delete existing items
        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM ChecklistTemplateItems WHERE TemplateId = @tid";
        delCmd.Parameters.AddWithValue("@tid", templateId);
        delCmd.ExecuteNonQuery();

        // Insert new items
        for (int i = 0; i < descriptions.Count; i++)
        {
            using var insCmd = conn.CreateCommand();
            insCmd.CommandText = """
                INSERT INTO ChecklistTemplateItems (TemplateId, Description, SortOrder)
                VALUES (@tid, @desc, @sort)
                """;
            insCmd.Parameters.AddWithValue("@tid", templateId);
            insCmd.Parameters.AddWithValue("@desc", descriptions[i]);
            insCmd.Parameters.AddWithValue("@sort", i);
            insCmd.ExecuteNonQuery();
        }
    }

    // ── App Settings ──

    public string GetSetting(string key, string defaultValue = "")
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result is string s ? s : defaultValue;
    }

    public void SetSetting(string key, string value)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppSettings (Key, Value) VALUES (@key, @val)
            ON CONFLICT(Key) DO UPDATE SET Value = @val
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", value);
        cmd.ExecuteNonQuery();
    }

    public AppSettings LoadSettings()
    {
        return new AppSettings
        {
            OutlookFromAccount = GetSetting("OutlookFromAccount"),
            EmailSubjectTemplate = GetSetting("EmailSubjectTemplate", AppSettings.DefaultSubjectTemplate),
            EmailBodyTemplate = GetSetting("EmailBodyTemplate", AppSettings.DefaultBodyTemplate),
            EmailFontFamily = GetSetting("EmailFontFamily", "Aptos"),
            EmailFontSize = int.TryParse(GetSetting("EmailFontSize", "11"), out var size) ? size : 11,
            DefaultBccAddress = GetSetting("DefaultBccAddress"),
            FirmName = GetSetting("FirmName"),
            FollowUpReminderDays = int.TryParse(GetSetting("FollowUpReminderDays", "14"), out var days) ? days : 14
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        SetSetting("OutlookFromAccount", settings.OutlookFromAccount);
        SetSetting("EmailSubjectTemplate", settings.EmailSubjectTemplate);
        SetSetting("EmailBodyTemplate", settings.EmailBodyTemplate);
        SetSetting("EmailFontFamily", settings.EmailFontFamily);
        SetSetting("EmailFontSize", settings.EmailFontSize.ToString());
        SetSetting("DefaultBccAddress", settings.DefaultBccAddress);
        SetSetting("FirmName", settings.FirmName);
        SetSetting("FollowUpReminderDays", settings.FollowUpReminderDays.ToString());
    }

    // ── Helpers ──

    private static Client ReadClient(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ClientId = reader.GetString(1),
        FirstName = reader.GetString(2),
        LastName = reader.GetString(3),
        Email = reader.GetString(4),
        LastEmailed = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
        CreatedAt = DateTime.Parse(reader.GetString(6))
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
        CreatedAt = DateTime.Parse(reader.GetString(7)),
        TaxYear = reader.GetInt32(8)
    };

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
}
