namespace EquipFlow.WebApi.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication";

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public string? Key { get; set; }
}