using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaguesController : ControllerBase
    {
        private readonly FootballApiService _football;

        public LeaguesController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeagues([FromQuery] string? country)
        {
            var result = await _football.GetLeaguesAsync(country);
            return Ok(result);
        }
    }
}