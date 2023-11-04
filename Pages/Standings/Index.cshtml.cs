using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sbt.Models;

namespace Sbt.Pages.Standings;

public class IndexModel : PageModel
{
    private readonly Sbt.Services.CosmosService _service;

    public IList<Sbt.Models.Standings> Standings { get; set; } = default!;

    public IList<Sbt.Models.Schedule> Schedule { get; set; } = default!;
    
    public Sbt.Models.Division Division { get; set; } = default!;

    public Sbt.Models.DivisionInfo DivisionInfo { get; set; } = default!;

    public bool ShowOvertimeLosses { get; set; } = false;

    [BindProperty]
    public string? TeamName { get; set; }

    public IndexModel(Sbt.Services.CosmosService service)
    {
        this._service = service;
    }

    public async Task<IActionResult> OnGetAsync(string organization, string id)
    {
        if (this._service != null && organization != null && id != null)
        {
            this.DivisionInfo = await this._service.GetDivisionInfoIfExists(organization, id);
            this.Division = await this._service.GetDivision(organization, id);

            if (this.Division == null || this.Division.Organization == string.Empty)
            {
                return NotFound();
            }

            this.Schedule = this.Division.Schedule.ToList();
            this.Standings = this.Division.Standings.ToList();

            if (Request.Query.TryGetValue("teamName", out var teamName))
            {
                this.TeamName = teamName;
                this.Schedule = this.Schedule
                    .Where<Schedule>(s => s.Home == teamName || s.Visitor == teamName).ToList();
            }

            this.Standings = this.Standings
                .OrderBy(s => s.GB).ThenByDescending(s => s.Percentage)
                .ToList();

            this.DetermineOvertimeLossVisibility();
        }
        return Page();
    }

    private void DetermineOvertimeLossVisibility()
    {
        // in a production system this would be handled more generically,
        // but for now we are just checking if Org contains "Hockey"
        this.ShowOvertimeLosses = this.Division.Organization.ToLower().Contains("hockey");
    }
}
