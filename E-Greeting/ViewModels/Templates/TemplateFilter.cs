namespace E_Greeting.ViewModels.Templates
{
    public class TemplateFilter
    {
        public int? CategoryId { get; set; }

        public bool? IsFree { get; set; }

        public string? Keyword { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 2;
    }
}
