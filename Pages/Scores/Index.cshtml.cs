using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sbt.Pages.Scores;

public class IndexModel : PageModel
{
    private readonly Sbt.Services.CosmosService _service;

    [BindProperty]
    public string Organization { get; set; } = default!;
    
    [BindProperty]
    public string DivisionID { get; set; } = default!;

    [BindProperty]
    public IList<Sbt.Models.Schedule> Schedule { get; set; } = default!;

    [BindProperty]
    public IList<Sbt.Models.ScheduleVM> ScheduleVM { get; set; } = default!;

    private bool ShowOvertimeLosses { get; set; } = false;

    public IndexModel(Sbt.Services.CosmosService service)
    {
        this._service = service;
    }

    public async Task<IActionResult> OnGetAsync(string organization, string divisionID)
    {
        if (this._service != null && organization != null && divisionID != null)
        {
            this.Organization = organization;
            this.DivisionID = divisionID;
            int gameID = 0;

            if (Request.Query.TryGetValue("gameID", out var gameIDString) == false ||
                int.TryParse(gameIDString, out gameID) == false)
            {
                return NotFound();
            }

            var gameInfo = await this._service.GetGames(organization, divisionID, gameID);

            if (gameInfo == null)
            {
                return NotFound();
            }

            this.Schedule = gameInfo;

            // populate ViewModel (which is used to prevent overposting)
            this.ScheduleVM = new List<Sbt.Models.ScheduleVM>();
            for (int i = 0; i < this.Schedule.Count; i++)
            {
                var scheduleVM = new Sbt.Models.ScheduleVM();
                scheduleVM.GameID = this.Schedule[i].GameID;
                scheduleVM.HomeScore = this.Schedule[i].HomeScore;
                scheduleVM.VisitorScore = this.Schedule[i].VisitorScore;
                scheduleVM.HomeForfeit = this.Schedule[i].HomeForfeit;
                scheduleVM.VisitorForfeit = this.Schedule[i].VisitorForfeit;
                this.ScheduleVM.Add(scheduleVM);
            }

            this.DetermineOvertimeLossVisibility();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string organization, string divisionID)
    {
        if (!ModelState.IsValid || this._service == null)
        {
            return Page();
        }

        await this._service.SaveScores(organization, divisionID, this.ScheduleVM);

        return RedirectToPage("/Standings/Index", 
            new { organization = this.Organization, id = this.DivisionID });
    }

    private void DetermineOvertimeLossVisibility()
    {
        // in a production system this would be handled more generically,
        // but for now we are just checking if Org contains "Hockey"
        this.ShowOvertimeLosses = this.Organization.ToLower().Contains("hockey");
    }
}
