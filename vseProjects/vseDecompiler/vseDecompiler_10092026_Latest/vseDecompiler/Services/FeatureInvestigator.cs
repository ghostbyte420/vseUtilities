using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;

/// <summary>Read-only "how does this feature work" helpers built on the ILSpy engine. Writes nothing.</summary>
internal static class FeatureInvestigator
{
    private const int MaxTypesScanned = 400;

    private static DecompilerSettings Settings() => new(LanguageVersion.Latest) { ThrowOnAssemblyResolveErrors = false };

    public static string SearchMembers(string path, string query, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        if (string.IsNullOrWhiteSpace(query)) return "Give a non-empty query.";

        var d = new CSharpDecompiler(path, Settings());
        var hits = new List<string>();
        foreach (var t in d.TypeSystem.MainModule.TypeDefinitions.Where(t => !t.FullName.Contains('<')))
        {
            if (t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                hits.Add($"{t.Kind,-9} {t.FullName}");
            foreach (var m in t.Members.Where(m => !m.Name.Contains('<') && m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                hits.Add($"{m.SymbolKind,-9} {t.FullName}::{m.Name}");
        }
        return Format($"members/types matching '{query}' in {Path.GetFileName(path)}", hits, max);
    }

    public static string SearchCode(string path, string text, string? typeFilter, int contextLines, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        if (string.IsNullOrWhiteSpace(text)) return "Give non-empty text to search for.";

        var d = new CSharpDecompiler(path, Settings());
        var types = d.TypeSystem.MainModule.TypeDefinitions
            .Where(t => t.DeclaringTypeDefinition is null)
            .Where(t => string.IsNullOrEmpty(typeFilter) || t.FullName.Contains(typeFilter, StringComparison.OrdinalIgnoreCase))
            .Take(MaxTypesScanned).ToList();

        var hits = new List<string>();
        var failed = 0;
        foreach (var t in types)
        {
            string code;
            try { code = d.DecompileTypeAsString(t.FullTypeName); }
            catch (Exception) { failed++; continue; }

            var lines = code.Split('\n');
            for (var i = 0; i < lines.Length && hits.Count <= max; i++)
            {
                if (!lines[i].Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
                var from = Math.Max(0, i - contextLines);
                var to = Math.Min(lines.Length - 1, i + contextLines);
                var sb = new StringBuilder($"{t.FullName} line {i + 1}:\n");
                for (var j = from; j <= to; j++) sb.AppendLine("    " + lines[j].TrimEnd());
                hits.Add(sb.ToString());
            }
        }
        var note = failed > 0 ? $" ({failed} types could not be decompiled and were skipped)" : "";
        return Format($"code lines containing '{text}' in {Path.GetFileName(path)}{note}", hits, max);
    }

    public static string ExtractStrings(string path, string? contains, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;

        using var pe = new PEFile(path);
        var md = pe.Metadata;
        var hits = new List<string>();
        var handle = MetadataTokens.UserStringHandle(1);
        while (!handle.IsNil)
        {
            var s = md.GetUserString(handle);
            if (s.Length > 0 && (string.IsNullOrEmpty(contains) || s.Contains(contains, StringComparison.OrdinalIgnoreCase)))
                hits.Add(s.Replace("\r", "\\r").Replace("\n", "\\n"));
            handle = md.GetNextHandle(handle);
        }
        return Format($"string literals{(string.IsNullOrEmpty(contains) ? "" : $" containing '{contains}'")} in {Path.GetFileName(path)}", hits, max);
    }

    public static string ListDependencies(string path)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;

        using var pe = new PEFile(path);
        var md = pe.Metadata;
        var sb = new StringBuilder();
        sb.AppendLine($"Assembly references of {Path.GetFileName(path)}:");
        foreach (var r in md.AssemblyReferences.Select(h => md.GetAssemblyReference(h)))
            sb.AppendLine($"  {md.GetString(r.Name)} {r.Version}");

        var natives = Enumerable.Range(1, md.GetTableRowCount(TableIndex.ModuleRef))
            .Select(i => md.GetString(md.GetModuleReference(MetadataTokens.ModuleReferenceHandle(i)).Name))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        sb.AppendLine(natives.Count == 0 ? "Native libraries (P/Invoke): none" : "Native libraries (P/Invoke):");
        foreach (var n in natives) sb.AppendLine($"  {n}");
        return sb.ToString();
    }

    public static string FeatureReport(string path, string keyword, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        if (string.IsNullOrWhiteSpace(keyword)) return "Give a keyword describing the feature.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Feature investigation: '{keyword}' in {Path.GetFileName(path)}");
        sb.AppendLine("Evidence only: every line below came from the assembly, nothing is guessed.\n");
        sb.AppendLine("## Matching types and members");
        sb.AppendLine(SearchMembers(path, keyword, max));
        sb.AppendLine("## Matching string literals");
        sb.AppendLine(ExtractStrings(path, keyword, max));
        sb.AppendLine("## Code that mentions it");
        sb.AppendLine(SearchCode(path, keyword, null, 2, Math.Min(max, 15)));
        sb.AppendLine("## Dependencies");
        sb.AppendLine(ListDependencies(path));
        return sb.ToString();
    }

    private static readonly Dictionary<short, System.Reflection.Emit.OperandType> OpTable =
        typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!)
            .ToDictionary(o => o.Value, o => o.OperandType);

    private static string TypeName(MetadataReader md, EntityHandle h) => h.Kind switch
    {
        HandleKind.TypeDefinition => Qualify(md, md.GetTypeDefinition((TypeDefinitionHandle)h).Namespace, md.GetTypeDefinition((TypeDefinitionHandle)h).Name),
        HandleKind.TypeReference => Qualify(md, md.GetTypeReference((TypeReferenceHandle)h).Namespace, md.GetTypeReference((TypeReferenceHandle)h).Name),
        _ => "?"
    };

    private static string Qualify(MetadataReader md, StringHandle ns, StringHandle name)
        => ns.IsNil ? md.GetString(name) : md.GetString(ns) + "." + md.GetString(name);

    private static string MemberName(MetadataReader md, EntityHandle h)
    {
        switch (h.Kind)
        {
            case HandleKind.MethodDefinition:
                var m = md.GetMethodDefinition((MethodDefinitionHandle)h);
                return TypeName(md, m.GetDeclaringType()) + "::" + md.GetString(m.Name);
            case HandleKind.FieldDefinition:
                var f = md.GetFieldDefinition((FieldDefinitionHandle)h);
                return TypeName(md, f.GetDeclaringType()) + "::" + md.GetString(f.Name);
            case HandleKind.MemberReference:
                var r = md.GetMemberReference((MemberReferenceHandle)h);
                return TypeName(md, r.Parent) + "::" + md.GetString(r.Name);
            case HandleKind.MethodSpecification:
                return MemberName(md, md.GetMethodSpecification((MethodSpecificationHandle)h).Method);
            default:
                return "?";
        }
    }

    private static List<(string Caller, string Target)> ReadEdges(string path)
    {
        using var pe = new PEFile(path);
        var md = pe.Metadata;
        var edges = new List<(string, string)>();
        foreach (var mh in md.MethodDefinitions)
        {
            var def = md.GetMethodDefinition(mh);
            if (def.RelativeVirtualAddress == 0) continue;
            byte[]? il;
            try { il = pe.Reader.GetMethodBody(def.RelativeVirtualAddress).GetILBytes(); }
            catch (Exception) { continue; }
            if (il is null) continue;
            var caller = MemberName(md, mh);
            for (var i = 0; i < il.Length;)
            {
                short op = il[i++];
                if (op == 0xFE && i < il.Length) op = (short)(0xFE00 | il[i++]);
                if (!OpTable.TryGetValue(op, out var kind)) break;
                int size;
                switch (kind)
                {
                    case System.Reflection.Emit.OperandType.InlineNone: size = 0; break;
                    case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
                    case System.Reflection.Emit.OperandType.ShortInlineI:
                    case System.Reflection.Emit.OperandType.ShortInlineVar: size = 1; break;
                    case System.Reflection.Emit.OperandType.InlineVar: size = 2; break;
                    case System.Reflection.Emit.OperandType.InlineI8:
                    case System.Reflection.Emit.OperandType.InlineR: size = 8; break;
                    case System.Reflection.Emit.OperandType.InlineSwitch:
                        size = i + 4 <= il.Length ? 4 + 4 * BitConverter.ToInt32(il, i) : il.Length; break;
                    default: size = 4; break;
                }
                if ((kind == System.Reflection.Emit.OperandType.InlineMethod || kind == System.Reflection.Emit.OperandType.InlineField) && i + 4 <= il.Length)
                {
                    try { edges.Add((caller, MemberName(md, MetadataTokens.EntityHandle(BitConverter.ToInt32(il, i))))); }
                    catch (Exception) { }
                }
                i += size;
            }
        }
        return edges;
    }

    public static string FindUsages(string path, string member, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        if (string.IsNullOrWhiteSpace(member)) return "Give a non-empty member or type name.";
        var hits = ReadEdges(path)
            .Where(e => e.Target.Contains(member, StringComparison.OrdinalIgnoreCase))
            .Select(e => $"{e.Caller}  ->  {e.Target}").Distinct().ToList();
        return Format($"usages (IL call/field references) of '{member}' in {Path.GetFileName(path)}", hits, max);
    }

    public static string TraceCalls(string path, string method, int depth, int max, bool includeFramework = false)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        if (string.IsNullOrWhiteSpace(method)) return "Give a non-empty method name.";
        depth = Math.Clamp(depth, 1, 5);
        var all = ReadEdges(path);
        var own = all.Select(e => e.Caller).ToHashSet();
        var calls = all.Where(e => !e.Target.Contains('<') && (includeFramework || own.Contains(e.Target)))
            .ToLookup(e => e.Caller, e => e.Target);
        var starts = own.Where(k => !k.Contains('<') && k.Contains(method, StringComparison.OrdinalIgnoreCase)).ToList();
        var lines = new List<string>();
        var seen = new HashSet<string>();
        void Walk(string node, int level)
        {
            if (level > depth || !seen.Add(node)) return;
            foreach (var t in calls[node].Distinct())
            {
                lines.Add(new string(' ', level * 2) + node + "  ->  " + t);
                Walk(t, level + 1);
            }
        }
        foreach (var s in starts) Walk(s, 0);
        return Format($"outgoing calls from '{method}' (depth {depth}) in {Path.GetFileName(path)}; {starts.Count} matching method(s)", lines, max);
    }

