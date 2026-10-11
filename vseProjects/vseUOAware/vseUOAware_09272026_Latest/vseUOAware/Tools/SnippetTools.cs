using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Read-only C# snippet generator (ServUO/RunUO style Point3D list and Rectangle2D). Needs no map files.
/// </summary>
internal class SnippetTools
{
    private const int MaxCoordinates = 500;
    private static readonly Regex NumberPair = new(@"(-?\d+)\s*[, ]\s*(-?\d+)", RegexOptions.Compiled);

    [McpServerTool]
    [Description("Generates C# snippets (ServUO/RunUO style) from coordinates: a Point3D array and a Rectangle2D bounding box. Give either a coordinate list (one 'x,y' per line or separated by ';', max 500) or a rectangle (x, y, width, height). Read-only, uses no files.")]
    public string GenerateCodeSnippet(
        [Description("Name used for the generated C# variables, e.g. 'Spawns'.")] string name = "Points",
        [Description("Coordinates, e.g. '1432,1696' on each line or separated by ';'. Leave empty when using a rectangle.")] string? coordinates = null,
        [Description("Z value for every Point3D (default 0).")] int z = 0,
        [Description("Rectangle start X (used when no coordinate list is given).")] int x = 0,
        [Description("Rectangle start Y.")] int y = 0,
        [Description("Rectangle width in tiles (0 = not used).")] int width = 0,
        [Description("Rectangle height in tiles (0 = not used).")] int height = 0)
    {
        string id = Regex.Replace(name ?? "", @"[^A-Za-z0-9_]", "");
        if (id.Length == 0 || char.IsDigit(id[0])) id = "Points";
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(coordinates))
        {
            var pts = NumberPair.Matches(coordinates).Select(m => (X: int.Parse(m.Groups[1].Value), Y: int.Parse(m.Groups[2].Value))).ToList();
            if (pts.Count == 0) return "No 'x,y' pairs found in the text.";
            if (pts.Count > MaxCoordinates) return $"Too many coordinates ({pts.Count}); the maximum is {MaxCoordinates}.";
            sb.AppendLine($"private static readonly Point3D[] {id} = new Point3D[]");
            sb.AppendLine("{");
            foreach (var (px, py) in pts) sb.AppendLine($"    new Point3D({px}, {py}, {z}),");
            sb.AppendLine("};");
            int minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y), maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
            sb.AppendLine();
            sb.AppendLine($"private static readonly Rectangle2D {id}Bounds = new Rectangle2D({minX}, {minY}, {maxX - minX + 1}, {maxY - minY + 1});");
            return sb.ToString();
        }

        if (width > 0 && height > 0)
        {
            sb.AppendLine($"private static readonly Rectangle2D {id}Bounds = new Rectangle2D({x}, {y}, {width}, {height});");
            sb.AppendLine();
            sb.AppendLine($"private static readonly Point3D {id}Center = new Point3D({x + width / 2}, {y + height / 2}, {z});");
            return sb.ToString();
        }

        return "Give either a coordinate list or a rectangle (width and height greater than 0).";
    }
}
