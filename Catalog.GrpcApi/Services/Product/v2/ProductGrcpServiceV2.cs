using Catalog.Core;
using Catalog.Core.Entities.Product;
using Catalog.Core.Entities.Product.Validations;
using Catalog.Core.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Catalog.GrpcApi.Services.Product.v2
{
    public class ProductGrcpServiceV2 : ProductServiceV2.ProductServiceV2Base
    {
        private readonly CatalogDbContext _db;

        public ProductGrcpServiceV2(CatalogDbContext db) { _db = db; }

        public override async Task<ProductV2Response> GetProduct(
            GetProductV2Request request,
            ServerCallContext context)
        {
            if(!Guid.TryParse(request.Id, out var publicId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id is required or invalid GUID"));
            
            var product = await _db.Products.FirstOrDefaultAsync(p => p.PublicId == publicId);

            if (product is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Product not found"));
            }

            return Map(product);
        }

        public override async Task<ListProductsV2Response> ListProducts(
            ListProductsV2Request request,
            ServerCallContext context)
        {
            var products = await _db.Products.AsNoTracking().ToListAsync();

            var reply = new ListProductsV2Response();
            reply.Products.AddRange(products.Select(p => new ProductV2Response
            {
                Id = p.PublicId.ToString(),
                Name = p.Name,
                PriceCents = p.PriceCents,
                Stock = p.Stock
            }));

            return reply;
        }

        public override async Task<ProductV2Response> CreateProduct(
            CreateProductV2Request request,
            ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Name is required."));
            if (request.PriceCents < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PriceCents cannot be negative."));
            if (request.Stock < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Stock cannot be negative."));

            var product = new ProductIntegration
            {
                Name = request.Name,
                PriceCents = request.PriceCents,
                Stock = request.Stock
            };

            try
            {
                ProductValidation.Validate(product.PriceCents, product.Stock);
            }
            catch (DomainException ex)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            return Map(product);
        }

        public override async Task<ProductV2Response> UpdateProduct(
            UpdateProductV2Request request,
            ServerCallContext context)
        {
            if (!Guid.TryParse(request.Id, out var publicId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id is required or invalid GUID"));

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Name is required."));

            if (request.PriceCents < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PriceCents cannot be negative."));

            if (request.Stock < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Stock cannot be negative."));

            var product = await _db.Products.FirstOrDefaultAsync(product => product.PublicId.Equals(publicId));

            if (product is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Product with Id {request.Id} not found."));

            product.Name = request.Name;
            product.PriceCents = request.PriceCents;
            product.Stock = request.Stock;

            try
            {
                ProductValidation.Validate(product.PriceCents, product.Stock);
            }
            catch (DomainException ex)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
            }

            await _db.SaveChangesAsync();

            return Map(product);
        }

        public override async Task<Empty> DeleteProduct(
            DeleteProductV2Request request,
            ServerCallContext context)
        {
            if (!Guid.TryParse(request.Id, out var publicId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id is required"));

            var product = await _db.Products.FirstOrDefaultAsync(product => product.PublicId.Equals(publicId));

            if (product is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Product with Id {request.Id} not found."));

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            return new Empty();
        }

        public override async Task<ProductV2Response> PatchProduct(
            PatchProductV2Request request,
            ServerCallContext context)
        {
            if (!Guid.TryParse(request.Id, out var publicId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id is required"));

            var product = await _db.Products.FirstOrDefaultAsync(product => product.PublicId.Equals(publicId));

            if (product is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Product with Id {request.Id} not found."));

            if (request.UpdateMask == null || request.UpdateMask.Paths.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UpdateMask must contain at least one field."));

            foreach (var path in request.UpdateMask.Paths)
            {
                switch (path)
                {
                    case "name" when !string.IsNullOrWhiteSpace(request.Name):
                            product.Name = request.Name;
                        break;
                    case "price_cents":
                            product.PriceCents = request.PriceCents;
                        break;
                    case "stock":
                        product.Stock = request.Stock;
                        break;
                    default:
                        throw new RpcException(new Status(
                            StatusCode.InvalidArgument,
                            $"Field '{path}' is not allowed in update_mask."));
                }
            }

            try
            {
                ProductValidation.Validate(product.PriceCents, product.Stock);
            }
            catch (DomainException ex)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
            }

            await _db.SaveChangesAsync();

            return Map(product);
        }

        private static ProductV2Response Map(ProductIntegration product)
        {
            return new ProductV2Response
            {
                Id = product.PublicId.ToString(),
                Name = product.Name,
                PriceCents = product.PriceCents,
                Stock = product.Stock
            };
        }
    }
}
