namespace Jellyfin.Plugin.ArtistFin;

public sealed class ArtistProfile
{
    public string Name { get; init; } = string.Empty;

    public string? Overview { get; set; }

    public string? MusicBrainzId { get; set; }

    public string? AudioDbId { get; set; }

    public string? DeezerId { get; set; }

    public string? Homepage { get; set; }

    public string? Hometown { get; set; }

    public DateTime? Formed { get; set; }

    public DateTime? Disbanded { get; set; }

    public List<string> Genres { get; } = [];

    public string? PrimaryImageUrl { get; set; }

    public string? BackdropImageUrl { get; set; }

    public string? LogoImageUrl { get; set; }

    public string Source { get; set; } = string.Empty;

    public bool HasUsefulData
        => !string.IsNullOrWhiteSpace(Overview)
           || !string.IsNullOrWhiteSpace(PrimaryImageUrl)
           || !string.IsNullOrWhiteSpace(BackdropImageUrl)
           || !string.IsNullOrWhiteSpace(Homepage)
           || !string.IsNullOrWhiteSpace(Hometown)
           || Formed is not null
           || Genres.Count > 0;
}

public static class ArtistNames
{
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "various artists",
        "various artist",
        "va",
        "unknown",
        "unknown artist",
        "[unknown]",
        "soundtrack",
        "original soundtrack"
    };

    public static bool ShouldSkip(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        return n.Length == 0 || Skip.Contains(n);
    }

    public static string Norm(string name)
    {
        var s = (name ?? string.Empty).Trim().ToLowerInvariant();
        while (s.Contains("  ", StringComparison.Ordinal))
        {
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        }

        return s;
    }

    public static bool NamesMatch(string local, string remote)
    {
        var a = Norm(local);
        var b = Norm(remote);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        // Allow "The X" ↔ "X"
        if (a.StartsWith("the ", StringComparison.Ordinal) && a[4..] == b)
        {
            return true;
        }

        if (b.StartsWith("the ", StringComparison.Ordinal) && b[4..] == a)
        {
            return true;
        }

        return false;
    }
}
