namespace EquipFlow.Application.Ports;

public interface ITransactionManager
{
    Task<IUnitOfWork> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}