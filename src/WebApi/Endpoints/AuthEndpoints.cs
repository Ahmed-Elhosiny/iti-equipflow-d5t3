using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EquipFlow.Application.Ports;
using EquipFlow.Infrastructure.Persistence;
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

        private static async Task<IResult> Login(
        LoginRequest request,
        IUserRepository userRepository,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        bool isValid = false;

        if (user is not null && user.IsActive)
        {
            isValid = PasswordHasher.Verify(request.Password, user.PasswordHash);
        }
        else
        {
            // Burn CPU time to prevent user-enumeration timing attacks
            PasswordHasher.Verify(request.Password, PasswordHasher.Hash("dummy_password_for_timing")); 
        }

        if (!isValid || user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var settings = jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.Key))
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "JWT Key is not configured");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
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