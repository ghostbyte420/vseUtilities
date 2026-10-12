using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace vseUOAware.Services;

/// <summary>
/// Shared helper for the "read the C# source" tools. It finds .cs files (skipping build and test-evidence folders)
/// and reads each file line by line with the comments removed, so the tools only look at real code.
/// Nothing here writes to disk.
/// </summary>
internal static class SourceScan
{
    private const int MaxFolderDepth = 25;

    private static readonly string[] SkippedFolders =
        { "bin", "obj", ".git", ".vs", "node_modules", "packages", "evidence", "TestResults" };

    /// <summary>Matches a quoted text that looks like a data file name, such as "Terrain.xml".</summary>
    public static readonly Regex DataFileRx = new(
        @"""([^""\\]*\.(?:xml|bmp|mul|uop|csv|json|txt|idx|bin|def|png|log))""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void Add(StringBuilder sb, FormattableString text) =>
        sb.AppendLine(text.ToString(CultureInfo.InvariantCulture));

    public static List<string> CsFiles(string root, bool includeDesigner)
    {
        var found = new List<string>();
        Walk(root, found, 0, includeDesigner);
        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    private static void Walk(string dir, List<string> found, int depth, bool includeDesigner)
    {
        if (depth > MaxFolderDepth) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
            {
                if (!includeDesigner && file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(file);
            }
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (SkippedFolders.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                Walk(sub, found, depth + 1, includeDesigner);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // A folder we are not allowed to open is skipped on purpose; the rest of the scan is still valid.
        }
        catch (DirectoryNotFoundException)
        {
            // A folder that vanished while scanning is skipped on purpose.
        }
    }

    /// <summary>The part of the program a file lives in: its first two folder names, e.g. "MapCreator.Engine\Compiler".</summary>
    public static string Area(string relativePath)
    {
        var parts = relativePath.Split('\\', '/');
        if (parts.Length <= 1) return "(top folder)";
        return parts.Length == 2 ? parts[0] : parts[0] + "\\" + parts[1];
    }

    /// <summary>Returns every line of the file with comments removed. Text inside quotes is kept as is.</summary>
    public static List<(int Line, string Code)> CodeLines(string path)
    {
        string text = File.ReadAllText(path);
        var lines = new List<(int, string)>();
        var sb = new StringBuilder();
        int line = 1;
        bool inBlock = false, inString = false, inVerbatim = false, inChar = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '\r') continue;
            if (c == '\n')
            {
                lines.Add((line, sb.ToString()));
                sb.Clear();
                line++;
                if (!inVerbatim) inString = false;
                inChar = false;
                continue;
            }
            if (inBlock)
            {
                if (c == '*' && next == '/') { inBlock = false; i++; }
                continue;
            }
            if (inString)
            {
                sb.Append(c);
                if (inVerbatim)
                {
                    if (c == '"')
                    {
                        if (next == '"') { sb.Append(next); i++; }
                        else { inString = false; inVerbatim = false; }
                    }
                }
                else if (c == '\\' && next != '\0' && next != '\n') { sb.Append(next); i++; }
                else if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                sb.Append(c);
                if (c == '\\' && next != '\0' && next != '\n') { sb.Append(next); i++; }
                else if (c == '\'') inChar = false;
                continue;
            }
            if (c == '/' && next == '/')
            {
                while (i + 1 < text.Length && text[i + 1] != '\n') i++;
                continue;
            }
            if (c == '/' && next == '*') { inBlock = true; i++; continue; }
            if (c == '@' && next == '"') { inString = true; inVerbatim = true; sb.Append(c).Append(next); i++; continue; }
            if (c == '"') { inString = true; inVerbatim = false; sb.Append(c); continue; }
            if (c == '\'') { inChar = true; sb.Append(c); continue; }
            sb.Append(c);
        }
        lines.Add((line, sb.ToString()));
        return lines;
    }
}
