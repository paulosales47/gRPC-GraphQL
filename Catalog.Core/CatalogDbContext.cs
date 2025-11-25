using Catalog.Core.Entities.Product;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Core;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductIntegration> Products => Set<ProductIntegration>();
}
