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

        // Schritt 1: Alte Produkte-Tabellen droppen (Foreign Keys kurz deaktivieren)
        using var dropCmd = con.CreateCommand();
        dropCmd.CommandText = @"
            PRAGMA foreign_keys = OFF;
            DROP TABLE IF EXISTS products_generic;
            DROP TABLE IF EXISTS product_fields;
            DROP TABLE IF EXISTS product_types;
            PRAGMA foreign_keys = ON;";
        dropCmd.ExecuteNonQuery();

        // Schritt 2: Alle Tabellen erstellen
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS weeks (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                number     INTEGER NOT NULL UNIQUE,
                title      TEXT NOT NULL,
                phase      TEXT NOT NULL,
                badge_pc   TEXT NOT NULL,
                badge_phys TEXT NOT NULL,
                note       TEXT
            );
            CREATE TABLE IF NOT EXISTS tasks (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                week_number INTEGER NOT NULL,
                sort_order  INTEGER NOT NULL,
                type        TEXT NOT NULL,
                text        TEXT NOT NULL,
                hours       TEXT NOT NULL,
                FOREIGN KEY (week_number) REFERENCES weeks(number)
            );
            CREATE TABLE IF NOT EXISTS state (
                key   TEXT PRIMARY KEY,
                value INTEGER NOT NULL DEFAULT 0
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
            );";
        cmd.ExecuteNonQuery();
    }
}