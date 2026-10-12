using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
internal class MapCreatorXmlGuideTools
{
    public const string GuideText = """
# MapCreator XML guide (vseUOAware tools)

All tools write to a NEW/EMPTY output folder plus INSTALL.md and never touch your originals. None of them guess tile IDs.

## Vocabulary (the part everyone mixes up)
- Terrain group ID: a 0-255 number defined in Terrain.xml (Name, ID, TileID, R, G, B, Base, Random). The colour is the palette entry in Terrain.bmp. ID 254 is hardcoded by the compiler: never use it.
- HashKey: 18 hex chars = nine terrain group IDs in order NW,N,NE,W,C,E,SW,S,SE. These are NOT tile IDs.
- MapTile: a LAND tile ID (0-16383) the compiler paints for that rule. Several = random variants.
- StaticTile: an item/static art ID placed on top. Optional.
- Common.xml: the uniform rules (one terrain ID repeated 9 times) = plain fill for that terrain.
- Duplicate HashKeys: the first one loaded wins, later copies are silently ignored.
- A key whose terrain ID is not in Terrain.xml can never fire (nothing paints it).

## Workflow
1. AuditTransitionXml  - read-only. KEEP / REDO / DELETE-CANDIDATE per file, missing terrain IDs, duplicates, rules without MapTiles. Decide what to keep.
2. CheckSourceBitmaps / FindMissingTransitions - what your Terrain.bmp actually needs.
3. CreateTerrainType   - add a terrain (lowest free ID with id=-1).
4. EditCommonXml       - add/replace/remove the plain fill rule for a terrain.
5. CreateTransitionXml - add one 2-way/3-way rule (you give land tile IDs).
6. RepairTransitionXml - mechanical fixes only: malformed keys, later duplicates, out-of-range tiles, optional fills you supply.
7. ValidateTransitionFolder / DecodeTransitionHashKey - re-check and read keys as a 3x3 grid.
8. Copy the output's XMLFacetData\Custom\... into MapCreator.Engine\Compiler\XMLFacetData\Custom\..., rebuild MapCreator.Engine, re-run FindMissingTransitions.

## Templates (CreateTwoWayTemplate / CreateThreeWayTemplate)
- 2-way: terrain A is always the middle square, B surrounds it. 12 representative rules: 4 edges (NS, SN, WE, EW), 4 small corners (NWA, SEA, NEA, SWA), 4 big corners (NWB, SEB, NEB, SWB). A teaching template, not the exhaustive production set.
- 3-way: A in the middle, B and C on two different sides, 12 rules.
- Output: XMLFacetData\Custom\Transitions\name.xml, diagrams\rule_01..12.png (pink = B), diagrams\rules.txt, INSTALL.md.
- If you cannot view images: read diagrams\rules.txt or the tool reply. Each line is the 3x3 as top / middle / bottom plus the HashKey. The XML comments carry the same information.
- The tool reply lists full paths of every file. Replace each REPLACE_ME with a land tile ID, then run ValidateTransitionXml.

## Cross-reference tips
- To find what a land tile looks like: GetLandTile / RenderArtTile. Statics: GetItemTile.
- Unknown terrain ID in a report: look it up in Terrain.xml by decimal ID (hex pair in the key -> decimal). If absent, the rule is dead; delete/redo it or add the terrain.
""";

    [McpServerTool]
    [Description("Returns the guide for the MapCreator XML tool family: vocabulary (terrain group ID vs HashKey vs MapTile vs StaticTile), the recommended audit -> create -> repair -> install workflow, and which tool does what. No files are read or written.")]
    public static string GetMapCreatorXmlGuide() => GuideText;
}
