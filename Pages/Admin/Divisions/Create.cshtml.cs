using Microsoft.AspNetCore.Mvc;
using Sbt.Models;

namespace Sbt.Pages.Admin.Divisions;
public class CreateModel : Sbt.Pages.Admin.AdminPageModel
{
    public CreateModel(Sbt.Services.CosmosService service) : base(service)
    {
    }

    // Note - using base class version of OnGetAsync()

    public async Task<IActionResult> OnPostAsync(string organization)
    {
        // submit button should be disbled if true, but protect against other entries
        if (base.DisableSubmitButton == true)
        {
            return Page();
        }

        if (organization == null)
        {
            return Page();
        }

        var delMe = base.DivisionInfo;

        base.Organization = base.DivisionInfo.Organization = organization;

        if (!ModelState.IsValid || base._service == null)
        {
            return Page();
        }

        // need to make sure new division ID is unique
        var division = await this._service.GetDivisionInfoIfExists(organization, base.DivisionInfo.ID);
        if (division != null)
        {
            ModelState.AddModelError(string.Empty, "This Division ID already exists.");
            return Page();
        }

        // overposting is not an issue for DivisionInfo class
        base.DivisionInfo.Updated = base.GetEasternTime();
        await base._service.SaveDivisionInfo(base.DivisionInfo);

        return RedirectToPage("./Index", new { organization = organization });
    }
}
