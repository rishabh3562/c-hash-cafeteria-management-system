using System;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;


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
            else if (ch == 3) AdminLogin();

            else if (ch == 99 && SEED_MODE) { Seed(); }
            else break;
        }
    }

    // ---------------- AUTH ----------------

    static string ReadPassword()
    {
        return Console.ReadLine() ?? "";
    }
    static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
    static bool IsValidPassword(string p)
    {
        if (p.Length < 8) return false;
        if (!p.Any(char.IsUpper)) return false;
        if (!p.Any(char.IsLower)) return false;
        if (!p.Any(char.IsDigit)) return false;
        if (!p.Any(ch => !char.IsLetterOrDigit(ch))) return false;
        return true;
    }

    static int SelectVendor(bool includeInactive = false)
    {
        using var con = Db.GetConn(); con.Open();

        var sql = includeInactive ? "SELECT Id,Name,IsActive FROM Vendors" : "SELECT Id,Name FROM Vendors WHERE IsActive=1";
        var cmd = new SQLiteCommand(sql, con);
        using var r = cmd.ExecuteReader();

        Header("SELECT VENDOR");
        var list = new List<int>();
        while (r.Read())
        {
            int id = Convert.ToInt32(r["Id"]);
            string name = r["Name"].ToString()!;
            list.Add(id);
            if (includeInactive)
                Console.WriteLine($"{id}. {name} Active:{r["IsActive"]}");
            else
                Console.WriteLine($"{id}. {name}");
        }

        Console.Write("\nChoose Vendor (0 Back): ");
        int v = ReadInt(0, 9999);
        return list.Contains(v) ? v : 0;
    }



    static int SelectFoodItem(int vendorId)
    {
        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "SELECT Id,Name,Price,Quantity FROM FoodItems WHERE VendorId=@v",
            con);
        cmd.Parameters.AddWithValue("@v", vendorId);

        using var r = cmd.ExecuteReader();

        Header("SELECT FOOD ITEM");

        var list = new List<int>();
        while (r.Read())
        {
            int id = Convert.ToInt32(r["Id"]);
            list.Add(id);
            Console.WriteLine($"{id}. {r["Name"]} ₹{r["Price"]} Qty:{r["Quantity"]}");
        }

        Console.Write("\nChoose Food (0 Back): ");
        int f = ReadInt(0, 9999);

        return list.Contains(f) ? f : 0;
    }

    static void CustomerAuth()
    {
        Header("CUSTOMER LOGIN / REGISTER");

        Console.Write("Username: ");
        string user = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(user)) { Error("Username required"); return; }

        Console.Write("Password: ");
        // string pass = ReadPassword();
        // if (string.IsNullOrEmpty(pass)) { Error("Password required"); return; }

        //new
        string pass = ReadPassword();
        if (!IsValidPassword(pass))
        {
            Error("Password must be 8 chars with Upper, Lower, Number, Special");
            return;
        }
        else if (string.IsNullOrEmpty(pass)) { Error("Password required"); return; }
        string hash = Hash(pass);

        using var con = Db.GetConn();
        con.Open();

        // 1. CHECK IF USERNAME EXISTS
        var checkCmd = new SQLiteCommand(
     "SELECT Id, Name, PasswordHash, IsActive FROM Users WHERE Username=@u AND Role='Customer'",
     con);

        checkCmd.Parameters.AddWithValue("@u", user);

        using var r = checkCmd.ExecuteReader();

        if (r.Read())
        {
            int isActive = Convert.ToInt32(r["IsActive"]);
            string dbHash = Convert.ToString(r["PasswordHash"]) ?? "";

            if (isActive == 0)
            {
                Error("Account is deactivated. Contact admin or create new username.");
                return;
            }

            if (dbHash != hash)
            {
                Error("Wrong password");
                return;
            }

            CurrentUserId = Convert.ToInt32(r["Id"]);
            CurrentUserName = Convert.ToString(r["Name"]) ?? "";
            Success("Login Success");
            CustomerMenu();
            return;
        }

        // 2. USER DOES NOT EXIST → REGISTER
        Console.WriteLine("\nNo account found — registering new customer.");
        Console.Write("Full Name: ");
        string name = ReadName();

        var ins = new SQLiteCommand(
            "INSERT INTO Users(Username,PasswordHash,Name,Role,IsActive) VALUES(@u,@p,@n,'Customer',1); SELECT last_insert_rowid();",
            con);
        ins.Parameters.AddWithValue("@u", user);
        ins.Parameters.AddWithValue("@p", hash);
        ins.Parameters.AddWithValue("@n", name);

        try
        {
            long newId = (long)ins.ExecuteScalar()!;
            CurrentUserId = Convert.ToInt32(newId);
            CurrentUserName = name;
            Success("Registered & Logged in");
            CustomerMenu();
        }
        catch (SQLiteException)
        {
            Error("Username already exists but account is disabled. Contact admin.");
        }

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
        while (true)
        {
            using var con = Db.GetConn();
            con.Open();

            var r = new SQLiteCommand(
                "SELECT Id,Name,Price,Quantity FROM FoodItems WHERE VendorId=@v AND Quantity>0", con);
            r.Parameters.AddWithValue("@v", vid);

            using var reader = r.ExecuteReader();

            Header($"MENU - Vendor {vid}");

            while (reader.Read())
            {
                Console.WriteLine($"{reader["Id"]}. {reader["Name"]} ₹{reader["Price"]} Qty:{reader["Quantity"]}");
            }

            Console.WriteLine("\n1. Add item to cart");
            Console.WriteLine("2. Switch Vendor");
            Console.WriteLine("0. Back");

            int ch = ReadInt(0, 3);


            if (ch == 1)
            {
                Console.Write("Food Id: ");
                int fid = ReadInt(1, 9999);

                if (!ItemBelongsToVendor(fid, vid))
                {
                    Error("Invalid item for vendor");
                    continue;
                }

                Console.Write("Qty: ");
                int q = ReadInt(1, 999);
                AddToCart(vid, fid, q);
            }

            // 2 is return back to vendor list
            else if (ch == 2)
            {
                BrowseVendorsForCustomer();
                return;
            }
            else
            {
                return;
            }

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
    static int SelectCustomer()
    {
        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "SELECT Id,Name,Username FROM Users WHERE Role='Customer' AND IsActive=1",
            con);

        using var r = cmd.ExecuteReader();

        var list = new List<int>();
        Header("SELECT CUSTOMER");

        while (r.Read())
        {
            int id = Convert.ToInt32(r["Id"]);
            list.Add(id);
            Console.WriteLine($"{id}. {r["Name"]} ({r["Username"]})");
        }

        Console.Write("\nChoose Customer (0 Back): ");
        int c = ReadInt(0, 9999);

        return list.Contains(c) ? c : 0;
    }

    static void AddToCart(int vendorId, int foodId, int qty)
    {
        if (qty <= 0) { Error("Quantity must be > 0"); return; }

        using var con = Db.GetConn();
        con.Open();
        var cmd = new SQLiteCommand("SELECT Name,Price,Quantity,IsActive FROM FoodItems WHERE Id=@id AND VendorId=@v", con);
        cmd.Parameters.AddWithValue("@id", foodId);
        cmd.Parameters.AddWithValue("@v", vendorId);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            Error("Item not found");
            return;
        }
        int available = Convert.ToInt32(rdr["Quantity"]);
        int isActive = Convert.ToInt32(rdr["IsActive"]);
        if (isActive == 0) { Error("Item is no longer available"); return; }
        if (qty > available)
        {
            Error($"Only {available} available");
            return;
        }

        if (available == 0)
        {
            Error("Out of stock");
            return;
        }

        string name = Convert.ToString(rdr["Name"]) ?? "";
        double price = Convert.ToDouble(rdr["Price"]);

        // add with vendor id
        var existing = Cart.Items.Find(x => x.FoodId == foodId && x.VendorId == vendorId);
        if (existing != null) existing.Qty += qty;
        else Cart.Items.Add(new CartItem { VendorId = vendorId, FoodId = foodId, Name = name, Price = price, Qty = qty });

        Success("Added to cart");
    }

    static string GetVendorName(int vendorId)
    {
        using var con = Db.GetConn(); con.Open();
        var cmd = new SQLiteCommand("SELECT Name FROM Vendors WHERE Id=@id", con);
        cmd.Parameters.AddWithValue("@id", vendorId);
        var obj = cmd.ExecuteScalar();
        return obj == null ? $"Vendor {vendorId}" : Convert.ToString(obj)!;
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
            string vname = GetVendorName(it.VendorId);
            Console.WriteLine($"[V:{it.VendorId} - {vname}] {it.FoodId}. {it.Name} x{it.Qty} @ ₹{it.Price:F2} = ₹{it.Price * it.Qty:F2}");
            total += it.Price * it.Qty;
        }
        Console.WriteLine($"\nTotal: ₹{total:F2}");
        Console.WriteLine("\n1. Place Order (will create one order per vendor)");
        Console.WriteLine("2. Clear Cart");
        Console.WriteLine("0. Back");

        int ch = ReadInt(0, 2);
        if (ch == 1) PlaceOrder();
        else if (ch == 2) { Cart.Clear(); Success("Cart cleared"); }
    }

    static void PlaceOrder()
    {
        if (CurrentUserId <= 0)
        {
            Error("You must be logged in to place an order.");
            return;
        }

        if (Cart.Items.Count == 0)
        {
            Error("Cart empty");
            return;
        }

        using var con = Db.GetConn();
        con.Open();

        // validate all quantities first
        foreach (var it in Cart.Items)
            if (it.Qty <= 0) { Error("Invalid cart item quantity."); return; }

        // group by vendor -> create separate order per vendor
        var groups = Cart.Items.GroupBy(i => i.VendorId);
        try
        {
            foreach (var g in groups)
            {
                using var tx = con.BeginTransaction();
                double total = g.Sum(i => i.Price * i.Qty);

                var insOrder = new SQLiteCommand(
                    "INSERT INTO Orders(VendorId,CustomerId,Total,Status,OrderDate) VALUES(@v,@c,@t,'Placed',@d); SELECT last_insert_rowid();",
                    con, tx);
                insOrder.Parameters.AddWithValue("@v", g.Key);
                insOrder.Parameters.AddWithValue("@c", CurrentUserId);
                insOrder.Parameters.AddWithValue("@t", total);
                insOrder.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));

                var orderIdObj = insOrder.ExecuteScalar();
                if (orderIdObj == null) throw new Exception("Failed to insert order");
                long orderId = Convert.ToInt64(orderIdObj);

                foreach (var it in g)
                {
                    var stockCmd = new SQLiteCommand("SELECT Quantity FROM FoodItems WHERE Id=@id AND IsActive=1", con, tx);
                    stockCmd.Parameters.AddWithValue("@id", it.FoodId);
                    var qtyObj = stockCmd.ExecuteScalar();
                    if (qtyObj == null) throw new Exception($"Item {it.Name} not found");
                    long available = Convert.ToInt64(qtyObj);
                    if (it.Qty > available)
                        throw new Exception($"Not enough stock for item {it.Name}. Available: {available}");

                    var insItem = new SQLiteCommand(
                        "INSERT INTO OrderItems(OrderId,FoodItemId,Name,Price,Quantity) VALUES(@o,@f,@n,@p,@q)",
                        con, tx);
                    insItem.Parameters.AddWithValue("@o", orderId);
                    insItem.Parameters.AddWithValue("@f", it.FoodId);
                    insItem.Parameters.AddWithValue("@n", it.Name);
                    insItem.Parameters.AddWithValue("@p", it.Price);
                    insItem.Parameters.AddWithValue("@q", it.Qty);
                    insItem.ExecuteNonQuery();

                    var upd = new SQLiteCommand("UPDATE FoodItems SET Quantity = Quantity - @q WHERE Id=@id AND Quantity >= @q", con, tx);
                    upd.Parameters.AddWithValue("@q", it.Qty);
                    upd.Parameters.AddWithValue("@id", it.FoodId);
                    int changed = upd.ExecuteNonQuery();
                    if (changed == 0)
                        throw new Exception($"Failed to decrement stock for {it.Name} (concurrent change).");
                }

                tx.Commit();
                Success($"Order placed for Vendor {g.Key} (OrderId: {orderId})");

            }

            Cart.Clear();
        }
        catch (Exception ex)
        {
            Error("Order failed: " + ex.Message);
        }
    }
    static void ProcessDeletionRequest()
    {
        Header("PROCESS DELETION REQUEST");
        Console.Write("Request Id: ");
        int rid = ReadInt(1, 999999);

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand("SELECT EntityType,EntityId,Status FROM DeletionRequests WHERE Id=@i", con);
        cmd.Parameters.AddWithValue("@i", rid);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
        {
            Error("Request not found");
            return;
        }

        string status = Convert.ToString(r["Status"]) ?? "";
        if (status != "Pending")
        {
            Error("Request already processed");
            return;
        }

        string type = Convert.ToString(r["EntityType"]) ?? "";
        int eid = Convert.ToInt32(r["EntityId"]);

        Console.WriteLine($"Request: {type} {eid}");
        Console.WriteLine("1. Approve (soft-delete)");
        Console.WriteLine("2. Reject");
        Console.WriteLine("3. HARD DELETE");
        Console.WriteLine("0. Back");
        int ch = ReadInt(0, 3);
        if (ch == 0) return;

        if (ch == 1)
        {
            // Approve (soft-delete)
            if (type == "User" || type == "Customer")
            {
                var updU = new SQLiteCommand("UPDATE Users SET IsActive=0 WHERE Id=@id", con);
                updU.Parameters.AddWithValue("@id", eid);
                updU.ExecuteNonQuery();
            }
            else if (type == "FoodItem")
            {
                // soft-delete food item
                var updF = new SQLiteCommand("UPDATE FoodItems SET IsActive=0 WHERE Id=@id", con);
                updF.Parameters.AddWithValue("@id", eid);
                updF.ExecuteNonQuery();
            }

            var updReq = new SQLiteCommand("UPDATE DeletionRequests SET Status='Approved', ProcessedDate=@d WHERE Id=@i", con);
            updReq.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));
            updReq.Parameters.AddWithValue("@i", rid);
            updReq.ExecuteNonQuery();

            Success("Request approved and processed");
        }
        else if (ch == 3)
        {
            if (type == "User" || type == "Customer")
            {
                new SQLiteCommand("DELETE FROM Users WHERE Id=@id", con)
                { Parameters = { new SQLiteParameter("@id", eid) } }
                    .ExecuteNonQuery();
                Success("Request approved and processed");
            }
            else if (type == "FoodItem")
            {
                new SQLiteCommand("DELETE FROM FoodItems WHERE Id=@id", con)
                { Parameters = { new SQLiteParameter("@id", eid) } }
                .ExecuteNonQuery();
            }

        }

        else
        {
            var updReq = new SQLiteCommand("UPDATE DeletionRequests SET Status='Rejected', ProcessedDate=@d WHERE Id=@i", con);
            updReq.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));
            updReq.Parameters.AddWithValue("@i", rid);
            updReq.ExecuteNonQuery();
            Success("Request rejected");
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
    static void AdminLogin()
    {
        Header("ADMIN LOGIN");

        Console.Write("Username: ");
        string u = Console.ReadLine() ?? "";

        Console.Write("Password: ");
        string p = ReadPassword();
        string h = Hash(p);

        using var con = Db.GetConn();
        con.Open();

        var cmd = new SQLiteCommand(
            "SELECT Id,Name FROM Users WHERE Username=@u AND PasswordHash=@p AND Role='Admin' AND IsActive=1",
            con);

        cmd.Parameters.AddWithValue("@u", u);
        cmd.Parameters.AddWithValue("@p", h);

        using var r = cmd.ExecuteReader();

        if (!r.Read())
        {
            Error("Invalid Admin Credentials");
            return;
        }

        AdminMenu();
    }

    // ---------------- VENDOR ----------------
  

    // ----------- new vendor login with common pass --- -----
    static void VendorLogin()
    {
        Header("VENDOR LOGIN");

        int vid = SelectVendor();
        if (vid == 0) return;

        Console.Write("Password: ");
        string p = ReadPassword();
        if (p != "vendor@123")
        {
            Error("Wrong Vendor Password");
            return;
        }

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
                int id = SelectFoodItem(vid);
                if (id == 0) return;
                RequestDeletion("FoodItem", id);

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
            int q = Convert.ToInt32(reader["Quantity"]);
            if (q == 0)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"{reader["Id"]}. {reader["Name"]} ₹{Convert.ToDouble(reader["Price"]):F2} Qty:{q}");

            Console.ResetColor();
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
        int id = SelectFoodItem(vid);
        if (id == 0) return;

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
        int id = SelectFoodItem(vid);
        if (id == 0) return;
        Console.Write("Add Qty (use negative to reduce): ");
        int add = ReadInt(-9999, 9999);

        using var con = Db.GetConn();
        con.Open();

        // check current qty and vendor match
        var chk = new SQLiteCommand("SELECT Quantity FROM FoodItems WHERE Id=@id AND VendorId=@v", con);
        chk.Parameters.AddWithValue("@id", id);
        chk.Parameters.AddWithValue("@v", vid);
        var res = chk.ExecuteScalar();
        if (res == null) { Error("No matching item"); return; }
        int cur = Convert.ToInt32(res);
        if (cur + add < 0) { Error($"Cannot reduce below 0 (current: {cur})"); return; }

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
            Console.WriteLine("6. Process Deletion Request");
            Console.WriteLine("0. Back");

            // allow 0..6
            int ch = ReadInt(0, 6);

            if (ch == 1) ShowVendors();
            else if (ch == 2) ToggleVendor();
            else if (ch == 3) ShowCustomers();
            else if (ch == 4) DisableCustomer();
            else if (ch == 5) ShowDeletionRequests();
            else if (ch == 6) ProcessDeletionRequest();
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
      
        int id = SelectVendor(true);

        if (id == 0) return;

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

        // select Username instead of Mobile (Mobile column removed)
        var cmd = new SQLiteCommand("SELECT Id,Name,Username,IsActive FROM Users WHERE Role='Customer'", con);
        using var r = cmd.ExecuteReader();

        Header("CUSTOMERS");

        while (r.Read())
            Console.WriteLine($"{r["Id"]} {r["Name"]} {r["Username"]} Active:{r["IsActive"]}");

        Console.ReadKey();
    }

    static void DisableCustomer()
    {
        int id = SelectCustomer();
        if (id == 0) return;

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
        var cmd = new SQLiteCommand("SELECT Id,EntityType,EntityId,RequestedBy,Status,RequestDate,ProcessedDate FROM DeletionRequests ORDER BY Id DESC", con);
        using var r = cmd.ExecuteReader();
        Header("DELETION REQUESTS");
        while (r.Read())
            Console.WriteLine($"{r["Id"]} | {r["EntityType"]}:{r["EntityId"]} | By:{r["RequestedBy"]} | {r["Status"]} | {r["RequestDate"]} | Processed:{r["ProcessedDate"]}");
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
        // Console.Clear();
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

        // ---------- VENDORS ----------
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

        // ---------- FOOD ITEMS ----------
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

        // ---------- Vendors USER ----------
        var vuserCheck = new SQLiteCommand("SELECT COUNT(1) FROM Users WHERE Role='Vendor'", con);
        long vuc = (long)vuserCheck.ExecuteScalar()!;
        if (vuc == 0)
        {
            string vhash = Hash("vendor@123");
            var vcmd = new SQLiteCommand(@"
            INSERT INTO Users(Username,PasswordHash,Name,Role,IsActive) VALUES
            ('vendor1',@p,'Vendor One','Vendor',1),
            ('vendor2',@p,'Vendor Two','Vendor',1),
            ('vendor3',@p,'Vendor Three','Vendor',1);", con);
            vcmd.Parameters.AddWithValue("@p", vhash);
            vcmd.ExecuteNonQuery();
        }
        // ---------- ADMIN USER ----------
        var c3 = new SQLiteCommand("SELECT COUNT(1) FROM Users WHERE Role='Admin'", con);
        long acount = (long)c3.ExecuteScalar()!;
        if (acount == 0)
        {
            string hash = Hash("admin123"); // default password

            var ad = new SQLiteCommand(@"
        INSERT INTO Users(Username,PasswordHash,Name,Role,IsActive)
        VALUES('admin',@p,'Administrator','Admin',1);", con);

            ad.Parameters.AddWithValue("@p", hash);
            ad.ExecuteNonQuery();
        }

        Success("Seeded Vendors + Food + Admin (admin/admin123)");
    }

}
