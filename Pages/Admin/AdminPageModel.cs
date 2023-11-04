using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sbt.Pages.Admin;

// this is the base class for the Admin pages, created because
// Razor scaffolding creates a lot of duplicate code.
// Also included is a utility method for EST, and a boolean
// to temporarily allow me to quickly enable/disable Admin functions
// when posting to Azure.
public class AdminPageModel : PageModel
{
    protected readonly Sbt.Services.CosmosService _service = default!;

    // for now, this is a quick way to disable Admin Functions on Azure
    public bool DisableSubmitButton = true;

    public string Organization = string.Empty;

    [BindProperty]
    public Sbt.Models.DivisionInfo DivisionInfo { get; set; } = default!;

    public AdminPageModel(Sbt.Services.CosmosService service)
    {
        this._service = service;
    }

    virtual public async Task<IActionResult> OnGetAsync(string organization, string id)
    {
        if (organization == null || this._service == null)
        {
            return NotFound();
        }

        this.Organization = organization;

        // some pages may not have a need for an ID so this is not an error
        if( id == null || id == string.Empty)
        {
            return Page();
        }

        var division = await this._service.GetDivisionInfoIfExists(organization, id);

        if (division == null)
        {
            return NotFound();
        }

        this.DivisionInfo = division;
        return Page();
    }

    protected DateTime GetEasternTime()
    {
        DateTime utcTime = DateTime.UtcNow;

        TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, easternTimeZone);
    }
}
