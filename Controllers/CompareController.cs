using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompareController : ControllerBase
    {
        private readonly FootballApiService _football;

        public CompareController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("players/{id1}/{id2}/{season}")]
        public async Task<IActionResult> ComparePlayers(int id1, int id2, int season)
        {
            var result = await _football.ComparePlayersAsync(id1, id2, season);
            return Ok(result);
        }

        [HttpGet("teams/{id1}/{id2}/{leagueId}/{season}")]
        public async Task<IActionResult> CompareTeams(int id1, int id2, int leagueId, int season)
        {
            var result = await _football.CompareTeamsAsync(id1, id2, leagueId, season);
            return Ok(result);
        }
    }
}