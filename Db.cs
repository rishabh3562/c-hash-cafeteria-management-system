using System.Data.SQLite;

public static class Db
{
    private static string cs = "Data Source=cafeteria.db;Version=3;";

    public static SQLiteConnection GetConn()
    {
        return new SQLiteConnection(cs);
    }

    public static void Init()
    {
        using var con = GetConn();
        con.Open();

        // PRAGMAs: WAL, busy timeout, foreign keys
        new SQLiteCommand("PRAGMA journal_mode = WAL;", con).ExecuteNonQuery();
        new SQLiteCommand("PRAGMA busy_timeout = 3000;", con).ExecuteNonQuery();
        new SQLiteCommand("PRAGMA foreign_keys = ON;", con).ExecuteNonQuery();

        string sql = @"
        CREATE TABLE IF NOT EXISTS Vendors(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            IsActive INTEGER DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS FoodItems(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            VendorId INTEGER,
            Name TEXT,
            Price REAL,
            Quantity INTEGER,
            IsActive INTEGER DEFAULT 1,
            FOREIGN KEY(VendorId) REFERENCES Vendors(Id)
        );

        CREATE TABLE IF NOT EXISTS Users(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT UNIQUE,
            PasswordHash TEXT,
            Name TEXT,
            Role TEXT,
            IsActive INTEGER DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS Orders(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            VendorId INTEGER,
            CustomerId INTEGER,
            Total REAL,
            Status TEXT,
            OrderDate TEXT,
            FOREIGN KEY(VendorId) REFERENCES Vendors(Id),
            FOREIGN KEY(CustomerId) REFERENCES Users(Id)
        );

        CREATE TABLE IF NOT EXISTS OrderItems(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OrderId INTEGER,
            FoodItemId INTEGER,
            Name TEXT,
            Price REAL,
            Quantity INTEGER,
            FOREIGN KEY(OrderId) REFERENCES Orders(Id),
            FOREIGN KEY(FoodItemId) REFERENCES FoodItems(Id)
        );

        CREATE TABLE IF NOT EXISTS DeletionRequests(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            EntityType TEXT,
            EntityId INTEGER,
            RequestedBy TEXT,
            Status TEXT,
            RequestDate TEXT,
            ProcessedDate TEXT
        );
        ";

        new SQLiteCommand(sql, con).ExecuteNonQuery();
    }
}
