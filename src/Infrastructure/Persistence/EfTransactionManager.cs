using EquipFlow.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipFlow.Infrastructure.Persistence;

public sealed class EfTransactionManager(EquipFlowDbContext context) : ITransactionManager
{
    public async Task<IUnitOfWork> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWork(transaction);
    }

    private sealed class EfUnitOfWork(IDbContextTransaction transaction) : IUnitOfWork
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}