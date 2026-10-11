using ICSharpCode.Decompiler;
using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// Basic self-check tool so we can confirm the decompiler engine loads inside the server.
/// </summary>
internal class DecompilerInfoTools
{
    [McpServerTool]
    [Description("Reports that the vseDecompiler server is running and which ILSpy decompiler engine version it loaded. Read-only; touches no files.")]
    public string GetDecompilerInfo()
    {
        var engine = typeof(DecompilerSettings).Assembly.GetName();
        return $"vseDecompiler is running. ILSpy engine: {engine.Name} {engine.Version}.";
    }
}
