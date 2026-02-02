public class CartItem
{
    public int VendorId { get; set; }   // NEW
    public int FoodId { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public int Qty { get; set; }
}

public static class Cart
{
    // removed VendorId field to allow multi-vendor items
    public static List<CartItem> Items = new();

    public static void Clear()
    {
        Items.Clear();
    }
}
