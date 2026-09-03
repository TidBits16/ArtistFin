using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ArtistFin.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArtistFin;

public class ArtistEngine
{
    private readonly ILibraryManager _library;
    private readonly IProviderManager _providers;
    private readonly ArtistLookupClient _lookup;
    private readonly ILogger<ArtistEngine> _logger;
    private int _forceNext;

    public ArtistEngine(
        ILibraryManager library,
        IProviderManager providers,
        ArtistLookupClient lookup,
        ILogger<ArtistEngine> logger)
    {
        _library = library;
        _providers = providers;
        _lookup = lookup;
        _logger = logger;
    }

    /// <summary>Next scheduled run overwrites existing artist data (settings button).</summary>
    public void RequestForce() => Interlocked.Exchange(ref _forceNext, 1);

    public Task<ArtistRunResult> RunAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var force = Interlocked.Exchange(ref _forceNext, 0) == 1;
        return RunAsync(force, progress, cancellationToken);
    }

    /// <param name="force">When true, overwrite existing overview/images/details.</param>
    public async Task<ArtistRunResult> RunAsync(
        bool force,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var workers = Math.Clamp(cfg.Workers <= 0 ? 1 : cfg.Workers, 1, 4);

        var artists = _library.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.MusicArtist],
            Recursive = true
        }).OfType<MusicArtist>()
            .Where(a => a.Id != Guid.Empty && !ArtistNames.ShouldSkip(a.Name))
            .ToList();

        var targets = force
            ? artists
            : artists.Where(NeedsWork).ToList();

        _logger.LogInformation(
            "ArtistFin: {Targets}/{Total} artists ({Mode}), providers {Providers}, {Workers} workers",
            targets.Count,
            artists.Count,
            force ? "force all" : "missing only",
            string.Join(" --> ", cfg.EffectiveDataProviders),
            workers);

        var updated = 0;
        var skipped = artists.Count - targets.Count;
        var failed = 0;
        var done = 0;
        var total = Math.Max(1, targets.Count);

        using var gate = new SemaphoreSlim(workers, workers);
        await Task.WhenAll(targets.Select(async artist =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ok = await ProcessArtistAsync(artist, force, cfg, cancellationToken).ConfigureAwait(false);
                if (ok)
                {
                    Interlocked.Increment(ref updated);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                _logger.LogWarning(ex, "ArtistFin failed on {Id} ({Name})", artist.Id, artist.Name);
            }
            finally
            {
                var n = Interlocked.Increment(ref done);
                progress.Report(100.0 * n / total);
                gate.Release();
            }
        })).ConfigureAwait(false);

        progress.Report(100);
        _logger.LogInformation(
            "ArtistFin finished: updated {Updated}, no match {Missed}, skipped {Skipped}, http {Http}/{Cache} cache",
            updated,
            failed,
            skipped,
            _lookup.HttpCount,
            _lookup.CacheHits);

        return new ArtistRunResult(updated, failed, skipped);
    }

    private static bool NeedsWork(MusicArtist artist)
    {
        var missingOverview = string.IsNullOrWhiteSpace(artist.Overview);
        var missingImage = !artist.HasImage(ImageType.Primary, 0);
        return missingOverview || missingImage;
    }

    private async Task<bool> ProcessArtistAsync(
        MusicArtist artist,
        bool force,
        PluginConfiguration cfg,
        CancellationToken cancellationToken)
    {
        var profile = await _lookup.LookupAsync(
                artist.Name ?? string.Empty,
                cfg.EffectiveDataProviders,
                cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return false;
        }

        var changed = false;

        if (!string.IsNullOrWhiteSpace(profile.Overview)
            && (force || string.IsNullOrWhiteSpace(artist.Overview)))
        {
            artist.Overview = profile.Overview;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Hometown)
            && (force || artist.ProductionLocations is not { Length: > 0 }))
        {
            artist.ProductionLocations = [profile.Hometown!];
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Homepage)
            && (force || string.IsNullOrWhiteSpace(artist.HomePageUrl)))
        {
            artist.HomePageUrl = profile.Homepage;
            changed = true;
        }

        if (profile.Formed is not null && (force || artist.PremiereDate is null))
        {
            artist.PremiereDate = profile.Formed;
            artist.ProductionYear = profile.Formed.Value.Year;
            changed = true;
        }

        if (profile.Disbanded is not null && (force || artist.EndDate is null))
        {
            artist.EndDate = profile.Disbanded;
            changed = true;
        }

        if (profile.Genres.Count > 0)
        {
            var existing = artist.Genres ?? [];
            var merged = existing
                .Concat(profile.Genres)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (force || merged.Length > existing.Length)
            {
                artist.Genres = merged;
                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.MusicBrainzId)
            && string.IsNullOrWhiteSpace(artist.GetProviderId(MetadataProvider.MusicBrainzArtist)))
        {
            artist.SetProviderId(MetadataProvider.MusicBrainzArtist, profile.MusicBrainzId);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(profile.AudioDbId)
            && string.IsNullOrWhiteSpace(artist.GetProviderId(MetadataProvider.AudioDbArtist)))
        {
            artist.SetProviderId(MetadataProvider.AudioDbArtist, profile.AudioDbId);
            changed = true;
        }

        if (changed)
        {
            await _library.UpdateItemAsync(
                artist,
                artist.GetParent() ?? artist,
                ItemUpdateType.MetadataEdit,
                cancellationToken).ConfigureAwait(false);
        }

        var imagesSaved = 0;
        imagesSaved += await TrySaveImageAsync(
            artist,
            profile.PrimaryImageUrl,
            ImageType.Primary,
            force,
            cancellationToken).ConfigureAwait(false) ? 1 : 0;
        imagesSaved += await TrySaveImageAsync(
            artist,
            profile.BackdropImageUrl,
            ImageType.Backdrop,
            force,
            cancellationToken).ConfigureAwait(false) ? 1 : 0;
        imagesSaved += await TrySaveImageAsync(
            artist,
            profile.LogoImageUrl,
            ImageType.Logo,
            force,
            cancellationToken).ConfigureAwait(false) ? 1 : 0;

        if (changed || imagesSaved > 0)
        {
            _logger.LogInformation(
                "ArtistFin updated {Id}: {Name} ({Source})",
                artist.Id,
                artist.Name,
                profile.Source);
            return true;
        }

        return false;
    }

    private async Task<bool> TrySaveImageAsync(
        MusicArtist artist,
        string? url,
        ImageType type,
        bool force,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!force && artist.HasImage(type, 0))
        {
            return false;
        }

        try
        {
            await _providers.SaveImage(artist, url, type, null, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ArtistFin could not save {Type} image for {Name}", type, artist.Name);
            return false;
        }
    }
}

public readonly record struct ArtistRunResult(int Updated, int Missed, int Skipped);
