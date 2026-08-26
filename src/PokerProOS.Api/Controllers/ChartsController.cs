using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Charts.Commands;
using PokerProOS.Application.Charts.DTOs;
using PokerProOS.Application.Charts.Queries;
using PokerProOS.Infrastructure.Services;

namespace PokerProOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChartsController : ControllerBase
{
    private readonly GetChartByStackHandler _handler;
    private readonly ChartImportService _importService;

    public ChartsController(GetChartByStackHandler handler, ChartImportService importService)
    {
        _handler = handler;
        _importService = importService;
    }

    [HttpGet("{situation}/{stack}")]
    public async Task<IActionResult> GetByStack(string situation, string stack)
    {
        var result = await _handler.Handle(new GetChartByStackQuery(situation, stack));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{situation}/{stack}/{spot}")]
    public async Task<IActionResult> GetBySpot(string situation, string stack, string spot)
    {
        var result = await _handler.Handle(new GetChartByStackQuery(situation, stack, spot));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportChartDataCommand command)
    {
        var result = await _importService.ImportFromDirectoryAsync(command.JsonDirectory);
        return Ok(result);
    }
}
