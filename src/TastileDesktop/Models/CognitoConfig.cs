using System;

namespace TastileDesktop.Models;

/// <summary>
/// Cognito configuration loaded from environment variables.
/// Mirrors tastile-web's <c>lib/cognito/env.ts</c>.
/// </summary>
public sealed record CognitoConfig(
    string UserPoolId,
    string ClientId,
    string HostedUiDomain,
    string Region,
    string CallbackUrl,
    string WebLoginUrl)
{
    public string Issuer => $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";

    public string HostedUiBaseUrl =>
        $"https://{HostedUiDomain}.auth.{Region}.amazoncognito.com";

    public static CognitoConfig FromEnv()
    {
        return new CognitoConfig(
            UserPoolId: RequireEnv("TASTILE_COGNITO_USER_POOL_ID"),
            ClientId: RequireEnv("TASTILE_COGNITO_CLIENT_ID"),
            HostedUiDomain: RequireEnv("TASTILE_COGNITO_HOSTED_UI_DOMAIN"),
            Region: RequireEnv("TASTILE_COGNITO_REGION"),
            CallbackUrl: RequireEnv("TASTILE_COGNITO_CALLBACK_URL"),
            WebLoginUrl: RequireEnv("TASTILE_WEB_LOGIN_URL"));
    }

    private static string RequireEnv(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw new InvalidOperationException(
                $"Missing environment variable {name} — please set it before running. See .env.example for the contract.");
        }

        return raw;
    }
}
