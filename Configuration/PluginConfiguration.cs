using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ArtistFin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Concurrent artist workers. 0 = 1. Cap low to stay polite to Wikipedia/MusicBrainz.</summary>
    public int Workers { get; set; } = 1;

    public bool FillOverview { get; set; } = true;

    public bool FillImages { get; set; } = true;

    public bool FillGenres { get; set; } = true;

    public bool FillHometown { get; set; } = true;

    public bool FillDates { get; set; } = true;

    public bool FillHomepage { get; set; } = true;
}
