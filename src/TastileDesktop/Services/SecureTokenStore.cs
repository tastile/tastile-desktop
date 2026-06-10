using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// DPAPI-protected token persistence. Stores the <see cref="AuthSession"/>
/// JSON at <c>%LOCALAPPDATA%\Tastile\Auth\credentials.bin</c>, encrypted
/// with <see cref="DataProtectionScope.CurrentUser"/>.
///
/// Only the active Windows user can decrypt. Re-encryption is required if
/// the credential blob is moved to a different user profile.
/// </summary>
public sealed class SecureTokenStore : ITokenStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tastile", "Auth", "credentials.bin");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public async Task<TastileDesktop.Models.AuthSession?> LoadAsync()
    {
        if (!File.Exists(FilePath)) return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(FilePath).ConfigureAwait(false);
            var plain = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TastileDesktop.Models.AuthSession>(plain, JsonOptions);
        }
        catch
        {
            // DPAPI failure: user changed, profile corrupted, or blob tampered.
            return null;
        }
    }

    public async Task SaveAsync(TastileDesktop.Models.AuthSession session)
    {
        var dir = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("credentials.bin path is invalid.");
        Directory.CreateDirectory(dir);

        var plain = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
        var encrypted = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(FilePath, encrypted).ConfigureAwait(false);
    }

    public Task ClearAsync()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
        return Task.CompletedTask;
    }
}
