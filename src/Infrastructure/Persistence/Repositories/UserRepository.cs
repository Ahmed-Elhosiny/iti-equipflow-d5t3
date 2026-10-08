using EquipFlow.Application.Ports;
using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public class UserRepository(EquipFlowDbContext context) : IUserRepository
{
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct) =>
        await context.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.Users.FindAsync([id], ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(ct);
    }
}
