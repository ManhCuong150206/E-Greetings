using E_Greeting.Models;

namespace E_Greeting.Service.Templates
{
    public interface ITemplateService
    {
       Task<IEnumerable<Template>> GetTemplates();
    }
}
