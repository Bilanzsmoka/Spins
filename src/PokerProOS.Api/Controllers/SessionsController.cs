using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Sessions.Commands;
using PokerProOS.Application.Sessions.Interfaces;

namespace PokerProOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _repo;

    public SessionsController(ISessionRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int userId)
    {
        var sessions = await _repo.GetAllAsync(userId);
        return Ok(sessions);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var session = await _repo.GetByIdAsync(id);
        return session == null ? NotFound() : Ok(session);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionCommand command)
    {
        var handler = new CreateSessionHandler(_repo);
        var session = await handler.Handle(command);
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _repo.DeleteAsync(id);
        return NoContent();
    }
}
