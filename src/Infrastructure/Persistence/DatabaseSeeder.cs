using EquipFlow.Domain;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using EquipFlow.Domain.ValueObjects;
using EquipFlow.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly EquipFlowDbContext _context;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingPort _embeddingPort;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        EquipFlowDbContext context, 
        ITextChunker chunker,
        IEmbeddingPort embeddingPort,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _chunker = chunker;
        _embeddingPort = embeddingPort;
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
        await SeedDocumentsAsync(cancellationToken);

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

        private async Task SeedDocumentsAsync(CancellationToken cancellationToken)
    {
        if (await _context.Documents.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Document corpus already seeded.");
            return;
        }

        _logger.LogInformation("Seeding document corpus for grounded retrieval...");

        var manuals = new List<(string Title, DocumentType Type, string Content, string EquipmentId, string Line, string Section)>
        {
            (
                "P-101 Centrifugal Pump Maintenance Manual",
                DocumentType.EquipmentManual,
                "The P-101 centrifugal pump is designed for high-pressure fluid transfer in Line-1. Operating limits: Maximum discharge pressure 150 PSI, maximum temperature 200°F. Routine maintenance requires checking mechanical seals every 500 operating hours. Bearing lubrication must be performed every 1000 hours using NLGI Grade 2 grease. Safety protocol: Strict Lockout/Tagout (LOTO) procedures must be followed before opening the pump casing. Ensure all residual pressure is bled from the system via the drain valve before disassembly.",
                "P-101", "Line-1", "Operating Limits & Routine Maintenance"
            ),
            (
                "M-101 Electric Motor Operations Guide",
                DocumentType.EquipmentManual,
                "The M-101 AC induction motor drives the primary conveyor system on Line-1. Vibration limits must not exceed 0.2 in/s peak velocity. If vibration exceeds this threshold, immediate shutdown is required to prevent catastrophic bearing failure. Inspect cooling fins weekly for dust accumulation. Safety protocol: Ensure complete power disconnect and verify zero energy state using a multimeter before performing any internal inspections or terminal connections.",
                "M-101", "Line-1", "Vibration Limits & Safety"
            ),
            (
                "C-101 Industrial Compressor Safety Procedures",
                DocumentType.SafetyProcedure,
                "The C-101 rotary screw compressor provides plant air for Line-1 pneumatic tools. Check oil levels daily via the sight glass; maintain level between min and max indicators. Maximum allowable discharge temperature is 180°F. In case of high-temperature alarm, initiate emergency shutdown procedure immediately by pressing the red E-Stop button on the local control panel. Never open the oil separator while the system is pressurized.",
                "C-101", "Line-1", "Emergency Shutdown & Pressurization Safety"
            ),
            (
                "CV-101 Conveyor Belt Alignment SOP",
                DocumentType.SOP,
                "Standard Operating Procedure for CV-101 conveyor belt tracking. If belt slippage or misalignment is detected, stop the conveyor immediately. Loosen the tail pulley adjustment bolts by exactly two turns. Adjust the tracking bolts incrementally (1/4 turn at a time) while running the belt at 10% speed. Do not exceed 20% speed during alignment. Once aligned, tighten tail pulley bolts to 45 ft-lbs torque.",
                "CV-101", "Line-1", "Belt Tracking & Alignment"
            ),
            (
                "P-201 Pump Overheating Troubleshooting Guide",
                DocumentType.TroubleshootingGuide,
                "Symptom: P-201 pump casing temperature exceeds 160°F. Potential Causes: 1. Cavitation due to low suction pressure. Check suction strainer for blockage. 2. Cooling jacket flow restricted. Verify cooling water flow rate is > 5 GPM. 3. Bearing failure. Listen for high-frequency acoustic emissions using ultrasonic detector. Resolution: Flush suction strainer and backwash cooling jacket. If temperature persists, schedule bearing replacement.",
                "P-201", "Line-2", "Overheating Diagnostics"
            )
        };

        foreach (var manual in manuals)
        {
            // 1. Create metadata specifically for the Document
            var documentMetadata = new DocumentMetadata(
                Source: "EquipTech Internal Knowledge Base",
                Section: manual.Section,
                PageNumber: 1,
                Version: "1.0",
                Format: "PDF");
                
            var document = new Document(manual.Title, manual.Type, documentMetadata);
            document.MarkAsProcessing();

            var chunks = _chunker.ChunkText(manual.Content);
            var chunkTexts = chunks.Select(c => c.Text).ToList();
            var embeddings = await _embeddingPort.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkResult = chunks[i];
                var tokenCount = chunkResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                
                // Use the section extracted by the chunker if available, otherwise fallback to the manual's section
                var section = chunkResult.Section ?? manual.Section;

                // FIX: Create a DISTINCT metadata instance for the DocumentChunk.
                // EF Core tracks owned types by reference. Sharing the exact same record instance 
                // between Document and DocumentChunk causes EF Core to confuse their shadow 
                // foreign keys (e.g., trying to use DocumentId for the chunk's metadata).
                var chunkMetadata = new DocumentMetadata(
                    Source: documentMetadata.Source,
                    Section: section,
                    PageNumber: documentMetadata.PageNumber,
                    Version: documentMetadata.Version,
                    Format: documentMetadata.Format);

                var chunk = new DocumentChunk(
                    document.Id,
                    chunkResult.Text,
                    chunkMetadata,
                    tokenCount,
                    manual.EquipmentId,
                    manual.Line);
                
                chunk.SetEmbedding(embeddings[i]);
                document.AddChunk(chunk);
            }

            document.MarkAsReady();
            _context.Documents.Add(document);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} documents into the corpus.", manuals.Count);
    }
}