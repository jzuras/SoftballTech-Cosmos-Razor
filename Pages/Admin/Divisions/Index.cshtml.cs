using Microsoft.AspNetCore.Mvc;

namespace Sbt.Pages.Admin.Divisions;

public class IndexModel : Sbt.Pages.Admin.AdminPageModel
{
    public IndexModel(Sbt.Services.CosmosService service) : base(service)
    {
    }

    public IList<Sbt.Models.DivisionInfo> DivisionsList { get; set; } = default!;

    override public async Task<IActionResult> OnGetAsync(string organization, string id = "")
    {
        await base.OnGetAsync(organization, id);

        this.DivisionsList = await base._service.GetDivisionList(organization);

        return Page();
    }
}
