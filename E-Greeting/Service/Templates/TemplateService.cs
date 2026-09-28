using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Greeting.Service.Templates
{
    public class TemplateService(AppDbContext context) : ITemplateService
    {
        public async Task<IEnumerable<Template>> GetTemplates()
        {
            return await context.Templates.Include(c => c.Category).ToListAsync();
        }
    }
}
