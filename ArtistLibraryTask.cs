using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArtistTagShelf;

public class ArtistLibraryTask : IScheduledTask
{
    private readonly ArtistEngine _engine;
    private readonly ILogger<ArtistLibraryTask> _logger;

    public ArtistLibraryTask(ArtistEngine engine, ILogger<ArtistLibraryTask> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public string Name => "- ArtistTagShelf: Know Your Artists";

    public string Key => "ArtistTagShelfLibrary";

    public string Description =>
        "Fills missing artist bios, images, and profile details. A full refresh from plugin settings uses this same task and overwrites existing data.";

    public string Category => "Library";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.RunAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArtistTagShelf failed");
            throw;
        }
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.WeeklyTrigger,
                DayOfWeek = DayOfWeek.Sunday,
                TimeOfDayTicks = TimeSpan.FromHours(5).Ticks
            }
        ];
    }
}
