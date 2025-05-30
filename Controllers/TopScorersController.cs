using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopScorersController : ControllerBase
    {
        private readonly FootballApiService _football;

        public TopScorersController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("{leagueId}")]
        public async Task<IActionResult> GetTopScorers(int leagueId)
        {
            var scorers = await _football.GetTopScorersAsync(leagueId);
            if (scorers.Count == 0)
                return NotFound("Бомбардири не знайдені.");

            return Ok(scorers);
        }
    }
}
