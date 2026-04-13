using Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record UpdateProfileRequest(string? FirstName, string? LastName, string? AvatarUrl, string? Username, string? Email);

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await profileService.GetCurrentProfileAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await profileService.UpdateProfileAsync(request.FirstName, request.LastName, request.AvatarUrl, request.Username, request.Email, ct);
        return this.ToActionResult(result);
    }
}
