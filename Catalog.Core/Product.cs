namespace Catalog.Core;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public ulong PriceCents { get; set; }
    public uint Stock { get; set; }
}
