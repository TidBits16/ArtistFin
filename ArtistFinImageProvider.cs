using System.Net.Http;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.ArtistFin;

public sealed class ArtistFinImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly ArtistLookupClient _lookup;
    private readonly IHttpClientFactory _http;

    public ArtistFinImageProvider(ArtistLookupClient lookup, IHttpClientFactory http)
    {
        _lookup = lookup;
        _http = http;
    }

    public string Name => "ArtistFin";

    public int Order => 2;

    public bool Supports(BaseItem item) => item is MusicArtist;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null || cfg.WritePrimaryImages)
        {
            yield return ImageType.Primary;
        }

        if (cfg is null || cfg.WriteBackdrops)
        {
            yield return ImageType.Backdrop;
        }

        if (cfg is null || cfg.WriteLogos)
        {
            yield return ImageType.Logo;
        }
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicArtist artist || ArtistNames.ShouldSkip(artist.Name))
        {
            return [];
        }

        var cfg = Plugin.Instance?.Configuration;
        var providers = cfg?.EffectiveDataProviders;
        var profile = await _lookup.LookupAsync(artist.Name ?? string.Empty, providers, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return [];
        }

        var list = new List<RemoteImageInfo>();
        if (cfg is null || cfg.WritePrimaryImages)
        {
            Add(list, profile.PrimaryImageUrl, ImageType.Primary);
        }

        if (cfg is null || cfg.WriteBackdrops)
        {
            Add(list, profile.BackdropImageUrl, ImageType.Backdrop);
        }

        if (cfg is null || cfg.WriteLogos)
        {
            Add(list, profile.LogoImageUrl, ImageType.Logo);
        }

        return list;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _http.CreateClient().GetAsync(url, cancellationToken);

    private void Add(List<RemoteImageInfo> list, string? url, ImageType type)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        list.Add(new RemoteImageInfo
        {
            ProviderName = Name,
            Url = url,
            Type = type
        });
    }
}
