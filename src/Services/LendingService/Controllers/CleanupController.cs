using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MercuryPay.LendingService.Services;

namespace MercuryPay.LendingService.Controllers;

[ApiController]
[Route("[controller]")]
public class CleanupController(DataCleanupService cleanupService) : ControllerBase
{
    private readonly DataCleanupService _cleanupService = cleanupService;

    [HttpPost("trigger")]
    [AllowAnonymous]
    public async Task<IActionResult> Trigger(CancellationToken cancellationToken)
    {
        await _cleanupService.TriggerCleanupAsync(cancellationToken);
        return Ok("Cleanup triggered");
    }
}
