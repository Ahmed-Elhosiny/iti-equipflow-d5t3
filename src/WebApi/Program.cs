using System.Text;
using System.Threading.RateLimiting;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Agents;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Orchestration;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Budget.Services;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Queries;
using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.WebApi.Endpoints;
using EquipFlow.Infrastructure.AI;
using EquipFlow.Infrastructure.Documents.Extractors;
using EquipFlow.Infrastructure.Extensions;
using EquipFlow.Infrastructure.Persistence;
using EquipFlow.Infrastructure.Persistence.Repositories;
using EquipFlow.Infrastructure.Search;
using EquipFlow.Infrastructure.Text;
using EquipFlow.WebApi.Middleware;
using EquipFlow.WebApi.Options;
using EquipFlow.WebApi.Health;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using EquipFlow.Application.Options;
using EquipFlow.Application.Conversations.Ports;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// >>> SECURITY GUARD: Fail-fast in Production if JWT key is insecure <<<
var jwtKey = builder.Configuration["Jwt:Key"];
var defaultPlaceholder = "CHANGE_ME_TO_A_LONG_RANDOM_STRING_AT_LEAST_32_CHARS";

if (builder.Environment.IsProduction() && 
    (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == defaultPlaceholder))
{
    throw new InvalidOperationException(
        "FATAL SECURITY MISCONFIGURATION: JWT signing key is missing or using the default placeholder in Production. " +
        "Please configure a strong, secure JWT__Key via environment variables or a secrets manager.");
}
// >>> END SECURITY GUARD <<<


// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EquipFlow API",
        Version = "v1",
        Description = "D5 Industrial Field Maintenance + T3 Cost Governor API"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, "Bearer")] = new List<string>()
    });
});
builder.Services.AddAntiforgery();
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(SearchDocumentsQuery).Assembly));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddOptions<AgenticOptions>()
    .Bind(builder.Configuration.GetSection(AgenticOptions.SectionName));

// Configure Rate Limiting 
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed_ai", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
    
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Please try again later." }, token);
    };
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = string.IsNullOrWhiteSpace(jwtSettings.Key)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Technician", policy => policy.RequireRole("Technician"));
    options.AddPolicy("Engineer", policy => policy.RequireRole("Engineer"));
    options.AddPolicy("Manager", policy => policy.RequireRole("Manager"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
    options.AddPolicy("Supervisor", policy => policy.RequireRole("Supervisor"));
});
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection missing"),
        name: "postgres",
        tags: new[] { "db", "ready" })
    .AddCheck<LLMHealthCheck>(
        "llm_provider",
        tags: new[] { "ai", "ready" });

// Register DbContext for EF Core design-time tools
builder.Services.AddDbContext<EquipFlowDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=equipflow",
        npgsqlOptions => npgsqlOptions.UseVector() // <--- THIS ENABLES PGVECTOR MAPPING
    ));
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IAgentEventStore, AgentEventStore>();
builder.Services.AddScoped<IUserBudgetRepository, UserBudgetRepository>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
builder.Services.AddSingleton<ITextChunker, StructureAwareTextChunker>();
builder.Services.AddSingleton<IEmbeddingPort, OllamaEmbeddingAdapter>();
builder.Services.AddScoped<PdfDocumentExtractor>();
builder.Services.AddScoped<DocxDocumentExtractor>();
// Route extraction dynamically according to file extension (FR-1)
builder.Services.AddScoped<IDocumentExtractor, DocumentExtractorRouter>();

builder.Services.AddScoped<SymptomMatcherAgent>();
builder.Services.AddScoped<IAgent<SymptomMatchInput, SymptomMatchOutput>>(serviceProvider =>
    serviceProvider.GetRequiredService<SymptomMatcherAgent>());
builder.Services.AddScoped<DiagnosticSafetyPlannerAgent>();
builder.Services.AddScoped<IAgent<DiagnosticPlanInput, DiagnosticPlanOutput>>(serviceProvider =>
    serviceProvider.GetRequiredService<DiagnosticSafetyPlannerAgent>());
builder.Services.AddScoped<WorkOrderGeneratorAgent>();
builder.Services.AddScoped<IAgent<WorkOrderInput, WorkOrderOutput>>(serviceProvider =>
    serviceProvider.GetRequiredService<WorkOrderGeneratorAgent>());
