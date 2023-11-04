
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Sbt.Models;
using System.Net;

namespace Sbt.Services;

public class CosmosService
{
    public record LoadScheduleResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime FirstGameDate { get; init; }
        public DateTime LastGameDate { get; init; }
    };

    private readonly string _databaseName = "Sbt";
    private readonly string _containerName = "organizations";
    private readonly string _partitionKeyPath = "/Organization";
    private bool _isInitialized = false;

    /// <summary>
    /// Note - do NOT access directly - access via _container to ensure initialization
    /// </summary>
    private Container _cosmosContainer = default!;

    private readonly CosmosClient _client = default!;

    private readonly string _divisionListID = "DivisionListID";

    private Container _container
    {
        get
        {
            InitializeContainer();
            return this._cosmosContainer;
        }
    }

    public CosmosService(IConfiguration config)
    {
        string connString = config.GetConnectionString("Cosmos_ConnectionString")
                         ?? throw new InvalidOperationException("Cosmos ConnectionString is null");

        this._client = new CosmosClient(connectionString: connString);
    }

    #region DAL methods
    public async Task<Division> GetDivision(string organization, string divisionID)
    {
        try
        {
            // point read for quickest access
            ItemResponse<Division> itemResponse =
                await this._container.ReadItemAsync<Division>(divisionID.ToLower(), new PartitionKey(organization));
            var division = itemResponse.Resource;

            return division;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No division document
            return new Division();
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }
    }

    public async Task<DivisionInfo> GetDivisionInfoIfExists(string organization, string divisionID)
    {
        try
        {
            var queryable = this._container.GetItemLinqQueryable<DivisionInfoList>();
            using FeedIterator<DivisionInfoList> feedIterator = queryable
                .Where(d => d.Organization == organization && d.ID == this._divisionListID)
                .ToFeedIterator();
            {
                while (feedIterator.HasMoreResults)
                {
                    var response = await feedIterator.ReadNextAsync();
                    var infoList = response.FirstOrDefault();
                    if (infoList == null || infoList.DivisionList == null) return null!;
                    foreach (var item in infoList.DivisionList)
                    {
                        if(item.ID.ToLower() == divisionID.ToLower())
                            return item;
                    }
                }
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Division not found
            return null!;
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }
        return null!;
    }

    public async Task<List<DivisionInfo>> GetDivisionList(string organization)
    {
        try
        {
            var queryable = this._container.GetItemLinqQueryable<DivisionInfoList>();
            using FeedIterator<DivisionInfoList> feedIterator = queryable
                .Where(d => d.Organization == organization && d.ID == this._divisionListID)
                .ToFeedIterator();
            {
                while (feedIterator.HasMoreResults)
                {
                    var response = await feedIterator.ReadNextAsync();
                    var only = response.FirstOrDefault();
                    if (only != null)
                        return only.DivisionList;
                }
                return new List<DivisionInfo>();
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No divisions - return empty list
            return new List<DivisionInfo>();
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }
    }

    public async Task<List<Schedule>> GetGames(string organization, string divisionID, int gameID)
    {
        List<Schedule> list = new List<Schedule>();
        DateTime day;
        string field;

        try
        {
            // Step 1: do a query returning 1 game result based on the game id
            // Step 2: do a second query using that game's day and field

            // using sql here just to show it can be done for Cosmos
            // Note - "select value s" returns just the Schedule part of the entire document
            var sqlQueryText =
                "SELECT VALUE s FROM c JOIN s IN c.Schedule WHERE " +
                $"c.Organization = '{organization}' AND c.id = '{divisionID.ToLower()}' AND s.GameID = {gameID}";

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            var queryableSQL = this._container.GetItemQueryIterator<Schedule>(queryDefinition);
            var result = await queryableSQL.ReadNextAsync();

            if (result.Count > 0)
            {
                var gameInfo = result.First();
                day = gameInfo.Day!.Value;
                field = gameInfo.Field;
            }
            else
            {
                // game id not found (this should not happen)
                return new List<Schedule>();
            }

            var queryable = this._container.GetItemLinqQueryable<Division>();
            using FeedIterator<Schedule> feedIterator = queryable
                .Where(d => d.Organization == organization && d.ID == divisionID.ToLower())
                .SelectMany(d => d.Schedule)
                .Where(s => s.Day == day && s.Field == field)
                .ToFeedIterator();
            {
                while (feedIterator.HasMoreResults)
                {
                    var response = await feedIterator.ReadNextAsync();
                    foreach (var game in response)
                    {
                        list.Add(game);
                    }
                } // while has more results
            } // using
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No division document
            return null!;
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }

        return list;
    }

    public async Task<LoadScheduleResult> LoadScheduleFileAsync(IFormFile scheduleFile, string organization, string divisionID,
        bool usesDoubleHeaders)
    {
        bool docuumentExists = true;
        string errorMessage = string.Empty;
        DateTime firstGameDate = DateTime.MinValue;
        DateTime lastGameDate = DateTime.MinValue;
        int gameID = 0; // NOTE - Game IDs are unique within a Division (document) for Cosmos
        List<string> lines = new();
        Division division = new();
        var standings = new List<Standings>();
        var schedule = new List<Schedule>();

        var divisionInfo = await this.GetDivisionInfoIfExists(organization, divisionID);
        if (divisionInfo == null)
        {
            return new LoadScheduleResult
            {
                Success = false,
                ErrorMessage = "Division Does Not Exist",
                FirstGameDate = firstGameDate,
                LastGameDate = lastGameDate
            };
        }

        // we know division exists in InfoList but may not yet have its own document yet
        try
        {
            // point read for quickest access
            ItemResponse<Division> itemResponse =
                await this._container.ReadItemAsync<Division>(divisionID.ToLower(), new PartitionKey(organization));
            division = itemResponse.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // this is okay, document for division doesn't exists so create it
            division = new Division();
            division.Organization = organization;
            division.ID = divisionID.ToLower();
            docuumentExists = false;
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }

        using (var reader = new StreamReader(scheduleFile.OpenReadStream()))
        {
            while (reader.Peek() >= 0)
                lines.Add(reader.ReadLine()!);
        }

        try
        {
            // Note - expecting a properly formatted file since it is self-created,
            // solely for the purposes of populating some demo data for the website.
            // therefore no error-checking is done here - just wrapping in try-catch
            // and returning exceptions to the calling method

            List<string> teams = new();
            int lineNumber = 0;
            short teamID = 1;

            // skip first 4 lines which are simply for ease of reading the file
            lineNumber = 4;

            // next lines are teams - ended by blank line
            // team IDs are assumed, starting at 1
            while (lines[lineNumber].Length > 0)
            {
                teams.Add(lines[lineNumber].Trim());

                // create standings row for each team
                var standingsRow = new Standings
                {
                    Wins = 0,
                    Losses = 0,
                    Ties = 0,
                    OvertimeLosses = 0,
                    Percentage = 0,
                    GB = 0,
                    RunsAgainst = 0,
                    RunsScored = 0,
                    Forfeits = 0,
                    ForfeitsCharged = 0,
                    Name = lines[lineNumber].Trim(),
                    TeamID = teamID++
                };
                standings.Add(standingsRow);
                lineNumber++;
            }

            // rest of file is the actual schedule, in this format:
            // Date,Day,Time,Home,Visitor,Field
            for (int index = lineNumber + 1; index < lines.Count; index++)
            {
                string[] data = lines[index].Split(',');

                if (data[0].ToLower().StartsWith("week"))
                {
                    // original code had complicated method to determine week boundaries,
                    // but for simplicity's sake I am adding this info in the schedule files
                    schedule.Add(this.AddWeekBoundary(data[0], gameID));
                    gameID++;
                    continue;
                }
                DateTime gameDate = DateTime.Parse(data[0]);
                // skipping value at [1] - not currently used in this version of the website
                DateTime gameTime = DateTime.Parse(data[2]);
                short homeTeamID = short.Parse(data[3]);
                short visitorTeamID = short.Parse(data[4]);
                string field = data[5];

                // create schedule row for each game
                var scheduleRow = new Schedule
                {
                    GameID = gameID++,
                    Day = gameDate,
                    Field = field,
                    Home = teams[homeTeamID - 1],
                    HomeForfeit = false,
                    HomeID = homeTeamID,
                    Time = gameTime,
                    Visitor = teams[visitorTeamID - 1],
                    VisitorForfeit = false,
                    VisitorID = visitorTeamID,
                };
                schedule.Add(scheduleRow);

                if (usesDoubleHeaders)
                {
                    // add a second game 90 minutes later, swapping home/visitor
                    scheduleRow = new Schedule
                    {
                        GameID = gameID++,
                        Day = gameDate,
                        Field = field,
                        Home = teams[visitorTeamID - 1],
                        HomeForfeit = false,
                        HomeID = visitorTeamID,
                        Time = gameTime.AddMinutes(90),
                        Visitor = teams[homeTeamID - 1],
                        VisitorForfeit = false,
                        VisitorID = homeTeamID,
                    };
                    schedule.Add(scheduleRow);
                }

                // keep track of first and last games to show when done processing file,
                // as a way to show user that the entire schedule was processed.
                if (index == lineNumber + 2)
                {
                    firstGameDate = gameDate;
                }
                else if (index == lines.Count - 1)
                {
                    lastGameDate = gameDate;
                }
            } // for loop processing schedule data

            division.Schedule = schedule;
            division.Standings = standings;

            if (docuumentExists)
            {
                // replace the document with the updated content
                await this._container.ReplaceItemAsync<Division>(division, division.ID.ToLower(), new PartitionKey(organization));
            }
            else
            {
                await this._container.CreateItemAsync<Division>(division, new PartitionKey(organization));
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null)
            {
                errorMessage = ex.Message + ":<br>" + ex.InnerException.Message;
            }
            else
            {
                errorMessage = ex.Message;
            }
        }

        return new LoadScheduleResult
        {
            Success = (errorMessage == string.Empty) ? true : false,
            ErrorMessage = errorMessage,
            FirstGameDate = firstGameDate,
            LastGameDate = lastGameDate
        };
    }

    public async Task SaveDivisionInfo(DivisionInfo info, bool deleteDivision = false)
    {
        DivisionInfoList list = new();
        string organization = info.Organization;
        bool docuumentExists = false;
        int divisionIndex = -1;

        // first, see if divisionInfo list document already exists
        try
        {
            // point read for quickest access
            ItemResponse<DivisionInfoList> itemResponse =
                await this._container.ReadItemAsync<DivisionInfoList>(this._divisionListID, new PartitionKey(organization));
            list = itemResponse.Resource;
            docuumentExists = true;
            for (int i = 0; i < list.DivisionList.Count; i++)
            {
                if (list.DivisionList[i].ID == info.ID)
                {
                    divisionIndex = i;
                    break;
                }
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // divisionInfo list document not found
            docuumentExists = false;
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }

        if (divisionIndex >= 0)
        {
            // replace pre-existing division info
            list.DivisionList.RemoveAt(divisionIndex);
            if(deleteDivision == false)
                list.DivisionList.Add(info);
        }
        else if (docuumentExists)
        {
            // document found but division not in list, so add it here

            // Note - we shouldn't be able to delete a division
            // that's not in the list, but checking flag just in case
            if(deleteDivision == false)
                list.DivisionList.Add(info);
        }
        else
        {
            // need to create new document for division info list
            list = new DivisionInfoList();
            list.Organization = organization;
            list.ID = this._divisionListID;
            list.DivisionList = new List<DivisionInfo>();

            // Note - we shouldn't be able to delete a division
            // when the division info list doesn't exist, but checking flag just in case
            if (deleteDivision == false)
                list.DivisionList.Add(info);
        }

        try
        {
            if (docuumentExists)
            {
                if (deleteDivision == true)
                {
                    // division document may not exist, so catch error and ignore it
                    try
                    {
                        await this._container.DeleteItemAsync<Division>(info.ID.ToLower(), new PartitionKey(organization));
                    }
                    catch(Exception)
                    {
                        // nothing to do here
                    }
                }
                await this._container.ReplaceItemAsync<DivisionInfoList>(list, this._divisionListID, new PartitionKey(organization));
            }
            else
            {
                await this._container.CreateItemAsync<DivisionInfoList>(list, new PartitionKey(organization));
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }
    }

    public async Task SaveScores(string organization, string divisionID, IList<ScheduleVM> schedules)
    {
        // get entire document - we need to update 1 or 2 Schedule items and then replace the entire document
        var division = await this.GetDivision(organization, divisionID);
        var divisionInfo = await this.GetDivisionInfoIfExists(organization, divisionID);

        for (int i = 0; i < schedules.Count; i++)
        {
            // find matching game id
            for (int j = 0; j < division.Schedule.Count; j++)
            {
                if (schedules[i].GameID == division.Schedule[j].GameID)
                {
                    // populate Model from ViewModel (which is used to prevent overposting)
                    division.Schedule[j].HomeForfeit = schedules[i].HomeForfeit;
                    division.Schedule[j].HomeScore = schedules[i].HomeScore;
                    division.Schedule[j].VisitorForfeit = schedules[i].VisitorForfeit;
                    division.Schedule[j].VisitorScore = schedules[i].VisitorScore;

                    // force forfeit scores in case they came in wrong
                    if (division.Schedule[j].VisitorForfeit)
                    {
                        division.Schedule[j].VisitorScore = 0;
                        division.Schedule[j].HomeScore = (division.Schedule[j].HomeForfeit) ? (short)0 : (short)7;
                    }
                    else if (division.Schedule[j].HomeForfeit)
                    {
                        division.Schedule[j].VisitorScore = 7;
                        division.Schedule[j].HomeScore = 0;
                    }
                    break;
                }
            }
        } // for each game in list

        this.ReCalcStandings(division);

        // division updated time changes when scores are reported
        divisionInfo.Updated = this.GetEasternTime();
        await this.SaveDivisionInfo(divisionInfo);

        try
        {
            // update the division document
            await this._container.ReplaceItemAsync<Division>(division, division.ID.ToLower(), new PartitionKey(organization));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No division document
            throw new Exception("Unable to update Division - document not found. Exception message: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected Exception: " + ex.Message);
        }
    }
    #endregion

    #region Helper Methods
    private void ReCalcStandings(Division division)
    {
        var standings = division.Standings;

        var schedule = division.Schedule;

        // zero-out standings
        foreach (var stand in standings)
        {
            stand.Forfeits = stand.Losses = stand.OvertimeLosses = stand.Ties = stand.Wins = 0;
            stand.RunsAgainst = stand.RunsScored = stand.ForfeitsCharged = 0;
            stand.GB = stand.Percentage = 0;
        }

        foreach (var sched in schedule)
        {
            // skip week boundary
            if (sched.Visitor.ToUpper().StartsWith("WEEK") == true) continue;

            this.UpdateStandings(standings, sched);
        }
    }

    private void UpdateStandings(List<Standings> standings, Schedule sched)
    {
        // note - IList starts at 0, team IDs start at 1
        var homeTteam = standings[sched.HomeID - 1];
        var visitorTeam = standings[sched.VisitorID - 1];

        if (sched.HomeScore > -1) // this will catch null values (no scores reported yet)
        {
            homeTteam.RunsScored += (short)sched.HomeScore!;
            homeTteam.RunsAgainst += (short)sched.VisitorScore!;
            visitorTeam.RunsScored += (short)sched.VisitorScore!;
            visitorTeam.RunsAgainst += (short)sched.HomeScore!;
        }

        if (sched.HomeForfeit)
        {
            homeTteam.Forfeits++;
            homeTteam.ForfeitsCharged++;
        }
        if (sched.VisitorForfeit)
        {
            visitorTeam.Forfeits++;
            visitorTeam.ForfeitsCharged++;
        }

        if (sched.VisitorForfeit && sched.HomeForfeit)
        {
            // special case - not a tie - counted as losses for both team
            homeTteam.Losses++;
            visitorTeam.Losses++;
        }
        else if (sched.HomeScore > sched.VisitorScore)
        {
            homeTteam.Wins++;
            visitorTeam.Losses++;
        }
        else if (sched.HomeScore < sched.VisitorScore)
        {
            homeTteam.Losses++;
            visitorTeam.Wins++;
        }
        else if (sched.HomeScore > -1) // this will catch null values (no scores reported yet)
        {
            homeTteam.Ties++;
            visitorTeam.Ties++;
        }

        // calculate Games Behind (GB)
        var sortedTeams = standings.OrderByDescending(t => t.Wins).ToList();
        var maxWins = sortedTeams.First().Wins;
        var maxLosses = sortedTeams.First().Losses;
        foreach (var team in sortedTeams)
        {
            team.GB = ((maxWins - team.Wins) + (team.Losses - maxLosses)) / 2.0f;
            if ((team.Wins + team.Losses) == 0)
            {
                team.Percentage = 0.0f;
            }
            else
            {
                team.Percentage = (float)team.Wins / (team.Wins + team.Losses + team.Ties);
            }
        }
    }

    private DateTime GetEasternTime()
    {
        DateTime utcTime = DateTime.UtcNow;

        TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, easternTimeZone);
    }

    private Schedule AddWeekBoundary(string week, int maxGameID)
    {
        // this creates a mostly empty "WEEK #" row to make it easier to show
        // week boundaries when displaying the schedule.
        var scheduleRow = new Schedule
        {
            GameID = maxGameID,
            HomeForfeit = false,
            Visitor = week,
            VisitorForfeit = false,
        };

        return scheduleRow;
    }

    private void InitializeContainer()
    {
        if (this._isInitialized == false)
        {
            var databaseResponse = this._client.CreateDatabaseIfNotExistsAsync(this._databaseName).Result;

            var containerResponse =
                databaseResponse.Database.DefineContainer(this._containerName, this._partitionKeyPath)
                    .WithIndexingPolicy()
                        .WithIncludedPaths()
                            .Path("/*")
                        .Attach()
                        .WithExcludedPaths()
                            .Path("/\"_etag\"/*")
                            .Path("/ID/*")
                        .Attach()
                        .WithCompositeIndex()
                            .Path(this._partitionKeyPath, CompositePathSortOrder.Ascending)
                            .Path("/ID", CompositePathSortOrder.Descending)
                        .Attach()
                    .Attach()
                    .CreateIfNotExistsAsync().Result;

            this._cosmosContainer = containerResponse.Container;

            this._isInitialized = true;
        }
    }
    #endregion
}
