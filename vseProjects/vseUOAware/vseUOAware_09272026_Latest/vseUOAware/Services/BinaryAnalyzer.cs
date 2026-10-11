using System.Text;

namespace vseUOAware.Services;

/// <summary>
/// Read-only helpers that identify what kind of file something is from its
/// header bytes, decode Windows program headers by hand, show raw bytes as
/// labeled numbers, and search source code for the code that opens a file.
/// Nothing here writes to or changes any file.
/// </summary>
internal static class BinaryAnalyzer
{
    private const long MaxFullReadBytes = 64L * 1024 * 1024;

    public static string Analyze(string filePath)
    {
        var info = new FileInfo(filePath);
        long length = info.Length;
        var sb = new StringBuilder();
        sb.AppendLine($"File: {filePath}");
        sb.AppendLine($"Size: {length} bytes");

        if (length == 0)
        {
            sb.AppendLine("Kind: empty file.");
            sb.AppendLine("Like you're 3: an empty box with nothing inside.");
            return sb.ToString();
        }

        int headLength = (int)Math.Min(length, 4096);
        var head = new byte[headLength];
        using (var stream = File.OpenRead(filePath))
            stream.ReadExactly(head, 0, headLength);

        if (head.Length >= 2 && head[0] == 0x4D && head[1] == 0x5A)
        {
            if (length > MaxFullReadBytes)
            {
                sb.AppendLine("Kind: Windows program (starts with MZ), but it is too big to analyze safely here.");
                return sb.ToString();
            }
            AnalyzePe(File.ReadAllBytes(filePath), sb);
            return sb.ToString();
        }

        if (head.Length >= 4 && head[0] == 0x4D && head[1] == 0x59 && head[2] == 0x50 && head[3] == 0x00)
        {
            sb.AppendLine("Kind: UOP archive (starts with MYP). A container holding many smaller files inside it.");
            sb.AppendLine("Like you're 3: a toy chest with many small toys packed inside. Use list_uop_entries to see them.");
            return sb.ToString();
        }

        if (head.Length >= 4 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
        {
            sb.AppendLine("Kind: PNG picture.");
            sb.AppendLine("Like you're 3: a photograph.");
            return sb.ToString();
        }

        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04)
        {
            sb.AppendLine("Kind: ZIP archive.");
            sb.AppendLine("Like you're 3: a zipped-up bag of files.");
            return sb.ToString();
        }

        if (head.Length >= 4 && head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46)
        {
            sb.AppendLine("Kind: RIFF container (commonly WAV sound or AVI video).");
            return sb.ToString();
        }

        if (head.Length >= 2 && head[0] == 0x78 && (head[1] == 0x01 || head[1] == 0x5E || head[1] == 0x9C || head[1] == 0xDA))
        {
            sb.AppendLine("Kind: probably zlib-compressed data (header 78 xx). This is a guess from two bytes.");
            return sb.ToString();
        }

        if (LooksLikeText(head))
        {
            sb.AppendLine("Kind: plain text. Use read_uo_text_file to read it.");
            sb.AppendLine("Like you're 3: a note written in letters you can read.");
            return sb.ToString();
        }

        sb.AppendLine("Kind: UNKNOWN. No known header was recognized, so I will not guess what it is.");
        if (length % 4 == 0)
            sb.AppendLine("Hint: the size is a multiple of 4, so it could be a list of 32-bit numbers. Try view_binary_as_values.");
        sb.AppendLine("Next step: use find_file_usage_in_source to find the code that opens this file.");
        return sb.ToString();
    }

    private static bool LooksLikeText(byte[] head)
    {
        int printable = 0;
        foreach (var b in head)
            if (b == 9 || b == 10 || b == 13 || (b >= 32 && b < 127))
                printable++;
        return printable >= head.Length * 0.95;
    }

