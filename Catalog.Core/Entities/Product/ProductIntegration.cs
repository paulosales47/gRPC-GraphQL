namespace Catalog.Core.Entities.Product;

public class ProductIntegration
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public ulong PriceCents { get; set; }
    public uint Stock { get; set; }
}
