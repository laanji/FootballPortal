using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly FootballApiService _football;

        public ScheduleController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("{teamId}/{season}/{count}")]
        public async Task<IActionResult> GetSchedule(int teamId, int season, int count)
        {
            var result = await _football.GetTeamScheduleAsync(teamId, season, count);
            return Ok(result);
        }
    }
}
