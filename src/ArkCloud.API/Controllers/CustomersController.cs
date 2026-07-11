using ArkCloud.Application.DTOs;
using ArkCloud.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ArkCloud.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CustomerAppService _service;
    private readonly IValidator<CreateCustomerRequest> _createValidator;

    public CustomersController(CustomerAppService service, IValidator<CreateCustomerRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
