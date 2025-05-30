using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers 
{ 

    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly FootballApiService _football;

        public MatchesController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("live")]
        public async Task<IActionResult> GetLiveMatches()
        {
            var matches = await _football.GetLiveMatchesAsync();
            return Ok(matches);
        }

    }
}
