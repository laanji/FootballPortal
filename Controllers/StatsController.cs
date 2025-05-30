using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatsController : ControllerBase
    {
        private readonly FootballApiService _football;

        public StatsController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("player/{playerId}/{season}")]
        public async Task<IActionResult> GetPlayerStats(int playerId, int season)
        {
            var result = await _football.GetPlayerStatsTextAsync(playerId, season);
            return Ok(result);
        }

        [HttpGet("team/{teamId}/{leagueId}/{season}")]
        public async Task<IActionResult> GetTeamStats(int teamId, int leagueId, int season)
        {
            var result = await _football.GetTeamStatsTextAsync(teamId, leagueId, season);
            return Ok(result);
        }
    }
}