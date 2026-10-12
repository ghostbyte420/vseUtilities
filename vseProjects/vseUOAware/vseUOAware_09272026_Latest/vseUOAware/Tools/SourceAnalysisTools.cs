using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

[McpServerToolType]
internal class SourceAnalysisTools
{
    [McpServerTool]
    [Description("Draws a plain-English map of a C# program folder: its parts (folders), which code files name data files like Terrain.xml, every data file name seen, and the most used types. Read-only. Skips bin, obj and test-evidence folders.")]
    public static string MapProgramPipeline(
        [Description("Full path to the source folder, e.g. the MapCreator solution folder.")] string sourceFolderPath) =>
        CompilerPipelineMap.Run(sourceFolderPath);

    [McpServerTool]
    [Description("Lists what the C# code really expects from its input files: data file names it opens, XML tag and attribute names it looks for, bitmap size and color checks, and rule-style messages. Read-only. Trust this over a guide if they disagree.")]
    public static string ExtractInputContract(
        [Description("Full path to the source folder.")] string sourceFolderPath) =>
        InputContract.Run(sourceFolderPath);

    [McpServerTool]
    [Description("Finds numbers typed straight into C# code with no name (like a tile ID of 1078) and fixed file paths. Shows the most repeated numbers and where each one is. Numbers that already have a name (const) are counted but not listed. Read-only.")]
    public static string FindHardcodedValues(
        [Description("Full path to the source folder.")] string sourceFolderPath,
        [Description("Most lines to list per section (1-2000). Default 100.")] int maxResults = 100,
        [Description("Also scan *.Designer.cs form files. Default false, because they are full of layout numbers.")] bool includeDesigner = false,
        [Description("Smallest number of digits that counts as a magic number (2-8). Default 3.")] int minDigits = 3) =>
        HardcodedValues.Run(sourceFolderPath, maxResults, includeDesigner, minDigits);

    [McpServerTool]
    [Description("Shows what could break if you change a class or method name: where it is defined, every place it is used grouped by part of the program, and the types that depend on those users. Reads text, so same-named things are both counted. Read-only.")]
    public static string AnalyzeChangeImpact(
        [Description("Full path to the source folder.")] string sourceFolderPath,
        [Description("One class or method name, no spaces or dots.")] string symbolName,
        [Description("Most uses to list (1-1000). Default 100.")] int maxResults = 100) =>
        CodeImpact.Run(sourceFolderPath, symbolName, maxResults);

    [McpServerTool]
    [Description("Compares two compile output folders (before and after a change). Says which files are the same, different or only in one folder. For map files (mapN.mul) it counts changed blocks, land tile IDs and heights. Does not run the compiler. Read-only.")]
    public static string CompareCompileOutputs(
        [Description("Full path to the BEFORE output folder.")] string beforeFolderPath,
        [Description("Full path to the AFTER output folder.")] string afterFolderPath,
        [Description("Most changed tiles to list (1-1000). Default 50.")] int maxResults = 50,
        [Description("Map height in 8x8 blocks, to show X and Y. Use 0 if unknown. Default 0.")] int facetHeightBlocks = 0) =>
        CompileCompare.Run(beforeFolderPath, afterFolderPath, maxResults, facetHeightBlocks);

    [McpServerTool]
    [Description("Writes a fill-in-the-blanks transition XML template into a new empty folder. Tiles are comments like 'LAND TILE HERE: top edge' instead of real IDs, so you can see how a transition is built. Needs Terrain.xml for the two terrain IDs.")]
    public static string CreateTransitionTemplate(
        [Description("Full path to Terrain.xml.")] string terrainXmlPath,
        [Description("Terrain ID that sits in the middle of each rule.")] int centerTerrainId,
        [Description("Terrain ID that surrounds it.")] int otherTerrainId,
        [Description("Full path to a new or empty output folder.")] string outputFolderPath) =>
        TransitionTemplate.Run(terrainXmlPath, centerTerrainId, otherTerrainId, outputFolderPath);

    [McpServerTool]
    [Description("Writes a commented 2-WAY transition template (terrain A in the middle, terrain B around it: 12 representative rules) into a new empty folder. Tiles are REPLACE_ME placeholders. Also writes diagrams/rule_NN.png and a text version diagrams/rules.txt for AIs that cannot view images; the reply lists full paths and the rules as text.")]
    public static string CreateTwoWayTemplate(
        [Description("Full path to Terrain.xml.")] string terrainXmlPath,
        [Description("Terrain ID A.")] int terrainA,
        [Description("Terrain ID B.")] int terrainB,
        [Description("Full path to a new or empty output folder.")] string outputFolderPath) =>
        TransitionTemplatesWays.RunTwoWay(terrainXmlPath, terrainA, terrainB, outputFolderPath);

    [McpServerTool]
    [Description("Writes a commented 3-WAY transition template (A in the middle, B and C on two different sides, 12 rules) into a new empty folder. Tiles are REPLACE_ME placeholders.")]
    public static string CreateThreeWayTemplate(
        [Description("Full path to Terrain.xml.")] string terrainXmlPath,
        [Description("Terrain ID A (middle).")] int terrainA,
        [Description("Terrain ID B.")] int terrainB,
        [Description("Terrain ID C.")] int terrainC,
        [Description("Full path to a new or empty output folder.")] string outputFolderPath) =>
        TransitionTemplatesWays.RunThreeWay(terrainXmlPath, terrainA, terrainB, terrainC, outputFolderPath);
}
