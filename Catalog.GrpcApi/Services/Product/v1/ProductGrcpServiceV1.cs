using Catalog.Core;
using Catalog.Core.Entities.Product;
using Catalog.Core.Entities.Product.Validations;
using Catalog.Core.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Catalog.GrpcApi.Services.Product.v1
{
    public class ProductGrcpServiceV1 : ProductServiceV1.ProductServiceV1Base
    {
        private readonly CatalogDbContext _db;

        public ProductGrcpServiceV1(CatalogDbContext db) { _db = db; }

        public override async Task<ProductResponse> GetProduct(
            GetProductRequest request,
            ServerCallContext context)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id);

            if (product is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Product not found"));
            }

            return Map(product);
        }

        public override async Task<ListProductsResponse> ListProducts(
            ListProductsRequest request,
            ServerCallContext context)
        {
            var products = await _db.Products.ToListAsync();

            var reply = new ListProductsResponse();
            reply.Products.AddRange(products.Select(p => new ProductResponse
            {
                Id = p.Id,
                Name = p.Name,
                PriceCents = p.PriceCents,
                Stock = p.Stock
            }));

            return reply;
        }

        public override async Task<ProductResponse> CreateProduct(
            CreateProductRequest request,
            ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Name is required"));
            if (request.PriceCents == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PriceCents is required to be greater than 0"));
            if (request.Stock <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Stock is required to be greater than 0"));

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

        public override async Task<ProductResponse> UpdateProduct(
            UpdateProductRequest request,
            ServerCallContext context)
        {
            if (request.Id <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be greater than 0."));

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Name is required."));

            if (request.PriceCents == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PriceCents must be greater than 0."));


            var product = await _db.Products.FindAsync(request.Id);

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
            DeleteProductRequest request,
            ServerCallContext context)
        {
            if (request.Id <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be greater than 0."));

            var product = await _db.Products.FindAsync(request.Id);

            if (product is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Product with Id {request.Id} not found."));

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            return new Empty();
        }

        public override async Task<ProductResponse> PatchProduct(
            PatchProductRequest request,
            ServerCallContext context)
        {
            if (request.Id <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be greater than 0."));

            var product = await _db.Products.FindAsync(request.Id);

            if (product is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Product with Id {request.Id} not found."));

            if (request.UpdateMask == null || request.UpdateMask.Paths.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UpdateMask must contain at least one field."));

            foreach (var path in request.UpdateMask.Paths)
            {
                switch (path)
                {
                    case "name":
                        if (!string.IsNullOrWhiteSpace(request.Name))
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

        private static ProductResponse Map(ProductIntegration product)
        {
            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                PriceCents = product.PriceCents,
                Stock = product.Stock
            };
        }
    }
}
