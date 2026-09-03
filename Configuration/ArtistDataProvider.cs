namespace Jellyfin.Plugin.ArtistFin.Configuration;

/// <summary>Remote sources ArtistFin can merge into an artist profile.</summary>
public enum ArtistDataProvider
{
    MusicBrainz,
    LastFm,
    Deezer,
    TheAudioDB,
    Wikipedia,
}

public static class ArtistDataProviderCatalog
{
    /// <summary>Default merge order: IDs, Last.fm bio, Deezer portrait, then AudioDB extras, Wikipedia fallback.</summary>
    public static readonly ArtistDataProvider[] AllInOrder =
    [
        ArtistDataProvider.MusicBrainz,
        ArtistDataProvider.LastFm,
        ArtistDataProvider.Deezer,
        ArtistDataProvider.TheAudioDB,
        ArtistDataProvider.Wikipedia,
    ];
}
