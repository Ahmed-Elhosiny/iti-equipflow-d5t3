using EquipFlow.Domain;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using EquipFlow.Domain.ValueObjects;
using EquipFlow.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text;

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

        // CRITICAL FIX: Ensure the pgvector extension is enabled in the newly created database.
        _logger.LogInformation("Ensuring pgvector extension is enabled...");
        await _context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;", cancellationToken);

        // >>> ADD THIS BLOCK: Reload Npgsql type mappings <<<
        // Npgsql caches DB schema on first connect. We must reload it so it recognizes the new 'vector' type.
        var dbConnection = _context.Database.GetDbConnection();
        if (dbConnection is NpgsqlConnection npgsqlConn)
        {
            if (dbConnection.State != ConnectionState.Open)
                await dbConnection.OpenAsync(cancellationToken);
            await npgsqlConn.ReloadTypesAsync();
        }
        // >>> END ADD BLOCK <<<

        _logger.LogInformation("Seeding database...");
        await SeedAsync(cancellationToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting database seeding...");

        await SeedUsersAsync(cancellationToken);
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

        _logger.LogInformation("Seeding expanded document corpus (36 documents, ~180 pages) for grounded retrieval...");

        var equipmentList = new[] { 
            "P-101", "M-101", "C-101", "CV-101", 
            "P-201", "M-201", "C-201", "CV-201", 
            "P-301", "M-301", "C-301", "CV-301" 
        };
        
        var lineMap = new Dictionary<string, string>
        {
            { "P-101", "Line-1" }, { "M-101", "Line-1" }, { "C-101", "Line-1" }, { "CV-101", "Line-1" },
            { "P-201", "Line-2" }, { "M-201", "Line-2" }, { "C-201", "Line-2" }, { "CV-201", "Line-2" },
            { "P-301", "Line-3" }, { "M-301", "Line-3" }, { "C-301", "Line-3" }, { "CV-301", "Line-3" }
        };

        var docTypes = new[] { DocumentType.EquipmentManual, DocumentType.SOP, DocumentType.TroubleshootingGuide };
        int totalDocs = 0;

        foreach (var equipmentId in equipmentList)
        {
            var line = lineMap[equipmentId];
            
            foreach (var docType in docTypes)
            {
                var title = $"{equipmentId} {docType.ToString().Replace("_", " ")}";
                // Generate ~2500 words per document to ensure the total corpus exceeds 150 pages
                var content = GenerateTechnicalContent(equipmentId, docType.ToString(), targetWordCount: 2500);
                
                var documentMetadata = new DocumentMetadata(
                    Source: "EquipTech Internal Knowledge Base",
                    Section: "General",
                    PageNumber: 1,
                    Version: "2.1",
                    Format: "PDF");
                    
                var document = new Document(title, docType, documentMetadata);
                document.MarkAsProcessing();

                var chunks = _chunker.ChunkText(content);
                var chunkTexts = chunks.Select(c => c.Text).ToList();
                
                _logger.LogInformation("Generating embeddings for {Title} ({Count} chunks)...", title, chunks.Count);
                var embeddings = await _embeddingPort.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkResult = chunks[i];
                    var tokenCount = chunkResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    var section = chunkResult.Section ?? "General";

                    var chunkMetadata = new DocumentMetadata(
                        Source: documentMetadata.Source,
                        Section: section,
                        PageNumber: documentMetadata.PageNumber + (i / 2), // Simulate page progression
                        Version: documentMetadata.Version,
                        Format: documentMetadata.Format);

                    var chunk = new DocumentChunk(
                        document.Id,
                        chunkResult.Text,
                        chunkMetadata,
                        tokenCount,
                        equipmentId,
                        line);
                    
                    chunk.SetEmbedding(embeddings[i]);
                    document.AddChunk(chunk);
                }

                document.MarkAsReady();
                _context.Documents.Add(document);
                totalDocs++;
                
                // Save periodically to avoid massive memory spikes and transaction log issues
                if (totalDocs % 5 == 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Saved {Count} documents to database...", totalDocs);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully seeded {Count} documents into the corpus.", totalDocs);
    }

    private string GenerateTechnicalContent(string equipmentName, string docType, int targetWordCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {docType} for {equipmentName}");
        sb.AppendLine($"Revision 2.1. Effective Date: 2026-01-01. Equipment: {equipmentName}");
        sb.AppendLine();
        
        sb.AppendLine("## SAFETY WARNINGS");
        sb.AppendLine("WARNING: Failure to follow these instructions may result in severe injury or death. Ensure all Lockout/Tagout (LOTO) procedures are strictly adhered to before commencing any maintenance activity on this equipment. Verify zero energy state using a calibrated multimeter. Wear appropriate Personal Protective Equipment (PPE) including safety glasses, steel-toed boots, and arc-flash rated clothing where applicable. Never bypass safety interlocks or defeat emergency stop mechanisms. Ensure proper ventilation when working with chemical solvents or lubricants.");
        sb.AppendLine();

        string[] sections = { "Preparation", "Initial Inspection", "Disassembly Procedure", "Component Replacement", "Reassembly", "Testing and Commissioning", "Routine Maintenance Schedule", "Troubleshooting Matrix", "Calibration Procedures", "Environmental Controls" };
        
        // Use a deterministic seed based on equipment and doc type so content is consistent across runs
        Random rnd = new Random(equipmentName.GetHashCode() + docType.GetHashCode());
        int currentWords = 50;
        
        int stepCounter = 1;
        while (currentWords < targetWordCount)
        {
            string section = sections[rnd.Next(sections.Length)];
            sb.AppendLine($"## {section}");
            
            for (int i = 0; i < 5; i++)
            {
                string step = $"Step {stepCounter++}: Inspect the primary {section.ToLower()} components of the {equipmentName}. ";
                step += $"Verify that the torque specifications for all mounting bolts are within the manufacturer's tolerances (typically 45-60 Nm). ";
                step += $"Check for signs of abnormal wear, thermal degradation, or chemical corrosion on the external housing. ";
                step += $"If any anomalies are detected during this phase, halt the procedure immediately and consult the shift supervisor. ";
                step += $"Document all findings in the Computerized Maintenance Management System (CMMS) using work order category '{section}'. ";
                step += $"Ensure that all replacement parts used during this procedure are Original Equipment Manufacturer (OEM) certified and match the exact part number specified in the Bill of Materials (BOM) for {equipmentName}. ";
                step += $"Lubricate all moving bearings using NLGI Grade 2 synthetic grease, applying exactly 15 grams per bearing housing. ";
                step += $"Perform a vibration analysis using the handheld accelerometer; baseline readings must not exceed 0.15 in/s peak velocity. ";
                step += $"Record the ambient temperature and humidity levels, as these environmental factors can significantly impact the operational tolerances of the {equipmentName}. ";
                step += $"Cross-reference the serial number with the historical fault database to identify any recurring failure patterns specific to this unit. ";
                
                sb.AppendLine(step);
                currentWords += step.Split(' ').Length;
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
        private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Users already seeded.");
            return;
        }

        var users = new List<User>
        {
            new User(Guid.Parse("00000000-0000-0000-0000-000000000001"), "technician", PasswordHasher.Hash("password"), "Technician"),
            new User(Guid.Parse("00000000-0000-0000-0000-000000000002"), "engineer", PasswordHasher.Hash("password"), "Engineer"),
            new User(Guid.Parse("00000000-0000-0000-0000-000000000003"), "manager", PasswordHasher.Hash("password"), "Manager"),
            new User(Guid.Parse("00000000-0000-0000-0000-000000000004"), "supervisor", PasswordHasher.Hash("password"), "Supervisor")
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded 4 Users with PBKDF2 hashed passwords.");
    }
}