namespace Jellyfin.Plugin.ArtistFin.Configuration;

/// <summary>Remote sources ArtistFin can merge into an artist profile.</summary>
public enum ArtistDataProvider
{
    MusicBrainz,
    TheAudioDB,
    Deezer,
    Wikipedia,
}

public static class ArtistDataProviderCatalog
{
    /// <summary>Default merge order: IDs/details first, then images, then bio fallback.</summary>
    public static readonly ArtistDataProvider[] AllInOrder =
    [
        ArtistDataProvider.MusicBrainz,
        ArtistDataProvider.TheAudioDB,
        ArtistDataProvider.Deezer,
        ArtistDataProvider.Wikipedia,
    ];
}
