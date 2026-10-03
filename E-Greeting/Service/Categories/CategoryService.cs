using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Greeting.Service.Categories
{
    public class CategoryService(AppDbContext context) : ICategoryService
    {
        public async Task<IEnumerable<Category>> GetCategories()
        {
            return await context.Categories.AsNoTracking()
            .OrderBy(c => c.Name).ToListAsync();
        }
    }
}
