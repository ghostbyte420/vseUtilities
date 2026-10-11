using System.Text.Json;

namespace vseMediaSearch.Services;

/// <summary>
/// Remembers an internal list of media/website "sources" (e.g. CNN.com, YouTube.com, w3schools.com)
/// that the search tools are allowed to fetch from. Saved as a small JSON file in the user's
/// "app data" folder, same pattern as vseUOAware's DirectoryService, so the list persists
/// across sessions without needing to be retyped every time.
/// </summary>
internal static class SiteListService
{
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseMediaSearch",
        "sites.json");

    private sealed class SavedConfig
    {
        public List<string> Sites { get; set; } = [];
    }

    // Reasonable free/educational defaults to seed a brand-new install with.
    private static readonly string[] DefaultSites =
    [
        "cnn.com",
        "youtube.com",
        "w3schools.com",
        "developer.mozilla.org",
        "wikipedia.org",
    ];

    /// <summary>Reads the saved site list, seeding it with sensible defaults on first run.</summary>
    public static List<string> LoadSites()
    {
        if (!File.Exists(ConfigFilePath))
        {
            var seeded = new List<string>(DefaultSites);
            SaveSites(seeded);
            return seeded;
        }

        var json = File.ReadAllText(ConfigFilePath);
        var config = JsonSerializer.Deserialize<SavedConfig>(json);
        return config?.Sites ?? [];
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>Overwrites the saved site list.</summary>
    public static void SaveSites(List<string> sites)
    {
        var folder = Path.GetDirectoryName(ConfigFilePath)!;
        Directory.CreateDirectory(folder);

        var config = new SavedConfig { Sites = sites };
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>Adds a site (domain) to the list if it isn't already present.</summary>
    public static List<string> AddSite(string domain)
    {
        var normalized = NormalizeDomain(domain);
        var sites = LoadSites();
        if (!sites.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            sites.Add(normalized);
            SaveSites(sites);
        }
        return sites;
    }

    /// <summary>Removes a site (domain) from the list.</summary>
    public static List<string> RemoveSite(string domain)
    {
        var normalized = NormalizeDomain(domain);
        var sites = LoadSites();
        sites.RemoveAll(s => string.Equals(s, normalized, StringComparison.OrdinalIgnoreCase));
        SaveSites(sites);
        return sites;
    }

    /// <summary>True if the given URL's host matches (or is a subdomain of) an allowed site.</summary>
    public static bool IsAllowed(Uri uri)
    {
        var host = uri.Host;
        var sites = LoadSites();
        return sites.Any(site =>
            host.Equals(site, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + site, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDomain(string domain)
    {
        domain = domain.Trim().ToLowerInvariant();
        if (domain.StartsWith("http://", StringComparison.Ordinal) || domain.StartsWith("https://", StringComparison.Ordinal))
        {
            domain = new Uri(domain).Host;
        }
        return domain.StartsWith("www.", StringComparison.Ordinal) ? domain[4..] : domain;
    }
}
