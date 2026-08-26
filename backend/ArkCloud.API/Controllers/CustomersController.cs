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
public class CustomersController : ControllerBase
{
    private readonly CustomerAppService _service;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;

    public CustomersController(
        CustomerAppService service,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>?search= matches first name, last name, or email (case-insensitive). Paged, 20/page by default.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> GetAll(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await _service.GetPagedAsync(search, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(id, cancellationToken));

    // Any authenticated user can create a customer for now — Phase 2A has no admin
    // bootstrap/role-management endpoint yet, so gating this behind Manager/Admin would make
    // it unreachable for a freshly registered ("User" role) account. Revisit once role
    // assignment/promotion exists (see the Blazor/Entra ID follow-up phases).
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _service.UpdateAsync(id, request, cancellationToken));
    }

    // Delete is Admin-only — customers may already be referenced by orders, and this is a
    // destructive, hard-to-undo action we don't want a plain "User" account to be able to trigger.
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
