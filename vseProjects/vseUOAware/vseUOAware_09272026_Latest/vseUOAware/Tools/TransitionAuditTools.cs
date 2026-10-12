using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

[McpServerToolType]
internal class TransitionAuditTools
{
    [McpServerTool]
    [Description("Read-only audit of a MapCreator Transitions folder against Terrain.xml. Sorts every file into KEEP, REDO (some rules use terrain IDs missing from Terrain.xml) or DELETE-CANDIDATE (empty or all rules broken), lists missing terrain IDs with usage counts, rules lacking MapTiles, and duplicate keys. Writes AUDIT.md into a new/empty output folder. Never edits or deletes any XML, so users can decide what to do with their own copies.")]
    public static string AuditTransitionXml(
        [Description("Full path to the Transitions folder.")] string transitionsFolderPath,
        [Description("Full path to Terrain.xml.")] string terrainXmlPath,
        [Description("Full path to a new/empty output folder for AUDIT.md.")] string outputFolderPath)
    {
        var r = TransitionAudit.Run(transitionsFolderPath, terrainXmlPath, outputFolderPath);
        return (r.Ok ? "OK\n" : "FAILED\n") + r.Report;
    }
}
