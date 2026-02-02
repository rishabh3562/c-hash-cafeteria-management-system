using System.Data.SQLite;

class Program
{
    static bool SEED_MODE = false; // KEEP FALSE IN DEMO

    static void Main()
    {
        Db.Init();
        if (SEED_MODE) Seed();

        while (true)
        {
            Header("CAFETERIA MANAGEMENT SYSTEM");

            Console.WriteLine("1. Customer");
            Console.WriteLine("2. Vendor");
            Console.WriteLine("3. Admin");
            if (SEED_MODE) Console.WriteLine("99. Seed Data");
            Console.WriteLine("0. Exit");

            int ch = ReadInt(0, 99);

            if (ch == 1) CustomerMenu();
            else if (ch == 2) VendorMenu();
            else if (ch == 3) AdminMenu();
            else if (ch == 99 && SEED_MODE) Seed();
            else break;
        }
    }

    // ---------------- CUSTOMER ----------------
    static void CustomerMenu()
    {
        while (true)
        {
            Header("CUSTOMER");

            Console.WriteLine("1. View Vendors");
            Console.WriteLine("2. Checkout");
            Console.WriteLine("3. Request Account Deletion");
            Console.WriteLine("0. Back");

            int ch = ReadInt(0, 3);

            if (ch == 1) ShowVendors();
            else if (ch == 2) Checkout();
            else if (ch == 3) RequestDeletion("Customer", 1);
            else return;
        }
    }

    static void ShowVendors()
    {
        using var con = Db.GetConn();
        con.Open();

        var r = new SQLiteCommand(
            "SELECT Id,Name FROM Vendors WHERE IsActive=1", con).ExecuteReader();

        Header("VENDORS");

        while (r.Read())
            Console.WriteLine($"{r["Id"]}. {r["Name"]}");

        Console.Write("\nSelect Vendor (0 Back): ");
        int vid = ReadInt(0, 999);
        if (vid == 0) return;

        ShowMenu(vid);
    }

    static void ShowMenu(int vid)
    {
        using var con = Db.GetConn();
        con.Open();

        var r = new SQLiteCommand(
            "SELECT Id,Name,Price,Quantity FROM FoodItems WHERE VendorId=@v",
            con);
        r.Parameters.AddWithValue("@v", vid);

        var reader = r.ExecuteReader();

        Header($"MENU - Vendor {vid}");

        while (reader.Read())
        {
            Console.WriteLine(
                $"{reader["Id"]}. {reader["Name"]} ₹{reader["Price"]} Qty:{reader["Quantity"]}");
        }

        Console.WriteLine("\n(Only viewing for now)");
        Console.ReadKey();
    }

    static void Checkout()
    {
        Header("CHECKOUT");
        Console.WriteLine("Cart logic not added yet.");
        Console.ReadKey();
    }

    // ---------------- VENDOR ----------------
    static void VendorMenu()
    {
        Header("VENDOR");
        Console.Write("VendorId: ");
        int vid = ReadInt(1, 999);

        while (true)
        {
            Header($"VENDOR PANEL [{vid}]");

            Console.WriteLine("1. View Food Items");
            Console.WriteLine("2. Add Food Item");
            Console.WriteLine("3. Update Food Item");
            Console.WriteLine("4. Request Food Deletion");
            Console.WriteLine("0. Back");

            int ch = ReadInt(0, 4);

            if (ch == 1) ShowMenu(vid);
            else if (ch == 2) AddFood(vid);
            else if (ch == 3) UpdateFood(vid);
            else if (ch == 4)
            {
                Console.Write("Food Id: ");
                RequestDeletion("FoodItem", ReadInt(1, 999));
            }
            else return;
        }
    }

    static void AddFood(int vid)
    {
        Console.Write("Name: ");
        string n = ReadName();
        Console.Write("Price: ");
        double p = ReadDouble();
        Console.Write("Qty: ");
        int q = ReadInt(0, 999);

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "INSERT INTO FoodItems(VendorId,Name,Price,Quantity) VALUES(@v,@n,@p,@q)", con);

        cmd.Parameters.AddWithValue("@v", vid);
        cmd.Parameters.AddWithValue("@n", n);
        cmd.Parameters.AddWithValue("@p", p);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.ExecuteNonQuery();

        Success("Food Added");
    }

    static void UpdateFood(int vid)
    {
        Console.Write("Food Id: ");
        int id = ReadInt(1, 999);
        Console.Write("New Price: ");
        double p = ReadDouble();

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "UPDATE FoodItems SET Price=@p WHERE Id=@id AND VendorId=@v", con);

        cmd.Parameters.AddWithValue("@p", p);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@v", vid);
        cmd.ExecuteNonQuery();

        Success("Updated");
    }

    // ---------------- ADMIN ----------------
    static void AdminMenu()
    {
        Header("ADMIN");
        Console.WriteLine("Basic admin panel for requests only.");
        Console.ReadKey();
    }

    // ---------------- REQUESTS ----------------
    static void RequestDeletion(string type, int id)
    {
        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(@"
        INSERT INTO DeletionRequests
        (EntityType,EntityId,RequestedBy,Status,RequestDate)
        VALUES(@t,@i,'User','Pending',@d)", con);

        cmd.Parameters.AddWithValue("@t", type);
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString());
        cmd.ExecuteNonQuery();

        Success("Request Sent");
    }

    // ---------------- UX ----------------
    static void Header(string title)
    {
        Console.Clear();
        Console.WriteLine("================================");
        Console.WriteLine($" {title}");
        Console.WriteLine("================================");
    }

    static int ReadInt(int min, int max)
    {
        while (true)
        {
            if (int.TryParse(Console.ReadLine(), out int x) && x >= min && x <= max)
                return x;
            Error("Invalid Number");
        }
    }

    static double ReadDouble()
    {
        while (true)
        {
            if (double.TryParse(Console.ReadLine(), out double x) && x > 0)
                return x;
            Error("Invalid Price");
        }
    }

    static string ReadName()
    {
        while (true)
        {
            string s = Console.ReadLine()!.Trim();
            if (!string.IsNullOrWhiteSpace(s)) return s;
            Error("Invalid Name");
        }
    }

    static void Success(string m)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(m);
        Console.ResetColor();
        Console.ReadKey();
    }

    static void Error(string m)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(m);
        Console.ResetColor();
    }

    static void Seed()
    {
        using var con = Db.GetConn();
        con.Open();
        new SQLiteCommand(@"
        INSERT INTO Vendors(Name) VALUES
        ('Fresh Bites'),
        ('Snack Hub'),
        ('South Express');", con).ExecuteNonQuery();

        Success("Seeded");
    }
}
