namespace LoadKit.Core.Auth;

public static class AuthRequestOptions
{
    /// <summary>Set to <c>true</c> for requests with <c>"auth": false</c>; <see cref="AuthHandler"/> then sends them as is.</summary>
    public static readonly HttpRequestOptionsKey<bool> SkipAuth = new("LoadKit.SkipAuth");
}
