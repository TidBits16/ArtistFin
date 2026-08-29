using System.Globalization;
using System.Text.Json;
using Jellyfin.Plugin.ArtistFin.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArtistFin;

public sealed class ArtistLookupClient
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(14);

    private readonly PacedHttp _http;
    private readonly ILogger<ArtistLookupClient> _logger;

    public ArtistLookupClient(
        IHttpClientFactory factory,
        HttpCache cache,
        ILogger<ArtistLookupClient> logger)
    {
        _http = new PacedHttp(
            factory,
            cache,
            TimeSpan.FromMilliseconds(400),
            "ArtistFin/1.0.0 (https://github.com/TidBits16/ArtistFin)");
        _logger = logger;
    }

    public int HttpCount => _http.HttpCount;

    public int CacheHits => _http.CacheHits;

    public Task<ArtistProfile?> LookupAsync(string artistName, CancellationToken cancellationToken)
        => LookupAsync(artistName, null, cancellationToken);

    public async Task<ArtistProfile?> LookupAsync(
        string artistName,
        IReadOnlyList<ArtistDataProvider>? providers,
        CancellationToken cancellationToken)
    {
        if (ArtistNames.ShouldSkip(artistName))
        {
            return null;
        }

        var profile = new ArtistProfile { Name = artistName.Trim() };
        var enabled = providers is { Count: > 0 }
            ? providers
            : ArtistDataProviderCatalog.AllInOrder;

        var used = new List<string>();
        foreach (var provider in enabled)
        {
            switch (provider)
            {
                case ArtistDataProvider.MusicBrainz:
                    await EnrichFromMusicBrainzAsync(profile, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(profile.MusicBrainzId))
                    {
                        used.Add("musicbrainz");
                    }

                    break;
                case ArtistDataProvider.TheAudioDB:
                    await EnrichFromAudioDbAsync(profile, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(profile.AudioDbId))
                    {
                        used.Add("audiodb");
                    }

                    break;
                case ArtistDataProvider.Deezer:
                    await EnrichFromDeezerAsync(profile, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(profile.DeezerId))
                    {
                        used.Add("deezer");
                    }

                    break;
                case ArtistDataProvider.Wikipedia:
                {
                    var hadOverview = !string.IsNullOrWhiteSpace(profile.Overview);
                    await EnrichFromWikipediaAsync(profile, cancellationToken).ConfigureAwait(false);
                    if (!hadOverview && !string.IsNullOrWhiteSpace(profile.Overview))
                    {
                        used.Add("wikipedia");
                    }

                    break;
                }
            }
        }

        if (!profile.HasUsefulData)
        {
            return null;
        }

        profile.Source = string.Join('+', used.Distinct());
        return profile;
    }

    private async Task EnrichFromAudioDbAsync(ArtistProfile profile, CancellationToken cancellationToken)
    {
        JsonElement? payload;
        try
        {
            if (!string.IsNullOrWhiteSpace(profile.MusicBrainzId))
            {
                payload = await _http.GetJsonAsync(
                    "tadb/mb",
                    "https://www.theaudiodb.com/api/v1/json/2/artist-mb.php",
                    new Dictionary<string, string> { ["i"] = profile.MusicBrainzId! },
                    Ttl,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                payload = await _http.GetJsonAsync(
                    "tadb/search",
                    "https://www.theaudiodb.com/api/v1/json/2/search.php",
                    new Dictionary<string, string> { ["s"] = profile.Name },
                    Ttl,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TheAudioDB lookup failed for {Artist}", profile.Name);
            return;
        }

        var artist = FirstArtist(payload);
        if (artist is null)
        {
            return;
        }

        var remoteName = JsonUtil.Str(artist.Value, "strArtist");
        if (remoteName.Length > 0 && !ArtistNames.NamesMatch(profile.Name, remoteName))
        {
            return;
        }

        profile.AudioDbId = NullIfEmpty(JsonUtil.Str(artist.Value, "idArtist"));
        profile.MusicBrainzId ??= NullIfEmpty(JsonUtil.Str(artist.Value, "strMusicBrainzID"));
        profile.Homepage ??= NullIfEmpty(NormalizeUrl(JsonUtil.Str(artist.Value, "strWebsite")));
        profile.Hometown ??= NullIfEmpty(JsonUtil.Str(artist.Value, "strCountry"));
        profile.Overview ??= NullIfEmpty(PickBio(artist.Value));
        profile.PrimaryImageUrl ??= NullIfEmpty(JsonUtil.Str(artist.Value, "strArtistThumb"));
        profile.BackdropImageUrl ??= NullIfEmpty(JsonUtil.Str(artist.Value, "strArtistFanart"))
            ?? NullIfEmpty(JsonUtil.Str(artist.Value, "strArtistFanart2"));
        profile.LogoImageUrl ??= NullIfEmpty(JsonUtil.Str(artist.Value, "strArtistLogo"));

        var genre = JsonUtil.Str(artist.Value, "strGenre");
        if (genre.Length > 0 && !profile.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
        {
            profile.Genres.Add(genre);
        }

        profile.Formed ??= ParseYear(JsonUtil.Str(artist.Value, "intFormedYear"))
            ?? ParseYear(JsonUtil.Str(artist.Value, "intBornYear"));
        profile.Disbanded ??= ParseYear(JsonUtil.Str(artist.Value, "intDiedYear"));
    }

    private async Task EnrichFromDeezerAsync(ArtistProfile profile, CancellationToken cancellationToken)
    {
        JsonElement? payload;
        try
        {
            payload = await _http.GetJsonAsync(
                "deezer/artist",
                "https://api.deezer.com/search/artist",
                new Dictionary<string, string>
                {
                    ["q"] = profile.Name,
                    ["limit"] = "5"
                },
                Ttl,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Deezer artist search failed for {Artist}", profile.Name);
            return;
        }

        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!payload.Value.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        JsonElement? best = null;
        foreach (var item in data.EnumerateArray())
        {
            var name = JsonUtil.Str(item, "name");
            if (!ArtistNames.NamesMatch(profile.Name, name))
            {
                continue;
            }

            best = item;
            break;
        }

        if (best is null)
        {
            return;
        }

        var id = ((long)JsonUtil.Num(best.Value, "id")).ToString(CultureInfo.InvariantCulture);
        if (id != "0")
        {
            profile.DeezerId ??= id;
        }

        profile.PrimaryImageUrl ??= NullIfEmpty(JsonUtil.Str(best.Value, "picture_xl"))
            ?? NullIfEmpty(JsonUtil.Str(best.Value, "picture_big"))
            ?? NullIfEmpty(JsonUtil.Str(best.Value, "picture"));
    }

    private async Task EnrichFromMusicBrainzAsync(ArtistProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profile.MusicBrainzId))
            {
                var search = await _http.GetJsonAsync(
                    "mb/search",
                    "https://musicbrainz.org/ws/2/artist",
                    new Dictionary<string, string>
                    {
                        ["query"] = "artist:\"" + profile.Name.Replace('"', ' ') + "\"",
                        ["fmt"] = "json",
                        ["limit"] = "5"
                    },
                    Ttl,
                    cancellationToken).ConfigureAwait(false);

                var mbid = PickMusicBrainzId(search, profile.Name);
                if (mbid is null)
                {
                    return;
                }

                profile.MusicBrainzId = mbid;
            }

            var detail = await _http.GetJsonAsync(
                "mb/artist/" + profile.MusicBrainzId,
                "https://musicbrainz.org/ws/2/artist/" + profile.MusicBrainzId,
                new Dictionary<string, string>
                {
                    ["fmt"] = "json",
                    ["inc"] = "url-rels+genres+aliases"
                },
                Ttl,
                cancellationToken).ConfigureAwait(false);

            if (detail is null || detail.Value.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (detail.Value.TryGetProperty("area", out var area) && area.ValueKind == JsonValueKind.Object)
            {
                profile.Hometown ??= NullIfEmpty(JsonUtil.Str(area, "name"));
            }

            if (detail.Value.TryGetProperty("life-span", out var life) && life.ValueKind == JsonValueKind.Object)
            {
                profile.Formed ??= ParseDate(JsonUtil.Str(life, "begin"));
                profile.Disbanded ??= ParseDate(JsonUtil.Str(life, "end"));
            }

            if (detail.Value.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in genres.EnumerateArray())
                {
                    var name = JsonUtil.Str(g, "name");
                    if (name.Length > 0 && !profile.Genres.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        profile.Genres.Add(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name));
                    }
                }
            }

            if (detail.Value.TryGetProperty("relations", out var rels) && rels.ValueKind == JsonValueKind.Array)
            {
                foreach (var rel in rels.EnumerateArray())
                {
                    var type = JsonUtil.Str(rel, "type");
                    var url = string.Empty;
                    if (rel.TryGetProperty("url", out var urlObj) && urlObj.ValueKind == JsonValueKind.Object)
                    {
                        url = JsonUtil.Str(urlObj, "resource");
                    }

                    if (url.Length == 0)
                    {
                        continue;
                    }

                    if (type.Equals("official homepage", StringComparison.OrdinalIgnoreCase))
                    {
                        profile.Homepage ??= NormalizeUrl(url);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MusicBrainz lookup failed for {Artist}", profile.Name);
        }
    }

    private async Task EnrichFromWikipediaAsync(ArtistProfile profile, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(profile.Overview))
        {
            return;
        }

        var titles = new List<string> { profile.Name, profile.Name + " (band)", profile.Name + " (musician)" };
        foreach (var title in titles)
        {
            var extract = await FetchWikiSummaryAsync(title, cancellationToken).ConfigureAwait(false);
            if (extract is null)
            {
                continue;
            }

            profile.Overview = extract.Value.Extract;
            profile.PrimaryImageUrl ??= extract.Value.Thumbnail;
            return;
        }
    }

    private async Task<(string Extract, string? Thumbnail)?> FetchWikiSummaryAsync(
        string title,
        CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(title.Replace(' ', '_'));
        try
        {
            var payload = await _http.GetJsonAsync(
                "wiki/" + encoded,
                "https://en.wikipedia.org/api/rest_v1/page/summary/" + encoded,
                null,
                Ttl,
                cancellationToken).ConfigureAwait(false);

            if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var type = JsonUtil.Str(payload.Value, "type");
            if (type.Equals("disambiguation", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var extract = JsonUtil.Str(payload.Value, "extract").Trim();
            if (extract.Length < 40)
            {
                return null;
            }

            string? thumb = null;
            if (payload.Value.TryGetProperty("thumbnail", out var th) && th.ValueKind == JsonValueKind.Object)
            {
                thumb = NullIfEmpty(JsonUtil.Str(th, "source"));
            }
            else if (payload.Value.TryGetProperty("originalimage", out var oi) && oi.ValueKind == JsonValueKind.Object)
            {
                thumb = NullIfEmpty(JsonUtil.Str(oi, "source"));
            }

            return (extract, thumb);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Wikipedia summary failed for {Title}", title);
            return null;
        }
    }

    private static string? PickMusicBrainzId(JsonElement? search, string localName)
    {
        if (search is null || search.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!search.Value.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var a in artists.EnumerateArray())
        {
            var name = JsonUtil.Str(a, "name");
            if (ArtistNames.NamesMatch(localName, name))
            {
                return NullIfEmpty(JsonUtil.Str(a, "id"));
            }
        }

        return null;
    }

    private static JsonElement? FirstArtist(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!payload.Value.TryGetProperty("artists", out var artists)
            || artists.ValueKind != JsonValueKind.Array
            || artists.GetArrayLength() == 0)
        {
            return null;
        }

        return artists[0];
    }

    private static string PickBio(JsonElement artist)
    {
        foreach (var key in new[]
                 {
                     "strBiographyEN", "strBiography", "strBiographyDE", "strBiographyFR", "strBiographyES"
                 })
        {
            var bio = JsonUtil.Str(artist, key).Trim();
            if (bio.Length > 40)
            {
                return bio;
            }
        }

        return string.Empty;
    }

    private static DateTime? ParseYear(string raw)
    {
        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            && year is >= 1500 and <= 2100)
        {
            return new DateTime(year, 1, 1);
        }

        return null;
    }

    private static DateTime? ParseDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            return dt.Date;
        }

        return ParseYear(raw.Length >= 4 ? raw[..4] : raw);
    }

    private static string? NormalizeUrl(string url)
    {
        var u = (url ?? string.Empty).Trim();
        if (u.Length == 0)
        {
            return null;
        }

        if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            u = "https://" + u;
        }

        return u;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