    public static string ListResources(string path, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        using var pe = new PEFile(path);
        var hits = new List<string>();
        foreach (var r in pe.Resources)
        {
            long size = -1;
            try { using var s = r.TryOpenStream(); if (s is not null) size = s.Length; } catch (Exception) { }
            hits.Add($"{r.ResourceType,-8} {size,10} bytes  {r.Name}");
        }
        return Format($"embedded resources in {Path.GetFileName(path)}", hits, max);
    }

    public static string ShowIl(string path, string method, int max)
    {
        var err = AssemblyDecompiler.EnsureManaged(path);
        if (err.Length > 0) return err;
        if (string.IsNullOrWhiteSpace(method)) return "Give a non-empty method name.";
        using var pe = new PEFile(path);
        var md = pe.Metadata;
        var matches = md.MethodDefinitions.Where(h => MemberName(md, h).Contains(method, StringComparison.OrdinalIgnoreCase)).ToList();
        var sb = new StringBuilder();
        foreach (var h in matches.Take(max))
        {
            var w = new StringWriter();
            try { new ICSharpCode.Decompiler.Disassembler.ReflectionDisassembler(new PlainTextOutput(w), CancellationToken.None).DisassembleMethod(pe, h); }
            catch (Exception ex) { w.WriteLine("// could not disassemble: " + ex.Message); }
            sb.AppendLine("// ---- " + MemberName(md, h));
            sb.AppendLine(w.ToString());
        }
        return $"{Math.Min(matches.Count, max)} of {matches.Count} method(s) matching '{method}' (IL)\n" + sb;
    }

    private static string Format(string title, List<string> hits, int max)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Math.Min(hits.Count, max)} shown of {hits.Count} {title}");
        foreach (var h in hits.Take(max)) sb.AppendLine("  " + h);
        if (hits.Count > max) sb.AppendLine($"  ... {hits.Count - max} more not shown (raise max or narrow the query).");
        return sb.ToString();
    }
}