builder.Services.AddSingleton<EquipFlow.Application.Ports.ICachePort, EquipFlow.Infrastructure.Search.InMemorySemanticCacheAdapter>();
builder.Services.AddScoped<EquipFlow.Application.Ports.IModelRouter, EquipFlow.Infrastructure.LLM.BudgetAwareModelRouter>();
builder.Services.AddScoped<EquipFlow.Application.Ports.ITransactionManager, EquipFlow.Infrastructure.Persistence.EfTransactionManager>();
builder.Services.AddScoped<ICostGovernor, CostGovernorService>();
builder.Services.AddHostedService<EquipFlow.Infrastructure.BackgroundServices.BudgetResetBackgroundService>();
builder.Services.AddScoped<SequentialSupervisorOrchestrator>();
builder.Services.AddRagSearchInfrastructure();
builder.Services.AddScoped<EquipFlow.Application.Budget.Ports.IRunSpendRepository, EquipFlow.Infrastructure.Persistence.Repositories.RunSpendRepository>();
builder.Services.AddSingleton<EquipFlow.Application.Ports.ITokenEstimator, EquipFlow.Infrastructure.Text.HeuristicTokenEstimator>();

builder.Services.AddLLMProviders(builder.Configuration);

var primaryProvider = builder.Configuration["DefaultLlmProvider"] ?? "OpenAI";
var fallbackProvider = builder.Configuration["Llm:FallbackProvider"] ?? "Ollama";
var providerChain = new List<string> { primaryProvider, fallbackProvider };

builder.Services.AddScoped<ILLMProvider>(serviceProvider =>
    new ConfiguredLlmProvider(
        serviceProvider.GetRequiredService<EquipFlow.Application.Ports.LLM.ILLMProviderFactory>(),
        providerChain,
        serviceProvider.GetRequiredService<IOptions<AgenticOptions>>(),
        serviceProvider.GetRequiredService<ILogger<ConfiguredLlmProvider>>()));

builder.Services.AddScoped<EquipFlow.Application.Ports.LLM.ILLMGenerationPort>(serviceProvider =>
    serviceProvider
        .GetRequiredService<EquipFlow.Application.Ports.LLM.ILLMProviderFactory>()
        .GetProvider(primaryProvider));

builder.Services.AddEquipFlowTools();
builder.Services.AddSingleton<EquipFlow.Application.Agentic.Abstractions.IActiveRunRegistry, EquipFlow.Infrastructure.Agentic.ActiveRunRegistry>();

var app = builder.Build();

var isSeedMode = args.Any(argument => string.Equals(argument, "--seed", StringComparison.OrdinalIgnoreCase))
    || bool.TryParse(Environment.GetEnvironmentVariable("SEED_MODE"), out var seedMode)
        && seedMode;

if (isSeedMode)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.MigrateAndSeedAsync();
    app.Logger.LogInformation("Seed completed. Exiting.");
    return;
}

// Configure the HTTP request pipeline.
app.UseMiddleware<SecurityHeadersMiddleware>(); // Inject OWASP headers early
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter(); // Enforce rate limits after AuthZ so we can identify users if needed
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness probe: Only checks if the app is running (no dependencies)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false 
});

// Readiness probe: Checks critical dependencies (DB + LLM)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(json);
    }
});

// Full health check (all dependencies)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(json);
    }
});

app.MapAuthEndpoints();
app.MapCostGovernorEndpoints();
app.MapDocumentsEndpoints();
app.MapSearchEndpoints();
app.MapAiEndpoints();
app.MapEquipmentEndpoints();
app.MapMaintenanceEndpoints(); 
app.MapWorkOrderEndpoints();
app.MapConversationEndpoints();

app.Run();

public partial class Program;

