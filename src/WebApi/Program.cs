using System.Text;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Budget.Services;
using EquipFlow.Application.CostGovernor.Queries;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Queries;
using EquipFlow.WebApi.Endpoints;
using EquipFlow.Infrastructure.Extensions;
using EquipFlow.Infrastructure.Persistence;
using EquipFlow.Infrastructure.Persistence.Repositories;
using EquipFlow.Infrastructure.Search;
using EquipFlow.WebApi.Middleware;
using EquipFlow.WebApi.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHealthChecks();

// Register DbContext for EF Core design-time tools
builder.Services.AddDbContext<EquipFlowDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=equipflow"));
builder.Services.AddScoped<IUserBudgetRepository, UserBudgetRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
builder.Services.AddScoped<ICostGovernor, CostGovernorService>();
builder.Services.AddRagSearchInfrastructure();
builder.Services.AddLLMProviders(builder.Configuration);
builder.Services.AddEquipFlowTools();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapCostGovernorEndpoints();
app.MapDocumentsEndpoints();
app.MapSearchEndpoints();

app.Run();

public partial class Program;

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
