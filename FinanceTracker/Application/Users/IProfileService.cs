using Domain.Users;
using Shared.Results;

namespace Application.Users;

public interface IProfileService
{
    Task<Result<User>> GetCurrentProfileAsync(CancellationToken ct = default);
    Task<Result<User>> UpdateProfileAsync(string? firstName, string? lastName, string? avatarUrl, string? username, string? email, CancellationToken ct = default);
}
