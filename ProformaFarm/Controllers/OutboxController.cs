using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Interfaces.Outbox;

namespace ProformaFarm.Controllers;

[ApiController]
[Authorize]
[Route("api/outbox")]
public sealed class OutboxController : ControllerBase
{
    private readonly IOutboxHelloService _outboxHelloService;
    private readonly IOutboxProcessor _outboxProcessor;

    public OutboxController(IOutboxHelloService outboxHelloService, IOutboxProcessor outboxProcessor)
    {
        _outboxHelloService = outboxHelloService;
        _outboxProcessor = outboxProcessor;
    }

    [HttpPost("hello-event")]
    public async Task<IActionResult> EnqueueHelloEvent([FromBody] EnqueueHelloEventRequest? request = null)
    {
        var nomeEvento = request?.NomeEvento;
        var simularFalhaUmaVez = request?.SimularFalhaUmaVez ?? false;

        var result = await _outboxHelloService.EnqueueHelloEventAsync(
            nomeEvento,
            simularFalhaUmaVez,
            HttpContext.RequestAborted);

        return Ok(ApiResponse<OutboxHelloResult>.Ok(result, "Evento de prova de vida enviado ao Outbox com sucesso."));
    }

    [HttpPost("processar-agora")]
    public async Task<IActionResult> ProcessarAgora()
    {
        var total = await _outboxProcessor.ProcessPendingAsync(HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(new { totalProcessados = total }, "Processamento manual do Outbox executado."));
    }

    public sealed class EnqueueHelloEventRequest
    {
        public string? NomeEvento { get; set; }
        public bool SimularFalhaUmaVez { get; set; }
    }
}
