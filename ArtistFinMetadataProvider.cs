using System.Net.Http;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.ArtistFin;

/// <summary>Native Jellyfin metadata provider so Identify/Refresh Artist also uses ArtistFin.</summary>
public sealed class ArtistFinMetadataProvider : IRemoteMetadataProvider<MusicArtist, ArtistInfo>, IHasOrder
{
    private readonly ArtistLookupClient _lookup;
    private readonly IHttpClientFactory _http;

    public ArtistFinMetadataProvider(ArtistLookupClient lookup, IHttpClientFactory http)
    {
        _lookup = lookup;
        _http = http;
    }

    public string Name => "ArtistFin";

    public int Order => 2;

    public async Task<MetadataResult<MusicArtist>> GetMetadata(ArtistInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<MusicArtist>();
        var name = info.Name ?? string.Empty;
        if (ArtistNames.ShouldSkip(name))
        {
            return result;
        }

        var providers = Plugin.Instance?.Configuration.EffectiveDataProviders;
        var profile = await _lookup.LookupAsync(name, providers, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return result;
        }

        var item = new MusicArtist { Name = profile.Name };
        if (!string.IsNullOrWhiteSpace(profile.Overview))
        {
            item.Overview = profile.Overview;
        }

        if (!string.IsNullOrWhiteSpace(profile.Hometown))
        {
            item.ProductionLocations = [profile.Hometown];
        }

        if (!string.IsNullOrWhiteSpace(profile.Homepage))
        {
            item.HomePageUrl = profile.Homepage;
        }

        if (profile.Formed is not null)
        {
            item.PremiereDate = profile.Formed;
            item.ProductionYear = profile.Formed.Value.Year;
        }

        if (profile.Disbanded is not null)
        {
            item.EndDate = profile.Disbanded;
        }

        if (profile.Genres.Count > 0)
        {
            item.Genres = profile.Genres.ToArray();
        }

        if (!string.IsNullOrWhiteSpace(profile.MusicBrainzId))
        {
            item.SetProviderId(MetadataProvider.MusicBrainzArtist, profile.MusicBrainzId);
        }

        if (!string.IsNullOrWhiteSpace(profile.AudioDbId))
        {
            item.SetProviderId(MetadataProvider.AudioDbArtist, profile.AudioDbId);
        }

        result.Item = item;
        result.HasMetadata = true;
        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        ArtistInfo searchInfo,
        CancellationToken cancellationToken)
    {
        var name = searchInfo.Name ?? string.Empty;
        if (ArtistNames.ShouldSkip(name))
        {
            return [];
        }

        var providers = Plugin.Instance?.Configuration.EffectiveDataProviders;
        var profile = await _lookup.LookupAsync(name, providers, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return [];
        }

        var remote = new RemoteSearchResult
        {
            Name = profile.Name,
            Overview = profile.Overview,
            PremiereDate = profile.Formed,
            ImageUrl = profile.PrimaryImageUrl,
            SearchProviderName = Name
        };

        if (!string.IsNullOrWhiteSpace(profile.MusicBrainzId))
        {
            remote.SetProviderId(MetadataProvider.MusicBrainzArtist, profile.MusicBrainzId);
        }

        if (!string.IsNullOrWhiteSpace(profile.AudioDbId))
        {
            remote.SetProviderId(MetadataProvider.AudioDbArtist, profile.AudioDbId);
        }

        return [remote];
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _http.CreateClient().GetAsync(url, cancellationToken);
}
