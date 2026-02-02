using System.Collections.Generic;

public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class FoodItem
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public int Quantity { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
}


public class Order
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public int CustomerId { get; set; }
    public double Total { get; set; }
    public string Status { get; set; } = "Placed";
    public string OrderDate { get; set; } = "";
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int FoodItemId { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public int Quantity { get; set; }
}

public class DeletionRequest
{
    public int Id { get; set; }
    public string EntityType { get; set; } = ""; // Vendor/FoodItem/User
    public int EntityId { get; set; }
    public string RequestedBy { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string RequestDate { get; set; } = "";
}

public class CartItem
{
    public int FoodId { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public int Qty { get; set; }
}

public static class Cart
{
    public static int VendorId = -1;
    public static List<CartItem> Items = new();

    public static void Clear()
    {
        VendorId = -1;
        Items.Clear();
    }
}
