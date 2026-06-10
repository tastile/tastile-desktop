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
    string CallbackUrl)
{
    public string Issuer => $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";

    public string HostedUiBaseUrl =>
        $"https://{HostedUiDomain}.auth.{Region}.amazoncognito.com";

    public static CognitoConfig? TryFromEnv()
    {
        var clientId = Environment.GetEnvironmentVariable("TASTILE_COGNITO_CLIENT_ID")?.Trim();
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        return new CognitoConfig(
            UserPoolId: Environment.GetEnvironmentVariable("TASTILE_COGNITO_USER_POOL_ID")?.Trim()
                ?? "ap-northeast-1_pwYcPWOyR",
            ClientId: clientId,
            HostedUiDomain: Environment.GetEnvironmentVariable("TASTILE_COGNITO_HOSTED_UI_DOMAIN")?.Trim()
                ?? "tastile-beta",
            Region: Environment.GetEnvironmentVariable("TASTILE_COGNITO_REGION")?.Trim()
                ?? "ap-northeast-1",
            CallbackUrl: Environment.GetEnvironmentVariable("TASTILE_COGNITO_CALLBACK_URL")?.Trim()
                ?? "tastile://auth/callback");
    }
}
