using Microsoft.Data.Sqlite;

namespace AuraPrintsApi.Data;

public class DatabaseContext
{
    private readonly string _dbFile;

    public DatabaseContext(string dbFile)
    {
        _dbFile = dbFile;
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_dbFile}");
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbFile)!);
        using var con = CreateConnection();
        con.Open();

        // Schritt 1: Alte Produkte-Tabellen droppen
        using var dropCmd = con.CreateCommand();
        dropCmd.CommandText = @"
            PRAGMA foreign_keys = OFF;
            DROP TABLE IF EXISTS products_generic;
            DROP TABLE IF EXISTS product_fields;
            DROP TABLE IF EXISTS product_types;
            PRAGMA foreign_keys = ON;";
        dropCmd.ExecuteNonQuery();

        // Schritt 2: Alle Tabellen erstellen (bestehende + neue)
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS weeks (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                number     INTEGER NOT NULL,
                title      TEXT NOT NULL,
                phase      TEXT NOT NULL,
                badge_pc   TEXT NOT NULL,
                badge_phys TEXT NOT NULL,
                note       TEXT,
                project_id INTEGER NOT NULL DEFAULT 1,
                UNIQUE(project_id, number)
            );
            CREATE TABLE IF NOT EXISTS tasks (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                week_number INTEGER NOT NULL,
                sort_order  INTEGER NOT NULL,
                type        TEXT NOT NULL,
                text        TEXT NOT NULL,
                hours       TEXT NOT NULL,
                project_id  INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS state (
                key   TEXT PRIMARY KEY,
                value INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS state_v2 (
                project_id INTEGER NOT NULL,
                key        TEXT NOT NULL,
                value      INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (project_id, key)
            );
            CREATE TABLE IF NOT EXISTS categories (
                id    INTEGER PRIMARY KEY AUTOINCREMENT,
                name  TEXT NOT NULL,
                color TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS expenses (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id INTEGER NOT NULL,
                amount      REAL NOT NULL,
                description TEXT NOT NULL,
                link        TEXT,
                date        TEXT NOT NULL,
                week_number INTEGER,
                task_id     INTEGER,
                FOREIGN KEY (category_id) REFERENCES categories(id)
            );
            CREATE TABLE IF NOT EXISTS expense_attachments (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                expense_id INTEGER NOT NULL,
                file_name  TEXT NOT NULL,
                mime_type  TEXT NOT NULL,
                data       TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (expense_id) REFERENCES expenses(id)
            );
            CREATE TABLE IF NOT EXISTS milestones (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                name        TEXT NOT NULL,
                description TEXT,
                created_at  TEXT NOT NULL,
                snapshot    TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings_v2 (
                project_id INTEGER NOT NULL,
                key        TEXT NOT NULL,
                value      TEXT NOT NULL,
                PRIMARY KEY (project_id, key)
            );
            CREATE TABLE IF NOT EXISTS product_categories (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                name        TEXT NOT NULL,
                description TEXT,
                color       TEXT NOT NULL DEFAULT '#4f8ef7'
            );
            CREATE TABLE IF NOT EXISTS product_attributes (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id INTEGER NOT NULL,
                name        TEXT NOT NULL,
                field_type  TEXT NOT NULL DEFAULT 'text',
                options     TEXT,
                required    INTEGER NOT NULL DEFAULT 0,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (category_id) REFERENCES product_categories(id)
            );
            CREATE TABLE IF NOT EXISTS products_v2 (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id      INTEGER NOT NULL,
                name             TEXT NOT NULL,
                description      TEXT,
                attribute_values TEXT NOT NULL DEFAULT '{}',
                created_at       TEXT NOT NULL,
                FOREIGN KEY (category_id) REFERENCES product_categories(id)
            );
            CREATE TABLE IF NOT EXISTS product_variations (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                product_id INTEGER NOT NULL,
                name       TEXT NOT NULL,
                sku        TEXT NOT NULL UNIQUE,
                price      REAL NOT NULL DEFAULT 0,
                stock      INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                FOREIGN KEY (product_id) REFERENCES products_v2(id)
            );
            CREATE TABLE IF NOT EXISTS production_queue (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                product_id   INTEGER NOT NULL,
                variation_id INTEGER,
                quantity     INTEGER NOT NULL DEFAULT 1,
                done         INTEGER NOT NULL DEFAULT 0,
                note         TEXT,
                added_at     TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS calendar_events (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                title       TEXT NOT NULL,
                date        TEXT NOT NULL,
                end_date    TEXT,
                time        TEXT,
                description TEXT,
                color       TEXT NOT NULL DEFAULT '#4f8ef7',
                type        TEXT NOT NULL DEFAULT 'event',
                created_at  TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS users (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                username          TEXT NOT NULL UNIQUE,
                password_hash     TEXT NOT NULL,
                is_admin          INTEGER NOT NULL DEFAULT 0,
                is_platform_admin INTEGER NOT NULL DEFAULT 0,
                created_at        TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS projects (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                name          TEXT NOT NULL,
                description   TEXT,
                start_date    TEXT,
                currency      TEXT NOT NULL DEFAULT 'CHF',
                project_image TEXT,
                visible_tabs  TEXT,
                created_at    TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS project_members (
                project_id INTEGER NOT NULL,
                user_id    INTEGER NOT NULL,
                role       TEXT NOT NULL DEFAULT 'member',
                PRIMARY KEY (project_id, user_id),
                FOREIGN KEY (project_id) REFERENCES projects(id),
                FOREIGN KEY (user_id)    REFERENCES users(id)
            );
            CREATE TABLE IF NOT EXISTS invites (
                token      TEXT PRIMARY KEY,
                type       TEXT NOT NULL DEFAULT 'project',
                project_id INTEGER,
                role       TEXT NOT NULL DEFAULT 'member',
                created_by INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                used_at    TEXT,
                FOREIGN KEY (project_id) REFERENCES projects(id)
            );
            CREATE TABLE IF NOT EXISTS agent_token_usage (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id       INTEGER NOT NULL,
                project_id    INTEGER NOT NULL,
                input_tokens  INTEGER NOT NULL DEFAULT 0,
                output_tokens INTEGER NOT NULL DEFAULT 0,
                recorded_at   TEXT NOT NULL,
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            CREATE TABLE IF NOT EXISTS agent_tier_config (
                tier         TEXT PRIMARY KEY,
                input_limit  INTEGER NOT NULL,
                output_limit INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS activity_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                project_id  INTEGER NOT NULL,
                entity_type TEXT NOT NULL,
                action      TEXT NOT NULL,
                title       TEXT NOT NULL,
                description TEXT,
                actor       TEXT,
                created_at  TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        // Schritt 3: ALTER TABLE — project_id zu bestehenden Tabellen hinzufügen (idempotent)
        var alterStatements = new[]
        {
            "ALTER TABLE weeks              ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE tasks              ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE state              ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE categories         ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE expenses           ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE milestones         ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE product_categories ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE production_queue   ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE calendar_events    ADD COLUMN project_id INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE users              ADD COLUMN is_platform_admin INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE expenses           ADD COLUMN type TEXT NOT NULL DEFAULT 'expense'",
            "ALTER TABLE users              ADD COLUMN agent_tier TEXT NOT NULL DEFAULT 'free'",
        };

        foreach (var sql in alterStatements)
        {
            try
            {
                using var alterCmd = con.CreateCommand();
                alterCmd.CommandText = sql;
                alterCmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column"))
            {
                // Spalte existiert bereits — ignorieren
            }
        }

        // Schritt 4: Migration — falls projects leer ist, Projekt 1 aus settings erstellen
        RunMigration(con);

        // Schritt 4b: Agent-Tier-Konfiguration seeden
        SeedAgentTierConfig(con);

        // Schritt 5: settings → settings_v2 migrieren
        MigrateSettingsToV2(con);

        // Schritt 6: weeks.number UNIQUE → UNIQUE(project_id, number) migrieren
        MigrateWeeksConstraint(con);
    }

    private static void SeedAgentTierConfig(SqliteConnection con)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO agent_tier_config (tier, input_limit, output_limit) VALUES ('free',  50000,    20000);
            INSERT OR IGNORE INTO agent_tier_config (tier, input_limit, output_limit) VALUES ('basic', 500000,   200000);
            INSERT OR IGNORE INTO agent_tier_config (tier, input_limit, output_limit) VALUES ('pro',   5000000,  2000000);";
        cmd.ExecuteNonQuery();
    }

    private static void RunMigration(SqliteConnection con)
    {
        // Prüfen ob projects leer ist
        using var countCmd = con.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM projects";
        var projectCount = (long)(countCmd.ExecuteScalar() ?? 0L);
        if (projectCount > 0) return;

        // settings lesen
        using var sCmd = con.CreateCommand();
        sCmd.CommandText = "SELECT key, value FROM settings";
        var settingsDict = new Dictionary<string, string>();
        using var sReader = sCmd.ExecuteReader();
        while (sReader.Read())
            settingsDict[sReader.GetString(0)] = sReader.GetString(1);

        var projectName = settingsDict.GetValueOrDefault("project_name", "Mein Projekt");
        var startDate = settingsDict.GetValueOrDefault("start_date", "");
        var description = settingsDict.GetValueOrDefault("description", "");
        var currency = settingsDict.GetValueOrDefault("currency", "CHF");
        var projectImage = settingsDict.GetValueOrDefault("project_image", "");
        var visibleTabs = settingsDict.GetValueOrDefault("visible_tabs", "");
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Projekt 1 erstellen
        using var pCmd = con.CreateCommand();
        pCmd.CommandText = @"
            INSERT INTO projects (id, name, description, start_date, currency, project_image, visible_tabs, created_at)
            VALUES (1, @name, @desc, @sd, @cur, @img, @tabs, @ca)";
        pCmd.Parameters.AddWithValue("@name", projectName);
        pCmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? DBNull.Value : (object)description);
        pCmd.Parameters.AddWithValue("@sd", string.IsNullOrEmpty(startDate) ? DBNull.Value : (object)startDate);
        pCmd.Parameters.AddWithValue("@cur", currency);
        pCmd.Parameters.AddWithValue("@img", string.IsNullOrEmpty(projectImage) ? DBNull.Value : (object)projectImage);
        pCmd.Parameters.AddWithValue("@tabs", string.IsNullOrEmpty(visibleTabs) ? DBNull.Value : (object)visibleTabs);
        pCmd.Parameters.AddWithValue("@ca", createdAt);
        pCmd.ExecuteNonQuery();

        // state → state_v2 migrieren
        using var stateCmd = con.CreateCommand();
        stateCmd.CommandText = "INSERT OR IGNORE INTO state_v2 (project_id, key, value) SELECT 1, key, value FROM state";
        stateCmd.ExecuteNonQuery();

        // Admin-User zu project_members hinzufügen (settings werden in MigrateSettingsToV2 kopiert)
        using var uCmd = con.CreateCommand();
        uCmd.CommandText = "SELECT id FROM users WHERE is_admin = 1 ORDER BY id LIMIT 1";
        var adminId = uCmd.ExecuteScalar();
        if (adminId != null)
        {
            using var mCmd = con.CreateCommand();
            mCmd.CommandText = @"
                INSERT OR IGNORE INTO project_members (project_id, user_id, role)
                VALUES (1, @uid, 'admin')";
            mCmd.Parameters.AddWithValue("@uid", adminId);
            mCmd.ExecuteNonQuery();

            // ersten Admin auch als platform_admin markieren
            using var paCmd = con.CreateCommand();
            paCmd.CommandText = "UPDATE users SET is_platform_admin = 1 WHERE id = @uid";
            paCmd.Parameters.AddWithValue("@uid", adminId);
            paCmd.ExecuteNonQuery();
        }
    }

    private static void MigrateWeeksConstraint(SqliteConnection con)
    {
        // Prüfen ob die alte globale UNIQUE-Constraint auf number noch existiert
        using var schemaCmd = con.CreateCommand();
        schemaCmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='weeks'";
        var schema = (schemaCmd.ExecuteScalar() as string) ?? "";

        // Falls bereits UNIQUE(project_id, number) vorhanden → fertig
        if (!schema.Contains("number     INTEGER NOT NULL UNIQUE") && !schema.Contains("number INTEGER NOT NULL UNIQUE"))
            return;

        // Neue weeks-Tabelle ohne globale UNIQUE-Constraint erstellen
        using var pragma1 = con.CreateCommand();
        pragma1.CommandText = "PRAGMA foreign_keys = OFF";
        pragma1.ExecuteNonQuery();

        using var createCmd = con.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE weeks_migration (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                number     INTEGER NOT NULL,
                title      TEXT NOT NULL,
                phase      TEXT NOT NULL,
                badge_pc   TEXT NOT NULL,
                badge_phys TEXT NOT NULL,
                note       TEXT,
                project_id INTEGER NOT NULL DEFAULT 1,
                UNIQUE(project_id, number)
            )";
        createCmd.ExecuteNonQuery();

        using var copyCmd = con.CreateCommand();
        copyCmd.CommandText = "INSERT OR IGNORE INTO weeks_migration SELECT id, number, title, phase, badge_pc, badge_phys, note, project_id FROM weeks";
        copyCmd.ExecuteNonQuery();

        using var dropCmd = con.CreateCommand();
        dropCmd.CommandText = "DROP TABLE weeks";
        dropCmd.ExecuteNonQuery();

        using var renameCmd = con.CreateCommand();
        renameCmd.CommandText = "ALTER TABLE weeks_migration RENAME TO weeks";
        renameCmd.ExecuteNonQuery();

        using var pragma2 = con.CreateCommand();
        pragma2.CommandText = "PRAGMA foreign_keys = ON";
        pragma2.ExecuteNonQuery();
    }

    private static void MigrateSettingsToV2(SqliteConnection con)
    {
        using var checkCmd = con.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM settings_v2 WHERE project_id = 1";
        var count = (long)(checkCmd.ExecuteScalar() ?? 0L);
        if (count > 0) return;

        using var migrateCmd = con.CreateCommand();
        migrateCmd.CommandText = @"
            INSERT OR IGNORE INTO settings_v2 (project_id, key, value)
            SELECT 1, key, value FROM settings";
        migrateCmd.ExecuteNonQuery();
    }
}
