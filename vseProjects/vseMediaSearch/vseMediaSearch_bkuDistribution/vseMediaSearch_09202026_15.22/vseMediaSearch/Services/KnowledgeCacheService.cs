using System.Text.Json;

namespace vseMediaSearch.Services;

/// <summary>
/// Persists extracted knowledge (article text, video transcripts) to disk so the same
/// page/video is never re-fetched or re-transcribed in a later session — this is the
/// "persistent project knowledge" store (Rule 8A) that lets learning actually compound.
/// </summary>
internal static class KnowledgeCacheService
{
    // Keep retained content beside downloaded media so users can find and remove it.
    private static readonly string CacheFolder = Path.Combine(
        DownloadFolderResolver.GetDownloadsFolder(),
        "vseMediaSearch",
        "Knowledge");

    /// <summary>Default freshness window: cached entries older than this are considered stale.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(30);

    /// <summary>Exposes the cache folder path so other services (e.g. knowledge search) can scan it.</summary>
    public static string GetCacheFolder()
    {
        Directory.CreateDirectory(CacheFolder);
        return CacheFolder;
    }

    public sealed record CachedEntry(
        string SourceUrl,
        string Title,
        string Content,
        DateTimeOffset CachedAtUtc,
        string? OnScreenText = null,
        IReadOnlyList<string>? CodeBlocks = null);

    /// <summary>
    /// Loads a cached entry if one exists AND it is not older than <paramref name="maxAge"/>
    /// (defaults to <see cref="DefaultMaxAge"/>). A stale entry returns null, the same as a
    /// missing one, so callers naturally re-fetch/re-process without extra branching.
    /// </summary>
    public static CachedEntry? TryLoad(string cacheKey, TimeSpan? maxAge = null)
    {
        var path = GetPath(cacheKey);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        var entry = JsonSerializer.Deserialize<CachedEntry>(json);

        if (entry is not null && IsStale(entry, maxAge))
        {
            return null;
        }

        return entry;
    }

    /// <summary>Returns true if the given cached entry is older than <paramref name="maxAge"/> (or <see cref="DefaultMaxAge"/>).</summary>
    public static bool IsStale(CachedEntry entry, TimeSpan? maxAge = null)
    {
        var effectiveMaxAge = maxAge ?? DefaultMaxAge;
        return DateTimeOffset.UtcNow - entry.CachedAtUtc > effectiveMaxAge;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static void Save(string cacheKey, CachedEntry entry)
    {
        Directory.CreateDirectory(CacheFolder);
        var json = JsonSerializer.Serialize(entry, SerializerOptions);
        File.WriteAllText(GetPath(cacheKey), json);
    }

    private static string GetPath(string cacheKey)
    {
        var safeKey = string.Concat(cacheKey.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        return Path.Combine(CacheFolder, safeKey + ".json");
    }

    /// <summary>
    /// Loads every cached entry currently on disk, paired with its cache key (derived from the
    /// file name), for tools that need to search across all previously learned knowledge.
    /// Corrupt/unreadable entries are skipped rather than throwing, since this reads
    /// user-writable disk state that could be manually edited or partially written.
    /// </summary>
    public static IReadOnlyList<(string CacheKey, CachedEntry Entry)> LoadAll()
    {
        var folder = GetCacheFolder();
        var results = new List<(string, CachedEntry)>();

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<CachedEntry>(json);
                if (entry is not null)
                {
                    results.Add((Path.GetFileNameWithoutExtension(file), entry));
                }
            }
            catch (JsonException)
            {
                // Skip corrupt/partially-written cache files rather than failing the whole search.
            }
        }

        return results;
    }
}
