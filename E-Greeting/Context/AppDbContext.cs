using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace E_Greeting.Context
{
    public class AppDbContext :DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { 
            
        }
        
        public DbSet<Template> Template { get; set; }
        public DbSet<Category> Category { get; set; }

    }
}
