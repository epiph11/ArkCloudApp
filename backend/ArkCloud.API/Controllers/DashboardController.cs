using ArkCloud.Application.DTOs;
using ArkCloud.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkCloud.API.Controllers;

/// <summary>Read-only aggregate stats for the Blazor Dashboard landing page. Any authenticated role.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardAppService _service;

    public DashboardController(DashboardAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(cancellationToken));
}
