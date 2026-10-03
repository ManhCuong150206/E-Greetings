using E_Greeting.Models;

namespace E_Greeting.ViewModels.Templates
{
    public class TemplateListViewModel
    {
        public IEnumerable<Template> Templates { get; set; }
       = Enumerable.Empty<Template>();

        public int CurrentPage { get; set; }

        public int PageSize { get; set; }

        public int TotalItems { get; set; }

        public int TotalPages { get; set; }

        public TemplateFilter Filter { get; set; } = new();
    }
}
