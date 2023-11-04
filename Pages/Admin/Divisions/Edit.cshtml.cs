using Microsoft.AspNetCore.Mvc;

namespace Sbt.Pages.Admin.Divisions;

public class EditModel : Sbt.Pages.Admin.AdminPageModel
{
    public EditModel(Sbt.Services.CosmosService service) : base(service)
    {
    }

    // Note - using base class version of OnGetAsync()

    public async Task<IActionResult> OnPostAsync(string organization, string id)
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

        base.Organization = base.DivisionInfo.Organization = organization;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var delMe = base.DivisionInfo;

        var divisionToUpdate = await this._service.GetDivisionInfoIfExists(organization, base.DivisionInfo.ID);
        if (divisionToUpdate == null)
        {
            return Page();
        }

        // overposting is not an issue for DivisionInfo class
        base.DivisionInfo.Updated = base.GetEasternTime();
        await base._service.SaveDivisionInfo(base.DivisionInfo);

        return RedirectToPage("./Index", new { organization = organization });
    }
}
