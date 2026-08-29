using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArtistFin;

public class ArtistLibraryTask : IScheduledTask
{
    private readonly ArtistEngine _engine;
    private readonly ILogger<ArtistLibraryTask> _logger;

    public ArtistLibraryTask(ArtistEngine engine, ILogger<ArtistLibraryTask> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public string Name => "ArtistFin: Know Your Artists";

    public string Key => "ArtistFinLibrary";

    public string Description =>
        "Fills missing artist bios, images, and profile details (Wikipedia, Deezer, TheAudioDB, MusicBrainz).";

    public string Category => "Library";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.RunAsync(force: false, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArtistFin failed");
            throw;
        }
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromDays(7).Ticks
            }
        ];
    }
}
