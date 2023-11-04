using Microsoft.AspNetCore.Mvc;

namespace Sbt.Pages.Admin.Divisions;

public class DeleteModel : Sbt.Pages.Admin.AdminPageModel
{
    public DeleteModel(Sbt.Services.CosmosService service) : base(service)
    {
    }

    // Note - using base class version of OnGetAsync()

    public async Task<IActionResult> OnPostAsync()
    {
        // submit button should be disbled if true, but protect against other entries
        if (base.DisableSubmitButton == true)
        {
            return Page();
        }

        if (base._service == null)
        {
            return NotFound();
        }

        // overposting is not an issue for DivisionInfo class
        await base._service.SaveDivisionInfo(base.DivisionInfo, deleteDivision: true);

        return RedirectToPage("./Index", new { organization = base.DivisionInfo.Organization });
    }
}
