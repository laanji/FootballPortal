using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeamController : ControllerBase
    {
        private readonly FootballApiService _football;

        public TeamController(FootballApiService football)
        {
            _football = football;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTeam(int id)
        {
            var text = await _football.GetTeamInfoAsync(id);
            return Ok(text);
        }


    }

}
