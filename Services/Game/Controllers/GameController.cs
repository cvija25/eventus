using Microsoft.AspNetCore.Mvc;

namespace Game.Controllers;

[ApiController]
[Route("/api/v1/game")]
public class GameController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> GetGreeting()
    {
        return Ok("Hello World, from Game!");
    }
}