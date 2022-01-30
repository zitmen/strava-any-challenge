using Lib;

namespace Functions.StravaDto;

public class Login
{
    public string code { get; }
    public ulong client_id { get; }
    public string client_secret { get; }
    public string grant_type { get; }

    public Login(string code)
    {
        this.code = code;

        grant_type = "authorization_code";
        client_id = Config.StravaAppClientId;
        client_secret = Config.StravaAppClientSecret;
    }
}