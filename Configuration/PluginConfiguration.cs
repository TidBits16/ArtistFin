using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ArtistFin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Concurrent artist workers. 0 = 1. Cap low to stay polite to Wikipedia/MusicBrainz.</summary>
    public int Workers { get; set; } = 1;

    /// <summary>Last.fm API key for artist bios (create at https://www.last.fm/api/account/create).</summary>
    public string LastFmApiKey { get; set; } = string.Empty;

    public bool WriteBios { get; set; } = true;

    public bool WritePrimaryImages { get; set; } = true;

    public bool WriteBackdrops { get; set; } = true;

    public bool WriteLogos { get; set; } = true;

    public bool WriteHometown { get; set; } = true;

    public bool WriteDates { get; set; } = true;

    public bool WriteGenres { get; set; } = true;

    public bool WriteWebsite { get; set; } = true;

    /// <summary>All providers in UI order (checked and unchecked).</summary>
    public ArtistDataProvider[] DataProviderOrder { get; set; } = [];

    /// <summary>Checked providers to query, in merge order (first-wins per field).</summary>
    public ArtistDataProvider[] DataProviders { get; set; } = [];

    public IReadOnlyList<ArtistDataProvider> EffectiveDataProviderOrder
    {
        get
        {
            if (DataProviderOrder is { Length: > 0 })
            {
                return NormalizeProviderOrder(DataProviderOrder);
            }

            if (DataProviders is { Length: > 0 })
            {
                return NormalizeProviderOrder(DataProviders);
            }

            return ArtistDataProviderCatalog.AllInOrder;
        }
    }

    public IReadOnlyList<ArtistDataProvider> EffectiveDataProviders
    {
        get
        {
            if (DataProviders is { Length: > 0 })
            {
                var enabled = new HashSet<ArtistDataProvider>(DataProviders);
                var ordered = EffectiveDataProviderOrder.Where(enabled.Contains).ToList();
                return ordered.Count > 0 ? ordered : ArtistDataProviderCatalog.AllInOrder;
            }

            return ArtistDataProviderCatalog.AllInOrder;
        }
    }

    private static IReadOnlyList<ArtistDataProvider> NormalizeProviderOrder(
        IEnumerable<ArtistDataProvider> order)
    {
        var list = new List<ArtistDataProvider>();
        var seen = new HashSet<ArtistDataProvider>();
        foreach (var provider in order)
        {
            if (seen.Add(provider))
            {
                list.Add(provider);
            }
        }

        foreach (var provider in ArtistDataProviderCatalog.AllInOrder)
        {
            if (seen.Add(provider))
            {
                list.Add(provider);
            }
        }

        return list;
    }
}
