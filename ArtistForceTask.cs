using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArtistFin;

/// <summary>Manual-only force refresh (queued from settings).</summary>
public class ArtistForceTask : IScheduledTask
{
    private readonly ArtistEngine _engine;
    private readonly ILogger<ArtistForceTask> _logger;

    public ArtistForceTask(ArtistEngine engine, ILogger<ArtistForceTask> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public string Name => "ArtistFin: Refresh All Artists";

    public string Key => "ArtistFinForceAll";

    public string Description =>
        "Force-refresh artist bios, images, and profile details for every music artist.";

    public string Category => "Library";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.RunAsync(force: true, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArtistFin force refresh failed");
            throw;
        }
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
