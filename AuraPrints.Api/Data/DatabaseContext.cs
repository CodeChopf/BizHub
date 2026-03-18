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
            CREATE TABLE IF NOT EXISTS products (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                sku  TEXT NOT NULL,
                name TEXT NOT NULL,
                type TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS product_variants (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                product_id INTEGER NOT NULL,
                size       TEXT NOT NULL,
                height     TEXT NOT NULL,
                print_time TEXT NOT NULL,
                price      TEXT NOT NULL,
                FOREIGN KEY (product_id) REFERENCES products(id)
            );
            CREATE TABLE IF NOT EXISTS calculations (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                sku        TEXT NOT NULL,
                name       TEXT NOT NULL,
                sale_price TEXT NOT NULL,
                profit     TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS cost_items (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                calculation_id INTEGER NOT NULL,
                label          TEXT NOT NULL,
                amount         TEXT NOT NULL,
                sort_order     INTEGER NOT NULL,
                FOREIGN KEY (calculation_id) REFERENCES calculations(id)
            );
            CREATE TABLE IF NOT EXISTS phase2 (
                id    INTEGER PRIMARY KEY AUTOINCREMENT,
                label TEXT NOT NULL,
                name  TEXT NOT NULL,
                price TEXT NOT NULL,
                note  TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS legal (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                text TEXT NOT NULL
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
            );";
        cmd.ExecuteNonQuery();
    }
}