
namespace Sbt.Pages.Admin.Divisions;

public class DetailsModel : Sbt.Pages.Admin.AdminPageModel
{
    public DetailsModel(Sbt.Services.CosmosService service) : base(service)
    {
    }

    // Note - using base class version of OnGetAsync()

    // Note - no need for OnPostAsync() for details page
}
