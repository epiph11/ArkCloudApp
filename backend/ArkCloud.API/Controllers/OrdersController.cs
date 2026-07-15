using ArkCloud.API.Authorization;
using ArkCloud.Application.DTOs;
using ArkCloud.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkCloud.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderAppService _service;
    private readonly IValidator<CreateOrderRequest> _createValidator;

    public OrdersController(OrderAppService service, IValidator<CreateOrderRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    /// <summary>?search= matches a CustomerId, an order status name, or the customer's name/email. Paged, 20/page by default.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>> GetAll(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await _service.GetPagedAsync(search, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreateOrders)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = PolicyNames.CanCreateOrders)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        await _service.SubmitAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.ManagersOnly)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
