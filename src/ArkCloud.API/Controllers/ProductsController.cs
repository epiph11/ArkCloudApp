using ArkCloud.Application.DTOs;
using ArkCloud.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ArkCloud.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductAppService _service;
    private readonly IValidator<CreateProductRequest> _createValidator;

    public ProductsController(ProductAppService service, IValidator<CreateProductRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
