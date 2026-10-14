namespace ESGImpact.Api.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ESGImpact";
    public string Audience { get; set; } = "ESGImpactClient";

    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
