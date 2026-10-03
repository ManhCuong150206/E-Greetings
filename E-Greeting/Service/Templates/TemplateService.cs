using E_Greeting.Models;
using E_Greeting.ViewModels.Templates;
using Microsoft.EntityFrameworkCore;

namespace E_Greeting.Service.Templates
{
    public class TemplateService(AppDbContext context) : ITemplateService
    {
        public async Task<TemplateListViewModel> GetTemplates(
      TemplateFilter filter)
        {
            var query = context.Templates
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.IsActive);

            // 1. SEARCH
            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                query = await ApplySearch(query, filter.Keyword);
            }

            // 2. CATEGORY
            if (filter.CategoryId.HasValue)
            {
                query = query.Where(t =>
                    t.CategoryId == filter.CategoryId.Value);
            }

            // 3. FREE / PREMIUM
            if (filter.IsFree.HasValue)
            {
                query = query.Where(t =>
                    t.IsFree == filter.IsFree.Value);
            }

            // 4. COUNT
            var totalItems = await query.CountAsync();

            // 5. PAGINATION
            var templates = await query
                .OrderByDescending(t => t.Id)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(
                totalItems / (double)filter.PageSize
            );

            return new TemplateListViewModel
            {
                Templates = templates,
                CurrentPage = filter.Page,
                PageSize = filter.PageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Filter = filter
            };
        }
        public async Task<Template?> GetTemplateById(int id)
        {
            return await context.Templates
                .AsNoTracking()
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t =>
                    t.Id == id &&
                    t.IsActive);
        }
        //return await context.Templates.Include(c => c.Category).ToListAsync();

        private async Task<IQueryable<Template>> ApplySearch(
    IQueryable<Template> query,
    string keyword)
        {
            keyword = keyword.Trim();

            // =========================
            // LEVEL 1
            // SEARCH NGUYÊN CÂU
            // =========================

            var exactQuery = query.Where(t =>
                t.Name.Contains(keyword));

            if (await exactQuery.AnyAsync())
            {
                return exactQuery;
            }

            // =========================
            // LEVEL 2
            // TÁCH TỪ
            // =========================

            var words = keyword
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            if (words.Length > 0)
            {
                var wordQuery = query.Where(t =>
                    words.Any(word =>
                        t.Name.Contains(word)));

                if (await wordQuery.AnyAsync())
                {
                    return wordQuery;
                }
            }

            // =========================
            // LEVEL 3
            // BIGRAM
            // =========================

            var bigrams = GenerateBigrams(words);

            if (bigrams.Count > 0)
            {
                var bigramQuery = query.Where(t =>
                    bigrams.Any(bigram =>
                        t.Name.Contains(bigram)));

                return bigramQuery;
            }

            return query;
        }

        private List<string> GenerateBigrams(
    IEnumerable<string> words)
        {
            var bigrams = new List<string>();

            foreach (var word in words)
            {
                if (word.Length < 2)
                    continue;

                for (int i = 0; i < word.Length - 1; i++)
                {
                    bigrams.Add(
                        word.Substring(i, 2)
                    );
                }
            }

            return bigrams
                .Distinct()
                .ToList();
        }
    }
}
