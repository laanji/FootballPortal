using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StandingsController : ControllerBase
    {
        private readonly FootballApiService _football;

        public StandingsController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("{leagueId}")]
        public async Task<IActionResult> GetStandings(int leagueId)
        {
            var standings = await _football.GetStandingsAsync(leagueId);
            if (standings.Count == 0)
                return NotFound("Турнірна таблиця не знайдена.");

            return Ok(standings);
        }
    }
}
