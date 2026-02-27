using lab4.Models;
using Lab4.Models;

namespace Lab4.Services
{
    public interface ICartService
    {
        Task<Cart> GetOrCreateCartAsync();

        Task AddToCartAsync(int productId, int quantity);

        Task ClearCartAsync();
    }
}