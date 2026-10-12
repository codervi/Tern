namespace Tern.Api.Security;

public class JwtOptions
{
    public string Key { get; set; } = "";       // from user-secrets / environment, never from the repo
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int Minutes { get; set; } = 60;
}
