using ArkCloud.Application.DTOs;
using ArkCloud.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ArkCloud.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderAppService _service;
    private readonly IValidator<CreateOrderRequest> _createValidator;

    public OrdersController(OrderAppService service, IValidator<CreateOrderRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderResponse>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        await _service.SubmitAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
