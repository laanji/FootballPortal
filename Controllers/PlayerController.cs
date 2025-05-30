using Microsoft.AspNetCore.Mvc;
using FootballPortal.Services;

namespace FootballPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly FootballApiService _football;

        public PlayerController(FootballApiService football)
        {
            _football = football;
        }

        // повний профіль гравця за  ID 
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlayerProfile(int id)
        {
            var text = await _football.GetPlayerProfileTextAsync(id);
            return Ok(text);
        }
    }
}