    private static void AnalyzePe(byte[] data, StringBuilder sb)
    {
        if (data.Length < 0x40)
        {
            sb.AppendLine("Kind: starts with MZ but is too short to be a valid program.");
            return;
        }

        int pe = BitConverter.ToInt32(data, 0x3C);
        if (pe <= 0 || pe + 24 > data.Length || data[pe] != 0x50 || data[pe + 1] != 0x45 || data[pe + 2] != 0 || data[pe + 3] != 0)
        {
            sb.AppendLine("Kind: old DOS program (MZ header, no Windows PE header).");
            return;
        }

        ushort machine = BitConverter.ToUInt16(data, pe + 4);
        ushort sectionCount = BitConverter.ToUInt16(data, pe + 6);
        uint timestamp = BitConverter.ToUInt32(data, pe + 8);
        ushort optionalSize = BitConverter.ToUInt16(data, pe + 20);
        ushort characteristics = BitConverter.ToUInt16(data, pe + 22);
        int opt = pe + 24;
        if (opt + 96 > data.Length)
        {
            sb.AppendLine("Kind: Windows program, but its header is cut off.");
            return;
        }

        ushort magic = BitConverter.ToUInt16(data, opt);
        bool plus = magic == 0x20B;
        ushort subsystem = BitConverter.ToUInt16(data, opt + 68);
        int ddOffset = opt + (plus ? 112 : 96);
        uint ddCount = ddOffset - 4 + 4 <= data.Length ? BitConverter.ToUInt32(data, ddOffset - 4) : 0;

        string arch = machine switch
        {
            0x14C => "32-bit (x86)",
            0x8664 => "64-bit (x64)",
            0xAA64 => "64-bit (ARM64)",
            _ => $"unknown machine 0x{machine:X}"
        };
        bool isDll = (characteristics & 0x2000) != 0;
        string subsystemName = subsystem switch
        {
            2 => "windowed app (GUI)",
            3 => "console app",
            _ => $"subsystem {subsystem}"
        };

        uint comRva = ddCount > 14 && ddOffset + 15 * 8 <= data.Length ? BitConverter.ToUInt32(data, ddOffset + 14 * 8) : 0;

        sb.AppendLine($"Kind: Windows {(isDll ? "library (DLL)" : "program (EXE)")}, {arch}, {subsystemName}.");
        sb.AppendLine($"Built (from header timestamp, may be faked): {DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(comRva != 0
            ? "Made with: .NET (managed code). Its inner code can be read with a .NET decompiler."
            : "Made with: native code (C/C++ style). Not .NET.");

        var sections = ReadSections(data, opt + optionalSize, sectionCount);
        sb.AppendLine($"Sections: {string.Join(", ", sections.Select(s => s.Name))}");

        uint importRva = ddCount > 1 && ddOffset + 16 <= data.Length ? BitConverter.ToUInt32(data, ddOffset + 8) : 0;
        var imports = ReadImportNames(data, sections, importRva);
        if (imports.Count > 0)
            sb.AppendLine($"Uses these system libraries ({imports.Count}): {string.Join(", ", imports)}");

        var strings = ReadAsciiStrings(data, 8, 30);
        if (strings.Count > 0)
        {
            sb.AppendLine("Readable text found inside (first few):");
            foreach (var s in strings)
                sb.AppendLine($"  {s}");
        }

        sb.AppendLine("Like you're 3: this is a robot toy that does things by itself. It does not hold information for other things to read.");
        sb.AppendLine("Not proven: what this program is FOR. Header facts only say what it is made of.");
    }

    private sealed record Section(string Name, uint VirtualAddress, uint VirtualSize, uint RawPointer, uint RawSize);

    private static List<Section> ReadSections(byte[] data, int start, int count)
    {
        var list = new List<Section>();
        for (int i = 0; i < count; i++)
        {
            int p = start + i * 40;
            if (p + 40 > data.Length) break;
            string name = Encoding.ASCII.GetString(data, p, 8).TrimEnd('\0');
            list.Add(new Section(
                name,
                BitConverter.ToUInt32(data, p + 12),
                BitConverter.ToUInt32(data, p + 8),
                BitConverter.ToUInt32(data, p + 20),
                BitConverter.ToUInt32(data, p + 16)));
        }
        return list;
    }

    private static long RvaToOffset(List<Section> sections, uint rva)
    {
        foreach (var s in sections)
        {
            uint size = Math.Max(s.VirtualSize, s.RawSize);
            if (rva >= s.VirtualAddress && rva < s.VirtualAddress + size)
                return s.RawPointer + (rva - s.VirtualAddress);
        }
        return -1;
    }

    private static List<string> ReadImportNames(byte[] data, List<Section> sections, uint importRva)
    {
        var names = new List<string>();
        if (importRva == 0) return names;
        long offset = RvaToOffset(sections, importRva);
        if (offset < 0) return names;

        for (int i = 0; i < 200; i++)
        {
            long p = offset + i * 20L;
            if (p + 20 > data.Length) break;
            uint nameRva = BitConverter.ToUInt32(data, (int)p + 12);
            if (nameRva == 0) break;
            long nameOffset = RvaToOffset(sections, nameRva);
            if (nameOffset < 0 || nameOffset >= data.Length) break;

            int end = (int)nameOffset;
            while (end < data.Length && data[end] != 0 && end - nameOffset < 128) end++;
            names.Add(Encoding.ASCII.GetString(data, (int)nameOffset, end - (int)nameOffset));
        }
        return names;
    }

