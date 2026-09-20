using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EquipFlow.WebApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EquipFlow.WebApi.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate a demo user and get a JWT")
            .WithTags("Authentication")
            .AllowAnonymous()
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static IResult Login(
        LoginRequest request,
        IOptions<JwtOptions> jwtOptions)
    {
       // Hardcoded MVP Demo Users for testing - Aligned with DatabaseSeeder.cs
        var demoUsers = new Dictionary<string, (string Password, string Role, Guid Id)>(StringComparer.OrdinalIgnoreCase)
        {
            ["technician"] = ("password", "Technician", Guid.Parse("00000000-0000-0000-0000-000000000001")),
            ["engineer"] = ("password", "Engineer", Guid.Parse("00000000-0000-0000-0000-000000000002")),
            ["manager"] = ("password", "Manager", Guid.Parse("00000000-0000-0000-0000-000000000003")),
            ["supervisor"] = ("password", "Supervisor", Guid.Parse("00000000-0000-0000-0000-000000000004"))
        };

        if (!demoUsers.TryGetValue(request.Username, out var user) || user.Password != request.Password)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials");
        }

        var settings = jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.Key))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "JWT Key is not configured in appsettings.json");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, request.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiration = DateTime.UtcNow.AddMinutes(settings.ExpirationInMinutes > 0 ? settings.ExpirationInMinutes : 60);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        return Results.Ok(new LoginResponse(tokenString, expiration));
    }

    private sealed record LoginRequest(string Username, string Password);
    private sealed record LoginResponse(string Token, DateTime ExpiresAt);
}