using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HighlightsController : ControllerBase
    {
        private readonly FootballApiService _football;

        public HighlightsController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("{teamName}")]
        public async Task<IActionResult> GetHighlight(string teamName)
        {
            var result = await _football.GetTeamHighlightAsync(teamName);
            return Ok(result);
        }
    }
}