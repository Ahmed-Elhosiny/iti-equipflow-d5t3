using Microsoft.EntityFrameworkCore;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain;
using EquipFlow.Domain.Entities;
using EquipFlow.Infrastructure.Persistence.Configurations;
using EquipFlow.Infrastructure.Persistence.Entities;
namespace EquipFlow.Infrastructure.Persistence;

public class EquipFlowDbContext : DbContext
{
    public EquipFlowDbContext(DbContextOptions<EquipFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Equipment> Equipments { get; set; } = null!;
    public DbSet<SafetyPrerequisite> SafetyPrerequisites => Set<SafetyPrerequisite>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<UserBudget> UserBudgets { get; set; } = null!;
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<AgentEventEntity> AgentEvents => Set<AgentEventEntity>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new EquipmentConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EquipFlowDbContext).Assembly);
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }

        modelBuilder.Entity<AgentEventEntity>(entity =>
{
    entity.HasKey(e => e.Id);

    entity.Property(e => e.Id)
        .ValueGeneratedOnAdd();

    entity.HasIndex(e => e.CorrelationId);
    entity.HasIndex(e => e.Timestamp);

    entity.Property(e => e.AgentName)
        .IsRequired();

    entity.Property(e => e.EventType)
        .IsRequired();

    entity.Property(e => e.Payload)
        .IsRequired()
        .HasColumnType("jsonb");
});
    }
}