file sealed class ConfiguredLlmProvider(
    EquipFlow.Application.Ports.LLM.ILLMProviderFactory providerFactory,
    IReadOnlyList<string> providerChain,
    IOptions<AgenticOptions> agenticOptions,
    ILogger<ConfiguredLlmProvider> logger) : ILLMProvider
{
    private readonly int _maxLlmRetries = agenticOptions.Value.MaxLlmRetries;
    private readonly int _baseDelayMs = agenticOptions.Value.LlmRetryBaseDelayMs;

    public async Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // CG-010 / ADR-004: Fail-closed at the provider boundary. No reservation token = no call.
        if (string.IsNullOrWhiteSpace(request.ReservationId))
        {
            throw new InvalidOperationException("ADR-004 Violation: LLM execution requires a valid Budget ReservationId.");
        }

        Exception? lastException = null;
        
        foreach (var providerName in providerChain)
        {
            var generationPort = providerFactory.GetProvider(providerName);
            var llmRequest = BuildRequest(request);
            
            for (int attempt = 0; attempt <= _maxLlmRetries; attempt++)
            {
                try
                {
                    var result = await generationPort.CompleteAsync(llmRequest, cancellationToken);

                    return new CompletionResult(
                        result.Content ?? string.Empty,
                        new TokenUsage(result.PromptTokens, result.CompletionTokens),
                        result.FinishReason,
                        result.ToolCalls?.Select(toolCall => new ToolCall(
                            toolCall.Id,
                            toolCall.Name,
                            toolCall.ArgumentsJson)).ToArray());
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < _maxLlmRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(_baseDelayMs * Math.Pow(2, attempt));
                    logger.LogWarning(ex, "Transient failure on {ProviderName} (attempt {Attempt}/{Max}). Retrying in {Delay}ms.", 
                        providerName, attempt + 1, _maxLlmRetries, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    logger.LogWarning(ex, "Transient failure on {ProviderName} after {Max} retries. Moving to next provider.", 
                        providerName, _maxLlmRetries);
                    lastException = ex;
                    break; // Break out of retry loop, move to next provider in cascade
                }
            }
        }

        throw new InvalidOperationException("All LLM providers in the fallback chain failed.", lastException);
    }

    public async IAsyncEnumerable<StreamingChunk> StreamAsync(
        CompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
         // CG-010 / ADR-004: Fail-closed at the provider boundary. No reservation token = no call.
        if (string.IsNullOrWhiteSpace(request.ReservationId))
        {
            throw new InvalidOperationException("ADR-004 Violation: LLM execution requires a valid Budget ReservationId.");
        }
        
        Exception? lastException = null;
        
        foreach (var providerName in providerChain)
        {
            var generationPort = providerFactory.GetProvider(providerName);
            var llmRequest = BuildRequest(request);
            IAsyncEnumerable<EquipFlow.Application.Ports.LLM.LLMStreamChunk>? stream = null;

            for (int attempt = 0; attempt <= _maxLlmRetries; attempt++)
            {
                try
                {
                    stream = generationPort.StreamAsync(llmRequest, cancellationToken);
                    break; // Successfully initialized stream, break out of retry loop
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < _maxLlmRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(_baseDelayMs * Math.Pow(2, attempt));
                    logger.LogWarning(ex, "Transient failure initializing stream on {ProviderName} (attempt {Attempt}/{Max}). Retrying in {Delay}ms.", 
                        providerName, attempt + 1, _maxLlmRetries, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    logger.LogWarning(ex, "Transient failure initializing stream on {ProviderName} after {Max} retries. Moving to next provider.", 
                        providerName, _maxLlmRetries);
                    lastException = ex;
                    break; // Break out of retry loop, move to next provider in cascade
                }
            }

            if (stream is null) continue;

            // Yielding happens outside the try-catch block to satisfy C# iterator rules
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                yield return new StreamingChunk(chunk.DeltaContent ?? string.Empty, chunk.IsFinished);
            }
            
            yield break; // Successfully completed streaming from this provider
        }

        throw new InvalidOperationException("All LLM providers in the fallback chain failed.", lastException);
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;

    public Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Embeddings are provided by IEmbeddingPort.");

    public Task<ToolExecutionResult> ExecuteToolAsync(
        ToolCallRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tools are dispatched by IToolDispatcher.");

    private static EquipFlow.Application.Ports.LLM.LLMRequest BuildRequest(
        CompletionRequest request)
    {
        var messages = new List<EquipFlow.Application.Ports.LLM.ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new EquipFlow.Application.Ports.LLM.ChatMessage(
                EquipFlow.Application.Ports.LLM.ChatRole.System,
                request.SystemPrompt,
                null,
                null));
        }

        messages.Add(new EquipFlow.Application.Ports.LLM.ChatMessage(
            EquipFlow.Application.Ports.LLM.ChatRole.User,
            request.Prompt,
            null,
            null));

        return new EquipFlow.Application.Ports.LLM.LLMRequest(
            messages,
            request.Tools?.Select(tool => new EquipFlow.Application.Ports.LLM.ToolDefinition(
                tool.Name,
                tool.Description,
                tool.ParametersJsonSchema)).ToArray(),
            request.Temperature,
            request.MaxTokens,
            request.ReservationId);
    }
}