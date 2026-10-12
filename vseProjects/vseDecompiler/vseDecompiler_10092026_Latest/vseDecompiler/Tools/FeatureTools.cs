using ModelContextProtocol.Server;
using System.ComponentModel;

internal class FeatureTools
{
    [McpServerTool(Name = "search_members")]
    [Description("Read-only. Finds types, methods, fields and properties whose name contains the query in a .NET assembly. First step of 'how does feature X work'.")]
    public string SearchMembers(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Case-insensitive text in the type/member name.")] string query,
        [Description("Maximum results. Default 100.")] int max = 100)
        => FeatureInvestigator.SearchMembers(assemblyPath, query, max);

    [McpServerTool(Name = "search_code")]
    [Description("Read-only. Decompiles types and returns the C# lines (with context) that contain the text. Use it to trace where a method, field or string is used. Scans at most 400 top-level types.")]
    public string SearchCode(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Case-insensitive text to find in the decompiled code.")] string text,
        [Description("Optional text the type name must contain, to limit the scan.")] string? typeFilter = null,
        [Description("Lines of context around each hit. Default 3.")] int contextLines = 3,
        [Description("Maximum hits. Default 30.")] int max = 30)
        => FeatureInvestigator.SearchCode(assemblyPath, text, typeFilter, contextLines, max);

    [McpServerTool(Name = "extract_strings")]
    [Description("Read-only. Lists string literals compiled into a .NET assembly (URLs, messages, keys, file names), optionally filtered.")]
    public string ExtractStrings(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Optional case-insensitive filter text.")] string? contains = null,
        [Description("Maximum results. Default 200.")] int max = 200)
        => FeatureInvestigator.ExtractStrings(assemblyPath, contains, max);

    [McpServerTool(Name = "list_dependencies")]
    [Description("Read-only. Lists the assemblies and native libraries (P/Invoke) a .NET assembly depends on.")]
    public string ListDependencies(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath)
        => FeatureInvestigator.ListDependencies(assemblyPath);

    [McpServerTool(Name = "feature_report")]
    [Description("Read-only. One-call evidence report for a feature keyword: matching types/members, string literals, code mentions and dependencies. Writes nothing.")]
    public string FeatureReport(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Keyword describing the feature, e.g. 'login' or 'export'.")] string keyword,
        [Description("Maximum results per section. Default 30.")] int max = 30)
        => FeatureInvestigator.FeatureReport(assemblyPath, keyword, max);

    [McpServerTool(Name = "find_usages")]
    [Description("Read-only. Finds every method that calls or references a member/type (matched by name text), from the compiled IL. Answers 'what uses X'.")]
    public string FindUsages(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Case-insensitive text of the member, e.g. 'Namespace.Type::Method' or just 'Method'.")] string member,
        [Description("Maximum results. Default 100.")] int max = 100)
        => FeatureInvestigator.FindUsages(assemblyPath, member, max);

    [McpServerTool(Name = "list_resources")]
    [Description("Read-only. Lists resources embedded in a .NET assembly (name, type, size).")]
    public string ListResources(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Maximum results. Default 200.")] int max = 200)
        => FeatureInvestigator.ListResources(assemblyPath, max);

    [McpServerTool(Name = "show_il")]
    [Description("Read-only. Shows the raw IL disassembly of methods whose name contains the text.")]
    public string ShowIl(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Case-insensitive text of the method, e.g. 'Type::Method'.")] string method,
        [Description("Maximum methods. Default 3.")] int max = 3)
        => FeatureInvestigator.ShowIl(assemblyPath, method, max);

    [McpServerTool(Name = "trace_calls")]
    [Description("Read-only. Shows what a method calls, then what those call, down to the given depth (1-5), from the compiled IL. Answers 'how does X work'.")]
    public string TraceCalls(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Case-insensitive text of the starting method, e.g. 'Type::Method'.")] string method,
        [Description("Call depth 1-5. Default 2.")] int depth = 2,
        int max = 100,
        [Description("Also show calls into other assemblies (framework/libraries). Default false.")] bool includeFramework = false)
        => FeatureInvestigator.TraceCalls(assemblyPath, method, depth, max, includeFramework);
}
