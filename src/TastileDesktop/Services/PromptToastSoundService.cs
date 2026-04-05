using Windows.Media.Core;
using Windows.Media.Playback;

namespace TastileDesktop.Services;

public sealed class PromptToastSoundService : IDisposable
{
    public static PromptToastSoundService Instance { get; } = new();

    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private CancellationTokenSource? _playbackCts;

    public void TriggerFromPromptToast(TastileSettings settings)
    {
        _ = PlayAsync(settings);
    }

    public void Stop()
    {
        _playbackCts?.Cancel();
    }

    public async Task PlayAsync(TastileSettings settings, CancellationToken cancellationToken = default)
    {
        var plan = PromptToastSoundPlanBuilder.Create(
            settings.PromptToastSoundEnabled,
            settings.PromptToastSoundSource,
            settings.PromptToastSoundFilePath,
            settings.PromptToastSoundPlaybackMode,
            settings.PromptToastSoundDurationSeconds,
            settings.PromptToastSoundRepeatCount,
            settings.PromptToastSoundRepeatIntervalSeconds);

        if (!plan.Enabled)
        {
            return;
        }

        CancellationTokenSource ctsToDispose;
        CancellationToken token;
        await _playbackGate.WaitAsync(cancellationToken);
        try
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ctsToDispose = _playbackCts;
            token = _playbackCts.Token;
        }
        finally
        {
            _playbackGate.Release();
        }

        try
        {
            if (string.Equals(plan.PlaybackMode, PromptToastSoundPlaybackModes.UntilPromptResponse, StringComparison.Ordinal))
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    await PlayOnceAsync(plan, token);
                    await Task.Delay(TimeSpan.FromSeconds(plan.RepeatIntervalSeconds), token);
                }
            }
            else
            {
                for (var i = 0; i < plan.RepeatCount; i++)
                {
                    token.ThrowIfCancellationRequested();
                    await PlayOnceAsync(plan, token);
                    if (i + 1 < plan.RepeatCount)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(plan.RepeatIntervalSeconds), token);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            App.DebugLog($"[PromptToastSound] Playback failed: {ex.Message}");
        }
        finally
        {
            await _playbackGate.WaitAsync(CancellationToken.None);
            try
            {
                if (ReferenceEquals(_playbackCts, ctsToDispose))
                {
                    _playbackCts = null;
                }
            }
            finally
            {
                _playbackGate.Release();
            }

            ctsToDispose.Dispose();
        }
    }

    private static Task PlayOnceAsync(PromptToastSoundPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Source == PromptToastSoundSources.CustomFile
            && !string.IsNullOrWhiteSpace(plan.FilePath)
            && File.Exists(plan.FilePath))
        {
            return PlayCustomFileAsync(plan.FilePath, plan.DurationSeconds, cancellationToken);
        }

        return PlaySystemBeepAsync(plan.DurationSeconds, cancellationToken);
    }

    private static async Task PlaySystemBeepAsync(int durationSeconds, CancellationToken cancellationToken)
    {
        var total = TimeSpan.FromSeconds(durationSeconds);
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < total)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Console.Beep(880, 120);
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            await Task.Delay(350, cancellationToken);
        }
    }

    private static async Task PlayCustomFileAsync(string filePath, int durationSeconds, CancellationToken cancellationToken)
    {
        using var player = new MediaPlayer();
        player.Source = MediaSource.CreateFromUri(new Uri(filePath));
        player.Play();
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken);
        player.Pause();
    }

    public void Dispose()
    {
        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
        _playbackCts = null;
        _playbackGate.Dispose();
    }
}
