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
        => [ImageType.Primary, ImageType.Backdrop, ImageType.Logo];

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicArtist artist || ArtistNames.ShouldSkip(artist.Name))
        {
            return [];
        }

        var profile = await _lookup.LookupAsync(artist.Name ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return [];
        }

        var list = new List<RemoteImageInfo>();
        Add(list, profile.PrimaryImageUrl, ImageType.Primary);
        Add(list, profile.BackdropImageUrl, ImageType.Backdrop);
        Add(list, profile.LogoImageUrl, ImageType.Logo);
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
