using System;
using System.Data.SQLite;
using System.Collections.Generic;

// Entry point
class Program
{
    // Toggle to seed sample data (set true once to seed)
    static bool SEED_MODE = false;

    // Session
    static int CurrentUserId = -1;
    static string CurrentUserName = "";

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

            if (ch == 1) { CustomerAuth(); }
            else if (ch == 2) VendorLogin();
            else if (ch == 3) AdminMenu();
            else if (ch == 99 && SEED_MODE) { Seed(); }
            else break;
        }
    }

    // ---------------- AUTH ----------------
    static void CustomerAuth()
    {
        Header("CUSTOMER LOGIN / REGISTER");

        Console.Write("Mobile: ");
        string mob = Console.ReadLine()!.Trim();

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "SELECT Id, Name FROM Users WHERE Mobile=@m AND Role='Customer' AND IsActive=1", con);
        cmd.Parameters.AddWithValue("@m", mob);

        var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            CurrentUserId = Convert.ToInt32(reader["Id"]);
            CurrentUserName = Convert.ToString(reader["Name"]) ?? "";
            Success($"Login Success. Welcome {CurrentUserName}");
            CustomerMenu();
            return;
        }

        Console.Write("New User Name: ");
        string name = ReadName();

        var ins = new SQLiteCommand(
            "INSERT INTO Users(Name,Mobile,Role,IsActive) VALUES(@n,@m,'Customer',1); SELECT last_insert_rowid();", con);
        ins.Parameters.AddWithValue("@n", name);
        ins.Parameters.AddWithValue("@m", mob);
        long newId = (long)ins.ExecuteScalar()!;

        CurrentUserId = Convert.ToInt32(newId);
        CurrentUserName = name;
        Success("Registered & Logged in");
        CustomerMenu();
    }

    // ---------------- CUSTOMER ----------------
    static void CustomerMenu()
    {
        while (true)
        {
            Header($"CUSTOMER - {CurrentUserName} (Id:{CurrentUserId})");

            Console.WriteLine("1. View Vendors");
            Console.WriteLine("2. View Cart");
            Console.WriteLine("3. My Orders");
            Console.WriteLine("4. Request Account Deletion");
            Console.WriteLine("0. Logout");

            int ch = ReadInt(0, 4);

            if (ch == 1) BrowseVendorsForCustomer();
            else if (ch == 2) ViewCartMenu();
            else if (ch == 3) ShowCustomerOrders();
            else if (ch == 4)
            {
                RequestDeletion("User", CurrentUserId);
            }
            else
            {
                Cart.Clear();
                CurrentUserId = -1;
                CurrentUserName = "";
                return;
            }
        }
    }

    static void BrowseVendorsForCustomer()
    {
        using var con = Db.GetConn();
        con.Open();

        var rCmd = new SQLiteCommand("SELECT Id,Name FROM Vendors WHERE IsActive=1", con);
        using var r = rCmd.ExecuteReader();

        Header("VENDORS");

        var list = new List<int>();
        while (r.Read())
        {
            int id = Convert.ToInt32(r["Id"]);
            string name = Convert.ToString(r["Name"]) ?? "";
            list.Add(id);
            Console.WriteLine($"{id}. {name}");
        }

        Console.Write("\nSelect Vendor (0 Back): ");
        int vid = ReadInt(0, 999);
        if (vid == 0) return;

        if (!list.Contains(vid))
        {
            Error("Invalid Vendor");
            return;
        }

        ShowMenuForCustomer(vid);
    }

    static void ShowMenuForCustomer(int vid)
    {
        using var con = Db.GetConn();
        con.Open();

        var r = new SQLiteCommand(
            "SELECT Id,Name,Price,Quantity FROM FoodItems WHERE VendorId=@v AND Quantity>0", con);
        r.Parameters.AddWithValue("@v", vid);

        using var reader = r.ExecuteReader();

        Header($"MENU - Vendor {vid}");

        var available = new List<int>();
        while (reader.Read())
        {
            int id = Convert.ToInt32(reader["Id"]);
            string name = Convert.ToString(reader["Name"]) ?? "";
            double price = Convert.ToDouble(reader["Price"]);
            int qty = Convert.ToInt32(reader["Quantity"]);
            available.Add(id);
            Console.WriteLine($"{id}. {name} ₹{price:F2} Qty:{qty}");
        }

        Console.WriteLine("\n1. Add item to cart");
        Console.WriteLine("0. Back");
        int ch = ReadInt(0, 1);
        if (ch == 1)
        {
            Console.Write("Food Id: ");
            int fid = ReadInt(1, 9999);
            if (!ItemBelongsToVendor(fid, vid))
            {
                Error("Invalid item for vendor");
                return;
            }
            Console.Write("Qty: ");
            int q = ReadInt(1, 999);
            AddToCart(vid, fid, q);
        }
    }

    static bool ItemBelongsToVendor(int foodId, int vendorId)
    {
        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT COUNT(1) FROM FoodItems WHERE Id=@id AND VendorId=@v AND Quantity>0", con);
        cmd.Parameters.AddWithValue("@id", foodId);
        cmd.Parameters.AddWithValue("@v", vendorId);
        long cnt = (long)cmd.ExecuteScalar()!;
        return cnt > 0;
    }

    static void AddToCart(int vendorId, int foodId, int qty)
    {
        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT Name,Price,Quantity FROM FoodItems WHERE Id=@id AND VendorId=@v", con);
        cmd.Parameters.AddWithValue("@id", foodId);
        cmd.Parameters.AddWithValue("@v", vendorId);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            Error("Item not found");
            return;
        }
        int available = Convert.ToInt32(rdr["Quantity"]);
        if (qty > available)
        {
            Error($"Only {available} available");
            return;
        }
        string name = Convert.ToString(rdr["Name"]) ?? "";
        double price = Convert.ToDouble(rdr["Price"]);

        if (Cart.VendorId == -1) Cart.VendorId = vendorId;
        if (Cart.VendorId != vendorId)
        {
            Error("Cart has items from another vendor. Clear cart first.");
            return;
        }

        var existing = Cart.Items.Find(x => x.FoodId == foodId);
        if (existing != null) existing.Qty += qty;
        else Cart.Items.Add(new CartItem { FoodId = foodId, Name = name, Price = price, Qty = qty });

        Success("Added to cart");
    }

    static void ViewCartMenu()
    {
        Header("CART");
        if (Cart.Items.Count == 0)
        {
            Console.WriteLine("Cart is empty.");
            Console.ReadKey();
            return;
        }

        double total = 0;
        foreach (var it in Cart.Items)
        {
            Console.WriteLine($"{it.FoodId}. {it.Name} x{it.Qty} @ ₹{it.Price:F2} = ₹{it.Price * it.Qty:F2}");
            total += it.Price * it.Qty;
        }
        Console.WriteLine($"\nTotal: ₹{total:F2}");
        Console.WriteLine("\n1. Place Order");
        Console.WriteLine("2. Clear Cart");
        Console.WriteLine("0. Back");

        int ch = ReadInt(0, 2);
        if (ch == 1) PlaceOrder();
        else if (ch == 2) { Cart.Clear(); Success("Cart cleared"); }
    }

    static void PlaceOrder()
    {
        if (Cart.Items.Count == 0)
        {
            Error("Cart empty");
            return;
        }
        using var con = Db.GetConn();
        con.Open();

        using var tx = con.BeginTransaction();
        try
        {
            double total = 0;
            foreach (var it in Cart.Items) total += it.Qty * it.Price;

            var insOrder = new SQLiteCommand(
                "INSERT INTO Orders(VendorId,CustomerId,Total,Status,OrderDate) VALUES(@v,@c,@t,'Placed',@d); SELECT last_insert_rowid();",
                con, tx);
            insOrder.Parameters.AddWithValue("@v", Cart.VendorId);
            insOrder.Parameters.AddWithValue("@c", CurrentUserId);
            insOrder.Parameters.AddWithValue("@t", total);
            insOrder.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));
            long orderId = (long)insOrder.ExecuteScalar()!;

            foreach (var it in Cart.Items)
            {
                var insItem = new SQLiteCommand(
                    "INSERT INTO OrderItems(OrderId,FoodItemId,Name,Price,Quantity) VALUES(@o,@f,@n,@p,@q)",
                    con, tx);
                insItem.Parameters.AddWithValue("@o", orderId);
                insItem.Parameters.AddWithValue("@f", it.FoodId);
                insItem.Parameters.AddWithValue("@n", it.Name);
                insItem.Parameters.AddWithValue("@p", it.Price);
                insItem.Parameters.AddWithValue("@q", it.Qty);
                insItem.ExecuteNonQuery();

                // decrement stock
                var upd = new SQLiteCommand("UPDATE FoodItems SET Quantity = Quantity - @q WHERE Id=@id", con, tx);
                upd.Parameters.AddWithValue("@q", it.Qty);
                upd.Parameters.AddWithValue("@id", it.FoodId);
                upd.ExecuteNonQuery();
            }

            tx.Commit();
            Cart.Clear();
            Success($"Order placed (Id: {orderId})");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            Error("Order failed: " + ex.Message);
        }
    }

    static void ShowCustomerOrders()
    {
        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT Id,VendorId,Total,Status,OrderDate FROM Orders WHERE CustomerId=@c ORDER BY Id DESC", con);
        cmd.Parameters.AddWithValue("@c", CurrentUserId);
        using var r = cmd.ExecuteReader();
        Header("MY ORDERS");
        while (r.Read())
        {
            Console.WriteLine($"Order {r["Id"]} | Vendor:{r["VendorId"]} | ₹{Convert.ToDouble(r["Total"]):F2} | {r["Status"]} | {r["OrderDate"]}");
            // list items
            var itemsCmd = new SQLiteCommand("SELECT Name,Price,Quantity FROM OrderItems WHERE OrderId=@o", con);
            itemsCmd.Parameters.AddWithValue("@o", r["Id"]);
            using var itR = itemsCmd.ExecuteReader();
            while (itR.Read())
                Console.WriteLine($"   - {itR["Name"]} x{itR["Quantity"]} @ ₹{Convert.ToDouble(itR["Price"]):F2}");
        }
        Console.ReadKey();
    }

    // ---------------- VENDOR ----------------
    static void VendorLogin()
    {
        Header("VENDOR LOGIN");
        Console.Write("VendorId: ");
        int vid = ReadInt(1, 9999);

        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT COUNT(1) FROM Vendors WHERE Id=@id", con);
        cmd.Parameters.AddWithValue("@id", vid);
        long cnt = (long)cmd.ExecuteScalar()!;
        if (cnt == 0) { Error("Vendor not found"); return; }

        VendorMenu(vid);
    }

    static void VendorMenu(int vid)
    {
        while (true)
        {
            Header($"VENDOR PANEL [{vid}]");

            Console.WriteLine("1. View Food Items");
            Console.WriteLine("2. Add Food Item");
            Console.WriteLine("3. Update Food Item Price");
            Console.WriteLine("4. Update Food Item Qty");
            Console.WriteLine("5. Request Food Deletion");
            Console.WriteLine("6. View Orders");
            Console.WriteLine("0. Back");

            int ch = ReadInt(0, 6);

            if (ch == 1) ShowMenu(vid);
            else if (ch == 2) AddFood(vid);
            else if (ch == 3) UpdateFoodPrice(vid);
            else if (ch == 4) UpdateFoodQty(vid);
            else if (ch == 5)
            {
                Console.Write("Food Id: ");
                RequestDeletion("FoodItem", ReadInt(1, 9999));
            }
            else if (ch == 6) ShowVendorOrders(vid);
            else return;
        }
    }

    static void ShowMenu(int vid)
    {
        using var con = Db.GetConn();
        con.Open();

        var r = new SQLiteCommand(
            "SELECT Id,Name,Price,Quantity FROM FoodItems WHERE VendorId=@v", con);
        r.Parameters.AddWithValue("@v", vid);

        using var reader = r.ExecuteReader();

        Header($"MENU - Vendor {vid}");

        while (reader.Read())
        {
            Console.WriteLine($"{reader["Id"]}. {reader["Name"]} ₹{Convert.ToDouble(reader["Price"]):F2} Qty:{reader["Quantity"]}");
        }

        Console.ReadKey();
    }

    static void AddFood(int vid)
    {
        Console.Write("Name: ");
        string n = ReadName();
        Console.Write("Price: ");
        double p = ReadDouble();
        Console.Write("Qty: ");
        int q = ReadInt(0, 9999);

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

    static void UpdateFoodPrice(int vid)
    {
        Console.Write("Food Id: ");
        int id = ReadInt(1, 99999);
        Console.Write("New Price: ");
        double p = ReadDouble();

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand("UPDATE FoodItems SET Price=@p WHERE Id=@id AND VendorId=@v", con);
        cmd.Parameters.AddWithValue("@p", p);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@v", vid);
        int rows = cmd.ExecuteNonQuery();
        if (rows == 0) Error("No matching item");
        else Success("Updated");
    }

    static void UpdateFoodQty(int vid)
    {
        Console.Write("Food Id: ");
        int id = ReadInt(1, 99999);
        Console.Write("Add Qty (use negative to reduce): ");
        int add = ReadInt(-9999, 9999);

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand("UPDATE FoodItems SET Quantity = Quantity + @a WHERE Id=@id AND VendorId=@v", con);
        cmd.Parameters.AddWithValue("@a", add);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@v", vid);
        int rows = cmd.ExecuteNonQuery();
        if (rows == 0) Error("No matching item");
        else Success("Quantity updated");
    }

    static void ShowVendorOrders(int vid)
    {
        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT Id,CustomerId,Total,Status,OrderDate FROM Orders WHERE VendorId=@v ORDER BY Id DESC", con);
        cmd.Parameters.AddWithValue("@v", vid);
        using var r = cmd.ExecuteReader();
        Header($"ORDERS - Vendor {vid}");
        while (r.Read())
        {
            Console.WriteLine($"Order {r["Id"]} | Cust:{r["CustomerId"]} | ₹{Convert.ToDouble(r["Total"]):F2} | {r["Status"]} | {r["OrderDate"]}");
            var itemsCmd = new SQLiteCommand("SELECT Name,Price,Quantity FROM OrderItems WHERE OrderId=@o", con);
            itemsCmd.Parameters.AddWithValue("@o", r["Id"]);
            using var itR = itemsCmd.ExecuteReader();
            while (itR.Read())
                Console.WriteLine($"   - {itR["Name"]} x{itR["Quantity"]} @ ₹{Convert.ToDouble(itR["Price"]):F2}");
        }
        Console.ReadKey();
    }

    // ---------------- ADMIN ----------------
    static void AdminMenu()
    {
        while (true)
        {
            Header("ADMIN");

            Console.WriteLine("1. View Vendors");
            Console.WriteLine("2. Toggle Vendor Active");
            Console.WriteLine("3. View Customers");
            Console.WriteLine("4. Disable Customer");
            Console.WriteLine("5. View Deletion Requests");
            Console.WriteLine("0. Back");

            int ch = ReadInt(0, 5);

            if (ch == 1) ShowVendors();
            else if (ch == 2) ToggleVendor();
            else if (ch == 3) ShowCustomers();
            else if (ch == 4) DisableCustomer();
            else if (ch == 5) ShowDeletionRequests();
            else return;
        }
    }

    static void ShowVendors()
    {
        using var con = Db.GetConn();
        con.Open();

        var r = new SQLiteCommand("SELECT Id,Name,IsActive FROM Vendors", con).ExecuteReader();

        Header("VENDORS");

        while (r.Read())
            Console.WriteLine($"{r["Id"]}. {r["Name"]} Active:{r["IsActive"]}");

        Console.ReadKey();
    }

    static void ToggleVendor()
    {
        Console.Write("Vendor Id: ");
        int id = ReadInt(1, 99999);

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "UPDATE Vendors SET IsActive = CASE IsActive WHEN 1 THEN 0 ELSE 1 END WHERE Id=@i",
            con);
        cmd.Parameters.AddWithValue("@i", id);
        int rows = cmd.ExecuteNonQuery();
        if (rows == 0) Error("No vendor found");
        else Success("Toggled");
    }

    static void ShowCustomers()
    {
        using var con = Db.GetConn();
        con.Open();

        var r = new SQLiteCommand("SELECT Id,Name,Mobile,IsActive FROM Users WHERE Role='Customer'", con)
            .ExecuteReader();

        Header("CUSTOMERS");

        while (r.Read())
            Console.WriteLine($"{r["Id"]} {r["Name"]} {r["Mobile"]} Active:{r["IsActive"]}");

        Console.ReadKey();
    }

    static void DisableCustomer()
    {
        Console.Write("User Id: ");
        int id = ReadInt(1, 99999);

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand("UPDATE Users SET IsActive=0 WHERE Id=@i", con);
        cmd.Parameters.AddWithValue("@i", id);
        int rows = cmd.ExecuteNonQuery();
        if (rows == 0) Error("No user found");
        else Success("Disabled");
    }

    static void ShowDeletionRequests()
    {
        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT Id,EntityType,EntityId,RequestedBy,Status,RequestDate FROM DeletionRequests ORDER BY Id DESC", con);
        using var r = cmd.ExecuteReader();
        Header("DELETION REQUESTS");
        while (r.Read())
            Console.WriteLine($"{r["Id"]} | {r["EntityType"]}:{r["EntityId"]} | By:{r["RequestedBy"]} | {r["Status"]} | {r["RequestDate"]}");
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
        VALUES(@t,@i,@r,'Pending',@d)", con);

        cmd.Parameters.AddWithValue("@t", type);
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@r", CurrentUserId == -1 ? "User" : CurrentUserName);
        cmd.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        Success("Request Sent");
    }

    // ---------------- UX & Utilities ----------------
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
            string? s = Console.ReadLine();
            if (int.TryParse(s, out int x) && x >= min && x <= max)
                return x;
            Error("Invalid Number");
        }
    }

    static double ReadDouble()
    {
        while (true)
        {
            string? s = Console.ReadLine();
            if (double.TryParse(s, out double x) && x > 0)
                return x;
            Error("Invalid Price");
        }
    }

    static string ReadName()
    {
        while (true)
        {
            string s = Console.ReadLine() ?? "";
            s = s.Trim();
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

    // ---------------- SEED ----------------
    static void Seed()
    {
        using var con = Db.GetConn();
        con.Open();

        // seed vendors & foods if empty
        var c1 = new SQLiteCommand("SELECT COUNT(1) FROM Vendors", con);
        long vcount = (long)c1.ExecuteScalar()!;
        if (vcount == 0)
        {
            var cmd = new SQLiteCommand(@"
    INSERT INTO Vendors(Name,IsActive) VALUES
    ('Fresh Bites',1),
    ('Snack Hub',1),
    ('South Express',1);", con);
            cmd.ExecuteNonQuery();
        }

        var c2 = new SQLiteCommand("SELECT COUNT(1) FROM FoodItems", con);
        long fcount = (long)c2.ExecuteScalar()!;
        if (fcount == 0)
        {
            var cmd2 = new SQLiteCommand(@"
    INSERT INTO FoodItems(VendorId,Name,Price,Quantity) VALUES
    (1,'Burger',80,20),
    (1,'Sandwich',50,30),
    (2,'Samosa',20,100),
    (2,'Tea',15,200),
    (3,'Dosa',60,40),
    (3,'Idli',40,50);", con);
            cmd2.ExecuteNonQuery();
        }

        // sample admin user
        var c3 = new SQLiteCommand("SELECT COUNT(1) FROM Users WHERE Role='Admin'", con);
        long acount = (long)c3.ExecuteScalar()!;
        if (acount == 0)
        {
            var ad = new SQLiteCommand("INSERT INTO Users(Name,Mobile,Role,IsActive) VALUES('Admin','0000000000','Admin',1)", con);
            ad.ExecuteNonQuery();
        }

        Success("Seeded Vendors + Food + Admin (if empty)");
    }
}
