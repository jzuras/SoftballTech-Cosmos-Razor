using Microsoft.AspNetCore.Mvc.RazorPages;
using Sbt.Models;

namespace Sbt.Pages.Demo;

public class IndexModel : PageModel
{
    private readonly Sbt.Services.CosmosService _service;

    public string Organization { get; private set; } = string.Empty;

    public IList<DivisionInfo> DivisionsList { get; set; } = default!;

    public IndexModel(Sbt.Services.CosmosService service)
    {
        this._service = service;
    }

    public async Task OnGetAsync(string organization)
    {
        if (organization == null)
        {
            this.Organization = "[Missing Organization]";
        }
        else
        {
            this.Organization = organization;
            this.DivisionsList = await this._service.GetDivisionList(organization);
        }
    }
}
