using EquipFlow.Domain;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly EquipFlowDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(EquipFlowDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task MigrateAndSeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running database migrations...");
        await _context.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Seeding database...");
        await SeedAsync(cancellationToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting database seeding...");

        await SeedEquipmentsAsync(cancellationToken);
        await SeedUserBudgetsAsync(cancellationToken);

        _logger.LogInformation("Database seeding completed.");
    }

    private async Task SeedEquipmentsAsync(CancellationToken cancellationToken)
    {
        var existingEquipments = await _context.Equipments.ToListAsync(cancellationToken);

        var lineMap = new Dictionary<string, string>
        {
            { "P-101", "Line-1" }, { "M-101", "Line-1" }, { "C-101", "Line-1" }, { "CV-101", "Line-1" },
            { "P-201", "Line-2" }, { "M-201", "Line-2" }, { "C-201", "Line-2" }, { "CV-201", "Line-2" },
            { "P-301", "Line-3" }, { "M-301", "Line-3" }, { "C-301", "Line-3" }, { "CV-301", "Line-3" }
        };

        if (!existingEquipments.Any())
        {
            var equipments = new List<Equipment>
            {
                new Equipment { Name = "P-101", SerialNumber = "SN-P-101-001", Line = "Line-1" },
                new Equipment { Name = "M-101", SerialNumber = "SN-M-101-002", Line = "Line-1" },
                new Equipment { Name = "C-101", SerialNumber = "SN-C-101-003", Line = "Line-1" },
                new Equipment { Name = "CV-101", SerialNumber = "SN-CV-101-004", Line = "Line-1" },
                
                new Equipment { Name = "P-201", SerialNumber = "SN-P-201-005", Line = "Line-2" },
                new Equipment { Name = "M-201", SerialNumber = "SN-M-201-006", Line = "Line-2" },
                new Equipment { Name = "C-201", SerialNumber = "SN-C-201-007", Line = "Line-2" },
                new Equipment { Name = "CV-201", SerialNumber = "SN-CV-201-008", Line = "Line-2" },
                
                new Equipment { Name = "P-301", SerialNumber = "SN-P-301-009", Line = "Line-3" },
                new Equipment { Name = "M-301", SerialNumber = "SN-M-301-010", Line = "Line-3" },
                new Equipment { Name = "C-301", SerialNumber = "SN-C-301-011", Line = "Line-3" },
                new Equipment { Name = "CV-301", SerialNumber = "SN-CV-301-012", Line = "Line-3" }
            };

            _context.Equipments.AddRange(equipments);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded 12 Equipments across 3 production lines.");
            return;
        }

        // Update existing equipment that might have null Line from previous seeds
        bool updated = false;
        foreach (var eq in existingEquipments)
        {
            if (string.IsNullOrEmpty(eq.Line) && lineMap.TryGetValue(eq.Name, out var line))
            {
                eq.Line = line;
                updated = true;
            }
        }

        if (updated)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated existing Equipments with production lines.");
        }
        else
        {
            _logger.LogInformation("Equipments already seeded and updated.");
        }
    }

    private async Task SeedUserBudgetsAsync(CancellationToken cancellationToken)
    {
        if (await _context.UserBudgets.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("UserBudgets already seeded.");
            return;
        }

        var technicianId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var engineerId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var managerId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var supervisorId = Guid.Parse("00000000-0000-0000-0000-000000000004");

        var budgets = new List<UserBudget>
        {
            new UserBudget(technicianId, Money.FromDecimal(100.00m)),
            new UserBudget(engineerId, Money.FromDecimal(100.00m)),
            new UserBudget(managerId, Money.FromDecimal(100.00m)),
            new UserBudget(supervisorId, Money.FromDecimal(100.00m))
        };

        _context.UserBudgets.AddRange(budgets);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded 4 User Budgets.");
    }
}