    private static List<string> ReadAsciiStrings(byte[] data, int minLength, int maxCount)
    {
        var found = new List<string>();
        var seen = new HashSet<string>();
        var current = new StringBuilder();

        for (int i = 0; i <= data.Length && found.Count < maxCount; i++)
        {
            byte b = i < data.Length ? data[i] : (byte)0;
            if (b >= 32 && b < 127)
            {
                current.Append((char)b);
                continue;
            }

            if (current.Length >= minLength)
            {
                string s = current.ToString();
                int words = s.Count(ch => char.IsLetter(ch));
                int odd = s.Count(ch => !char.IsLetterOrDigit(ch) && ch != ' ' && ch != '.' && ch != '_' && ch != '-' && ch != '\\' && ch != '/' && ch != ':');
                bool looksReadable = words >= s.Length * 0.6 && odd <= s.Length * 0.1 && (s.Contains(' ') || s.Contains('.') || s.Contains('_'));
                if (looksReadable
                    && !s.StartsWith("!This program", StringComparison.Ordinal) && seen.Add(s))
                    found.Add(s.Length > 120 ? s[..120] : s);
            }
            current.Clear();
        }
        return found;
    }

    public static string ViewAsValues(string filePath, long offset, int length)
    {
        var info = new FileInfo(filePath);
        if (offset < 0 || offset >= info.Length)
            return $"Offset {offset} is out of range for a file of {info.Length} bytes.";

        length = Math.Clamp(length, 4, 512);
        int toRead = (int)Math.Min(length, info.Length - offset);
        toRead -= toRead % 4;
        if (toRead < 4)
            return "Fewer than 4 bytes remain at that offset.";

        var buffer = new byte[toRead];
        using (var stream = File.OpenRead(filePath))
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.ReadExactly(buffer, 0, toRead);
        }

        int count = toRead / 4;
        var values = new uint[count];
        var sb = new StringBuilder();
        sb.AppendLine($"File: {filePath} ({info.Length} bytes). Showing {toRead} bytes from offset {offset} as little-endian numbers.");
        sb.AppendLine("offset | u32 decimal | u32 hex | u16 low, u16 high | text");

        for (int i = 0; i < count; i++)
        {
            int p = i * 4;
            values[i] = BitConverter.ToUInt32(buffer, p);
            var text = new string(buffer.Skip(p).Take(4).Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
            sb.AppendLine($"{offset + p,8} | {values[i],11} | 0x{values[i]:X8} | {BitConverter.ToUInt16(buffer, p)}, {BitConverter.ToUInt16(buffer, p + 2)} | {text}");
        }

        var patterns = new List<string>();
        for (int i = 0; i < count; i++)
        {
            uint c = values[i];
            if (c < 3 || c > 4096) continue;

            int matched = 0;
            for (uint k = 1; k <= c; k++)
            {
                long idx = i + 2L * k - 1;
                if (idx >= count) break;
                if (values[idx] != k) { matched = -1; break; }
                matched++;
            }

            if (matched >= 3)
            {
                string note = matched < c ? $" (window ends after {matched} of {c} pairs)" : "";
                patterns.Add($"At offset {offset + i * 4L}: count={c}, then pairs of (number 1..{c}, value){note}.");
                i += 2 * matched;
            }
        }

        sb.AppendLine();
        sb.AppendLine(patterns.Count > 0
            ? "Pattern found ('count, then that many numbered pairs'):\n" + string.Join("\n", patterns)
            : "No 'count, then numbered pairs' pattern found in this window. That does not prove there is none elsewhere.");
        sb.AppendLine("These are raw numbers. What they MEAN is not proven until you find the code that reads this file.");
        return sb.ToString();
    }

    public static string FindUsage(string sourceFolder, string fileName, int maxResults)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
            return "Please give a file name such as housing.bin.";

        var word = new System.Text.RegularExpressions.Regex(
            $"(?<![A-Za-z0-9]){System.Text.RegularExpressions.Regex.Escape(stem)}(?![A-Za-z0-9])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var strong = new List<string>();
        var weak = new List<string>();
        int filesScanned = 0;

        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            filesScanned++;
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (!word.IsMatch(line)) continue;

                string entry = $"{file}:{lineNumber}: {line.Trim()}";
                bool exact = line.Contains(fileName, StringComparison.OrdinalIgnoreCase);
                if (exact) strong.Add(entry); else weak.Add(entry);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Searched {filesScanned} .cs files in {sourceFolder}.");
        sb.AppendLine($"Exact file name '{fileName}' found: {strong.Count}");
        foreach (var s in strong.Take(maxResults)) sb.AppendLine($"  {s}");
        sb.AppendLine($"Only the word '{stem}' found (names, comments; often unrelated): {weak.Count}");
        foreach (var s in weak.Take(maxResults)) sb.AppendLine($"  {s}");

        if (strong.Count == 0)
            sb.AppendLine("NOTHING FOUND that opens this file by name. A name built from pieces at runtime could still be missed; this search cannot prove the file is unused.");
        return sb.ToString();
    }
}
