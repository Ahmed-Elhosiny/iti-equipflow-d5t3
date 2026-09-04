using Microsoft.EntityFrameworkCore;
using EquipFlow.Domain.Entities;

namespace EquipFlow.Infrastructure.Persistence;

public class EquipFlowDbContext : DbContext
{
    public EquipFlowDbContext(DbContextOptions<EquipFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<SafetyPrerequisite> SafetyPrerequisites => Set<SafetyPrerequisite>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EquipFlowDbContext).Assembly);
    }
}
