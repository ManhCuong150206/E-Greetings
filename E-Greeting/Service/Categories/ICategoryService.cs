using E_Greeting.Models;

namespace E_Greeting.Service.Categories
{
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetCategories();
    }
}
