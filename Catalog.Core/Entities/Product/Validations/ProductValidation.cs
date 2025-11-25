using Catalog.Core.Exceptions;

namespace Catalog.Core.Entities.Product.Validations
{
    public static class ProductValidation
    {
        public static void Validate(ulong priceCents, uint stock)
        {
            if (priceCents == 0 && stock > 0)
            {
                throw new InvalidPriceException("PriceCents can be zero only when Stock is also zero.");
            }
        }
    }
}
