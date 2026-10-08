using EquipFlow.Domain.Entities;

namespace EquipFlow.Application.Ports;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}
