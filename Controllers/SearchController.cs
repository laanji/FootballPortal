using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly FootballApiService _football;

        public SearchController(FootballApiService football)
        {
            _football = football;
        }

        // Пошук команд за назвою
        [HttpGet("team/{name}")]
        public async Task<IActionResult> SearchTeam(string name)
        {
            var results = await _football.SearchTeamAsync(name);
            return results.Any() ? Ok(results) : NotFound("Команду не знайдено.");
        }

        // Пошук ліг за назвою
        [HttpGet("league/{name}")]
        public async Task<IActionResult> SearchLeague(string name)
        {
            var results = await _football.SearchLeagueAsync(name);
            return results.Any() ? Ok(results) : NotFound("Лігу не знайдено.");
        }
    }
}
