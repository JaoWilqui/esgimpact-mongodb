namespace ESGImpact.Api.ViewModels;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInSeconds { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string ParticipantCode { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}
