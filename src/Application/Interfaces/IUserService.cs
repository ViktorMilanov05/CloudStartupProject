using Application.DTOs;
using Application.DTOs.Users;

namespace Application.Interfaces;

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(Guid companyId, CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserAsync(Guid userId, Guid? companyId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
}
