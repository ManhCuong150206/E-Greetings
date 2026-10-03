using E_Greeting.Models;
using E_Greeting.ViewModels.Templates;

namespace E_Greeting.Service.Templates
{
    public interface ITemplateService
    {
       Task<TemplateListViewModel> GetTemplates(TemplateFilter filter);
       Task<Template?> GetTemplateById(int id);
    }
}
