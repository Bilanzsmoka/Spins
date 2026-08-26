using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Trainer.Interfaces;
using PokerProOS.Application.Trainer.Queries;

namespace PokerProOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainerController : ControllerBase
{
    private readonly EvaluateAnswerHandler _handler;
    private readonly ITrainerRepository _repo;

    public TrainerController(EvaluateAnswerHandler handler, ITrainerRepository repo)
    {
        _handler = handler;
        _repo = repo;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateAnswerQuery query)
    {
        var result = await _handler.Handle(query);
        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int userId)
    {
        var stats = await _repo.GetStatsAsync(userId);
        return Ok(stats);
    }
}
